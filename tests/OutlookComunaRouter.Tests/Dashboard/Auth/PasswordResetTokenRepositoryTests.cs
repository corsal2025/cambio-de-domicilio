using Microsoft.Data.Sqlite;
using OutlookComunaRouter.Dashboard.Auth;
using Xunit;

namespace OutlookComunaRouter.Tests.Dashboard.Auth;

public class PasswordResetTokenRepositoryTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"reset-token-test-{Guid.NewGuid():N}.db");
    private readonly IPasswordResetTokenRepository repository;

    public PasswordResetTokenRepositoryTests()
    {
        repository = new PasswordResetTokenRepository($"Data Source={dbPath}");
        repository.EnsureSchema();
    }

    [Fact]
    public void Insert_FindByToken_ReturnsToken()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(30);
        repository.Insert(new PasswordResetToken { UserId = 1, Token = "abc123", ExpiresAt = expiresAt });

        var found = repository.FindByToken("abc123");

        Assert.NotNull(found);
        Assert.Equal(1, found!.UserId);
        Assert.Null(found.UsedAt);
    }

    [Fact]
    public void FindByToken_UnknownToken_ReturnsNull()
    {
        Assert.Null(repository.FindByToken("no-existe"));
    }

    [Fact]
    public void MarkUsed_SetsUsedAt()
    {
        repository.Insert(new PasswordResetToken { UserId = 1, Token = "abc123", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30) });

        repository.MarkUsed("abc123");

        Assert.NotNull(repository.FindByToken("abc123")!.UsedAt);
    }

    [Fact]
    public void InvalidateAllForUser_MarksOnlyThatUsersUnusedTokensAsUsed()
    {
        repository.Insert(new PasswordResetToken { UserId = 1, Token = "user1-token", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30) });
        repository.Insert(new PasswordResetToken { UserId = 2, Token = "user2-token", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30) });

        repository.InvalidateAllForUser(1);

        Assert.NotNull(repository.FindByToken("user1-token")!.UsedAt);
        Assert.Null(repository.FindByToken("user2-token")!.UsedAt);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
    }
}
