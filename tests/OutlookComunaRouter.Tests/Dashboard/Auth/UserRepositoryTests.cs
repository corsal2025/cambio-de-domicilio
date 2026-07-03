using Microsoft.Data.Sqlite;
using OutlookComunaRouter.Dashboard.Auth;
using Xunit;

namespace OutlookComunaRouter.Tests.Dashboard.Auth;

public class UserRepositoryTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"users-test-{Guid.NewGuid():N}.db");
    private readonly IUserRepository repository;

    public UserRepositoryTests()
    {
        repository = new UserRepository($"Data Source={dbPath}");
        repository.EnsureSchema();
    }

    [Fact]
    public void Insert_FindByUsername_ReturnsUser()
    {
        var (hash, salt, iterations) = PasswordHasher.Hash("Cont2026#");
        repository.Insert(new DashboardUser { Username = "operador", PasswordHash = hash, PasswordSalt = salt, Iterations = iterations });

        var found = repository.FindByUsername("operador");

        Assert.NotNull(found);
        Assert.Equal(0, found!.FailedLoginAttempts);
    }

    [Fact]
    public void RecordFailedLogin_IncrementsAttemptsAndCanLock()
    {
        var (hash, salt, iterations) = PasswordHasher.Hash("Cont2026#");
        repository.Insert(new DashboardUser { Username = "operador", PasswordHash = hash, PasswordSalt = salt, Iterations = iterations });
        var user = repository.FindByUsername("operador")!;

        var lockUntil = DateTimeOffset.UtcNow.AddMinutes(15);
        repository.RecordFailedLogin(user.Id, 5, lockUntil);

        var reloaded = repository.FindByUsername("operador")!;
        Assert.Equal(5, reloaded.FailedLoginAttempts);
        Assert.NotNull(reloaded.LockedUntil);
    }

    [Fact]
    public void ResetFailedLogins_ClearsCounterAndLock()
    {
        var (hash, salt, iterations) = PasswordHasher.Hash("Cont2026#");
        repository.Insert(new DashboardUser { Username = "operador", PasswordHash = hash, PasswordSalt = salt, Iterations = iterations });
        var user = repository.FindByUsername("operador")!;
        repository.RecordFailedLogin(user.Id, 5, DateTimeOffset.UtcNow.AddMinutes(15));

        repository.ResetFailedLogins(user.Id);

        var reloaded = repository.FindByUsername("operador")!;
        Assert.Equal(0, reloaded.FailedLoginAttempts);
        Assert.Null(reloaded.LockedUntil);
    }

    [Fact]
    public void Delete_RemovesUser()
    {
        var (hash, salt, iterations) = PasswordHasher.Hash("Cont2026#");
        repository.Insert(new DashboardUser { Username = "operador", PasswordHash = hash, PasswordSalt = salt, Iterations = iterations });

        repository.Delete("operador");

        Assert.Null(repository.FindByUsername("operador"));
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
