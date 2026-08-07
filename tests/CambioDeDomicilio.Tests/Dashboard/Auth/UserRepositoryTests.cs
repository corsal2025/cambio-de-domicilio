using Microsoft.Data.Sqlite;
using CambioDeDomicilio.Dashboard.Auth;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Auth;

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
    public void UpdatePassword_ChangesHashSaltAndIterations()
    {
        var (hash, salt, iterations) = PasswordHasher.Hash("Cont2026#");
        repository.Insert(new DashboardUser { Username = "operador", PasswordHash = hash, PasswordSalt = salt, Iterations = iterations });
        var user = repository.FindByUsername("operador")!;

        var (newHash, newSalt, newIterations) = PasswordHasher.Hash("OtraClave2027#");
        repository.UpdatePassword(user.Id, newHash, newSalt, newIterations);

        var reloaded = repository.FindByUsername("operador")!;
        Assert.True(PasswordHasher.Verify("OtraClave2027#", reloaded.PasswordHash, reloaded.PasswordSalt, reloaded.Iterations));
        Assert.False(PasswordHasher.Verify("Cont2026#", reloaded.PasswordHash, reloaded.PasswordSalt, reloaded.Iterations));
    }

    [Fact]
    public void UpdateEmail_SetsEmailForRecovery()
    {
        var (hash, salt, iterations) = PasswordHasher.Hash("Cont2026#");
        repository.Insert(new DashboardUser { Username = "operador", PasswordHash = hash, PasswordSalt = salt, Iterations = iterations });
        var user = repository.FindByUsername("operador")!;
        Assert.Null(user.Email);

        repository.UpdateEmail(user.Id, "operador@munivalpo.cl");

        var reloaded = repository.FindByUsername("operador")!;
        Assert.Equal("operador@munivalpo.cl", reloaded.Email);
    }

    [Fact]
    public void FindById_ExistingUser_ReturnsUser()
    {
        var (hash, salt, iterations) = PasswordHasher.Hash("Cont2026#");
        repository.Insert(new DashboardUser { Username = "operador", PasswordHash = hash, PasswordSalt = salt, Iterations = iterations });
        var user = repository.FindByUsername("operador")!;

        var found = repository.FindById(user.Id);

        Assert.NotNull(found);
        Assert.Equal("operador", found!.Username);
    }

    [Fact]
    public void FindById_UnknownId_ReturnsNull()
    {
        Assert.Null(repository.FindById(999));
    }

    [Fact]
    public void UpdateEmailFooter_SetsFooterText()
    {
        var (hash, salt, iterations) = PasswordHasher.Hash("Cont2026#");
        repository.Insert(new DashboardUser { Username = "operador", PasswordHash = hash, PasswordSalt = salt, Iterations = iterations });
        var user = repository.FindByUsername("operador")!;
        Assert.Null(user.EmailFooter);

        repository.UpdateEmailFooter(user.Id, "María Pérez\nDepto. Licencias de Conducir\nMunicipalidad de Valparaíso");

        var reloaded = repository.FindByUsername("operador")!;
        Assert.Equal("María Pérez\nDepto. Licencias de Conducir\nMunicipalidad de Valparaíso", reloaded.EmailFooter);
    }

    [Fact]
    public void EnsureSchema_OnPreExistingTableWithoutEmailColumn_AddsColumnWithoutDataLoss()
    {
        // Simulates a database created before password-recovery-by-email existed.
        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                DROP TABLE DashboardUser;
                CREATE TABLE DashboardUser (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    PasswordSalt TEXT NOT NULL,
                    Iterations INTEGER NOT NULL,
                    FailedLoginAttempts INTEGER NOT NULL DEFAULT 0,
                    LockedUntil TEXT NULL,
                    CreatedAt TEXT NOT NULL
                );
                """;
            command.ExecuteNonQuery();
        }
        var (hash, salt, iterations) = PasswordHasher.Hash("Cont2026#");
        repository.Insert(new DashboardUser { Username = "operador", PasswordHash = hash, PasswordSalt = salt, Iterations = iterations });

        repository.EnsureSchema(); // re-run migration, as happens on every app startup

        var reloaded = repository.FindByUsername("operador");
        Assert.NotNull(reloaded);
        Assert.Null(reloaded!.Email);
        repository.UpdateEmail(reloaded.Id, "operador@munivalpo.cl");
        Assert.Equal("operador@munivalpo.cl", repository.FindByUsername("operador")!.Email);
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
