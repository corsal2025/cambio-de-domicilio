using Microsoft.Data.Sqlite;
using CambioDeDomicilio.Dashboard.Auth;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Auth;

public class LoginServiceTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"login-test-{Guid.NewGuid():N}.db");
    private readonly IUserRepository repository;
    private readonly ILoginService sut;

    public LoginServiceTests()
    {
        repository = new UserRepository($"Data Source={dbPath}");
        repository.EnsureSchema();
        sut = new LoginService(repository);

        var (hash, salt, iterations) = PasswordHasher.Hash("Cont2026#");
        repository.Insert(new DashboardUser { Username = "operador", PasswordHash = hash, PasswordSalt = salt, Iterations = iterations });
    }

    [Fact]
    public async Task TryLoginAsync_CorrectPassword_Succeeds()
    {
        var outcome = await sut.TryLoginAsync("operador", "Cont2026#");

        Assert.Equal(LoginOutcome.Success, outcome);
    }

    [Fact]
    public async Task TryLoginAsync_UnknownUser_ReturnsInvalidCredentials()
    {
        var outcome = await sut.TryLoginAsync("no-existe", "cualquiera");

        Assert.Equal(LoginOutcome.InvalidCredentials, outcome);
    }

    [Fact]
    public async Task TryLoginAsync_WrongPasswordFiveTimes_LocksAccount()
    {
        for (var i = 0; i < 4; i++)
        {
            var outcome = await sut.TryLoginAsync("operador", "clave-incorrecta");
            Assert.Equal(LoginOutcome.InvalidCredentials, outcome);
        }

        var fifthOutcome = await sut.TryLoginAsync("operador", "clave-incorrecta");
        Assert.Equal(LoginOutcome.LockedOut, fifthOutcome);

        // Even the correct password fails while locked.
        var attemptWithCorrectPassword = await sut.TryLoginAsync("operador", "Cont2026#");
        Assert.Equal(LoginOutcome.LockedOut, attemptWithCorrectPassword);
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
