using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Directories;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Mail;
using OutlookComunaRouter.Notifications;
using OutlookComunaRouter.Persistence;
using OutlookComunaRouter.Routing;
using Xunit;

namespace OutlookComunaRouter.Tests.Routing;

public class AddressChangeRoutingServiceTests : IDisposable
{
    private static readonly IReadOnlyList<ComunaContact> Contacts =
    [
        new ComunaContact("Catemu", "rfloresc@municatemu.cl", "municatemu.cl")
    ];

    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"routing-test-{Guid.NewGuid():N}.db");
    private readonly IPersonRequestRepository repository;
    private readonly FakeMailSender mailSender = new();
    private readonly FakeNotificationChannel notificationChannel = new();
    private readonly AddressChangeRoutingService sut;

    public AddressChangeRoutingServiceTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        repository.EnsureSchema();

        var options = new RouterOptions
        {
            Ews = new EwsOptions { Url = "https://mail.munivalpo.cl/EWS/Exchange.asmx", Username = "u", Password = "p" },
            MailboxAddress = "cambiodedomicilio@munivalpo.cl",
            OwnDomain = "munivalpo.cl",
            SourceFolderName = "CARP. PARA PEDIR",
            ConfirmationFolderName = "CARP. YA PEDIDAS",
            SqliteDbPath = dbPath,
            ComunaDirectoryCsvPath = "unused.csv",
            ReportCsvPath = "unused-report.csv",
            NotificationEmailAddress = "raul.salazar1984@gmail.com"
        };

        sut = new AddressChangeRoutingService(
            repository,
            new ComunaDirectory(),
            mailSender,
            [notificationChannel],
            options,
            NullLogger<AddressChangeRoutingService>.Instance);
    }

    [Fact]
    public void ProcessIncomingRequest_KnownComunaValidData_RecordsAsPendingWithoutSending()
    {
        sut.ProcessIncomingRequest(NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7"), Contacts);

        Assert.Empty(mailSender.SentMessages); // nothing is ever sent from this path
        var stored = repository.GetAll().Single();
        Assert.Equal(RequestStatus.Pending, stored.Status);
        Assert.Equal("Catemu", stored.Comuna);
    }

    [Fact]
    public void ProcessIncomingRequest_UnknownDomain_IsIgnored()
    {
        sut.ProcessIncomingRequest(
            NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7", sender: "alguien@otracomuna.cl"),
            Contacts);

        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void ProcessIncomingRequest_MissingData_RecordedAsNeedsReview()
    {
        sut.ProcessIncomingRequest(NewEmail("msg-1", "Correo sin datos reconocibles."), Contacts);

        Assert.True(repository.GetAll().Single().NeedsReview);
    }

    [Fact]
    public void ProcessIncomingRequest_DuplicatePersonAndComuna_DoesNotCreateSecondRow()
    {
        sut.ProcessIncomingRequest(NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7"), Contacts);
        sut.ProcessIncomingRequest(NewEmail("msg-2", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7"), Contacts);

        Assert.Single(repository.GetAll()); // no duplicate row in the tracking table / report
    }

    [Fact]
    public void ProcessUploadedCase_MatchingPending_MarksUploadedWithoutSending()
    {
        var id = InsertPending();

        sut.ProcessUploadedCase(NewEmail("msg-1", "irrelevante"));

        Assert.Empty(mailSender.SentMessages); // moving the email never sends anything by itself
        Assert.Equal(RequestStatus.Uploaded, repository.FindById(id)!.Status);
    }

    [Fact]
    public void ProcessUploadedCase_NoMatchingPending_IsNoOp()
    {
        sut.ProcessUploadedCase(NewEmail("unrelated-msg", "irrelevante"));

        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public async Task SendConfirmationAsync_UploadedCase_SendsAndMarksConfirmed()
    {
        var id = InsertPending();
        sut.ProcessUploadedCase(NewEmail("msg-1", "irrelevante"));

        var result = await sut.SendConfirmationAsync(id, confirmedByUserId: 1, Contacts, CancellationToken.None);

        Assert.True(result.Sent);
        Assert.Single(mailSender.SentMessages);
        Assert.Equal("rfloresc@municatemu.cl", mailSender.SentMessages[0].To);
        Assert.Equal(RequestStatus.Confirmed, repository.FindById(id)!.Status);
        Assert.Single(notificationChannel.Notified);
    }

    [Fact]
    public async Task SendConfirmationAsync_StillPending_RefusesToSend()
    {
        var id = InsertPending(); // never moved to CARP. YA PEDIDAS

        var result = await sut.SendConfirmationAsync(id, confirmedByUserId: 1, Contacts, CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Empty(mailSender.SentMessages);
        Assert.Equal(RequestStatus.Pending, repository.FindById(id)!.Status);
    }

    [Fact]
    public async Task SendConfirmationAsync_AlreadyConfirmed_DoesNotSendTwice()
    {
        var id = InsertPending();
        sut.ProcessUploadedCase(NewEmail("msg-1", "irrelevante"));
        await sut.SendConfirmationAsync(id, confirmedByUserId: 1, Contacts, CancellationToken.None);

        var second = await sut.SendConfirmationAsync(id, confirmedByUserId: 1, Contacts, CancellationToken.None);

        Assert.False(second.Sent);
        Assert.Single(mailSender.SentMessages); // only the first send happened
    }

    [Fact]
    public async Task SendConfirmationAsync_UnknownCase_ReturnsNotSent()
    {
        var result = await sut.SendConfirmationAsync(999, confirmedByUserId: 1, Contacts, CancellationToken.None);

        Assert.False(result.Sent);
    }

    private long InsertPending()
    {
        sut.ProcessIncomingRequest(NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7"), Contacts);
        return repository.GetAll().Single().Id;
    }

    private static IncomingEmail NewEmail(string messageId, string body, string sender = "rfloresc@municatemu.cl") =>
        new(messageId, "conv-1", "Solicitud de carpeta", sender, body, DateTimeOffset.UtcNow);

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

    private sealed class FakeNotificationChannel : INotificationChannel
    {
        public List<(string FullName, string Rut, string Comuna)> Notified { get; } = [];

        public void NotifyConfirmationSent(string fullName, string rut, string comuna) =>
            Notified.Add((fullName, rut, comuna));
    }
}
