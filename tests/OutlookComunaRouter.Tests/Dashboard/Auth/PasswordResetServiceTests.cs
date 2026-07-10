using Microsoft.Data.Sqlite;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Dashboard.Auth;
using OutlookComunaRouter.Mail;
using Xunit;

namespace OutlookComunaRouter.Tests.Dashboard.Auth;

public class PasswordResetServiceTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"reset-service-test-{Guid.NewGuid():N}.db");
    private readonly IUserRepository users;
    private readonly IPasswordResetTokenRepository tokens;
    private readonly FakeMailSender mailSender = new();
    private readonly PasswordResetService sut;

    public PasswordResetServiceTests()
    {
        users = new UserRepository($"Data Source={dbPath}");
        users.EnsureSchema();
        tokens = new PasswordResetTokenRepository($"Data Source={dbPath}");
        tokens.EnsureSchema();

        var options = new RouterOptions
        {
            Ews = new EwsOptions { Url = "https://mail.munivalpo.cl/EWS/Exchange.asmx", Username = "u", Password = "p" },
            MailboxAddress = "cambiodedomicilio@munivalpo.cl",
            OwnDomain = "munivalpo.cl",
            SqliteDbPath = dbPath,
            ComunaDirectoryCsvPath = "unused.csv",
            ReportCsvPath = "unused-report.csv",
            NotificationEmailAddress = "raul.salazar1984@gmail.com",
            PublicBaseUrl = "https://localhost:5001"
        };

        sut = new PasswordResetService(users, tokens, mailSender, options);

        var (hash, salt, iterations) = PasswordHasher.Hash("Cont2026#");
        users.Insert(new DashboardUser { Username = "operador", PasswordHash = hash, PasswordSalt = salt, Iterations = iterations });
    }

    [Fact]
    public async Task RequestResetAsync_UserWithEmail_SendsEmailAndReturnsRequested()
    {
        users.UpdateEmail(users.FindByUsername("operador")!.Id, "operador@munivalpo.cl");

        var outcome = await sut.RequestResetAsync("operador", CancellationToken.None);

        Assert.Equal(PasswordResetRequestOutcome.Requested, outcome);
        var sent = Assert.Single(mailSender.SentMessages);
        Assert.Equal("operador@munivalpo.cl", sent.To);
        Assert.Contains("https://localhost:5001/ResetPassword?token=", sent.Body);
    }

    [Fact]
    public async Task RequestResetAsync_UserWithoutEmail_DoesNotSendAndReturnsNoEmailOnFile()
    {
        var outcome = await sut.RequestResetAsync("operador", CancellationToken.None);

        Assert.Equal(PasswordResetRequestOutcome.NoEmailOnFile, outcome);
        Assert.Empty(mailSender.SentMessages);
    }

    [Fact]
    public async Task RequestResetAsync_UnknownUser_DoesNotSendAndReturnsUserNotFound()
    {
        var outcome = await sut.RequestResetAsync("no-existe", CancellationToken.None);

        Assert.Equal(PasswordResetRequestOutcome.UserNotFound, outcome);
        Assert.Empty(mailSender.SentMessages);
    }

    [Fact]
    public async Task RequestResetAsync_SecondRequest_InvalidatesFirstToken()
    {
        users.UpdateEmail(users.FindByUsername("operador")!.Id, "operador@munivalpo.cl");
        await sut.RequestResetAsync("operador", CancellationToken.None);
        var firstToken = ExtractToken(mailSender.SentMessages[0].Body);

        await sut.RequestResetAsync("operador", CancellationToken.None);

        var outcome = sut.CompleteReset(firstToken, "NuevaClave2027#");
        Assert.Equal(PasswordResetCompletionOutcome.TokenAlreadyUsed, outcome); // invalidation marks it used, not deleted
    }

    [Fact]
    public async Task CompleteReset_ValidToken_UpdatesPasswordAndMarksTokenUsed()
    {
        users.UpdateEmail(users.FindByUsername("operador")!.Id, "operador@munivalpo.cl");
        await sut.RequestResetAsync("operador", CancellationToken.None);
        var token = ExtractToken(mailSender.SentMessages[0].Body);

        var outcome = sut.CompleteReset(token, "NuevaClave2027#");

        Assert.Equal(PasswordResetCompletionOutcome.Success, outcome);
        var user = users.FindByUsername("operador")!;
        Assert.True(PasswordHasher.Verify("NuevaClave2027#", user.PasswordHash, user.PasswordSalt, user.Iterations));
    }

    [Fact]
    public async Task CompleteReset_TokenAlreadyUsed_IsRejected()
    {
        users.UpdateEmail(users.FindByUsername("operador")!.Id, "operador@munivalpo.cl");
        await sut.RequestResetAsync("operador", CancellationToken.None);
        var token = ExtractToken(mailSender.SentMessages[0].Body);
        sut.CompleteReset(token, "NuevaClave2027#");

        var secondAttempt = sut.CompleteReset(token, "OtraClave2028#");

        Assert.Equal(PasswordResetCompletionOutcome.TokenAlreadyUsed, secondAttempt);
    }

    [Fact]
    public void CompleteReset_UnknownToken_ReturnsInvalidOrExpired()
    {
        var outcome = sut.CompleteReset("no-existe", "NuevaClave2027#");

        Assert.Equal(PasswordResetCompletionOutcome.InvalidOrExpiredToken, outcome);
    }

    [Fact]
    public async Task CompleteReset_PasswordTooShort_IsRejectedWithoutConsumingToken()
    {
        users.UpdateEmail(users.FindByUsername("operador")!.Id, "operador@munivalpo.cl");
        await sut.RequestResetAsync("operador", CancellationToken.None);
        var token = ExtractToken(mailSender.SentMessages[0].Body);

        var outcome = sut.CompleteReset(token, "corta");

        Assert.Equal(PasswordResetCompletionOutcome.PasswordTooShort, outcome);
        // Token must still be usable afterwards since it wasn't actually consumed.
        Assert.Equal(PasswordResetCompletionOutcome.Success, sut.CompleteReset(token, "ClaveValida2029#"));
    }

    private static string ExtractToken(string emailBody)
    {
        var marker = "token=";
        var start = emailBody.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = emailBody.IndexOfAny(['\r', '\n'], start);
        return end < 0 ? emailBody[start..].Trim() : emailBody[start..end].Trim();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
    }

    private sealed class FakeMailSender : IMailSender
    {
        public List<(string To, string Subject, string Body)> SentMessages { get; } = [];

        public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
        {
            SentMessages.Add((toAddress, subject, body));
            return Task.CompletedTask;
        }
    }
}
