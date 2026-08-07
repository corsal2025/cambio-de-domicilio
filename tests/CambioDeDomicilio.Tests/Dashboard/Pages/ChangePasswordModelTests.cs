using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using CambioDeDomicilio.Dashboard.Auth;
using CambioDeDomicilio.Dashboard.Pages;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Pages;

public class ChangePasswordModelTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"change-password-test-{Guid.NewGuid():N}.db");
    private readonly IUserRepository repository;
    private readonly ChangePasswordModel model;

    public ChangePasswordModelTests()
    {
        repository = new UserRepository($"Data Source={dbPath}");
        repository.EnsureSchema();

        var (hash, salt, iterations) = PasswordHasher.Hash("Cont2026#");
        repository.Insert(new DashboardUser { Username = "operador", PasswordHash = hash, PasswordSalt = salt, Iterations = iterations });

        model = new ChangePasswordModel(repository)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Name, "operador")]))
                }
            }
        };
    }

    [Fact]
    public void OnPost_CorrectCurrentPasswordAndValidNew_UpdatesHash()
    {
        model.CurrentPassword = "Cont2026#";
        model.NewPassword = "NuevaClave2027#";
        model.ConfirmPassword = "NuevaClave2027#";

        model.OnPost();

        var user = repository.FindByUsername("operador")!;
        Assert.True(PasswordHasher.Verify("NuevaClave2027#", user.PasswordHash, user.PasswordSalt, user.Iterations));
        Assert.False(model.MessageIsError);
    }

    [Fact]
    public void OnPost_WrongCurrentPassword_RejectsAndKeepsOldHash()
    {
        model.CurrentPassword = "clave-incorrecta";
        model.NewPassword = "NuevaClave2027#";
        model.ConfirmPassword = "NuevaClave2027#";

        model.OnPost();

        var user = repository.FindByUsername("operador")!;
        Assert.True(PasswordHasher.Verify("Cont2026#", user.PasswordHash, user.PasswordSalt, user.Iterations));
        Assert.True(model.MessageIsError);
    }

    [Fact]
    public void OnPost_NewPasswordDoesNotMatchConfirmation_Rejects()
    {
        model.CurrentPassword = "Cont2026#";
        model.NewPassword = "NuevaClave2027#";
        model.ConfirmPassword = "OtraCosa#";

        model.OnPost();

        var user = repository.FindByUsername("operador")!;
        Assert.True(PasswordHasher.Verify("Cont2026#", user.PasswordHash, user.PasswordSalt, user.Iterations));
        Assert.True(model.MessageIsError);
    }

    [Fact]
    public void OnPost_NewPasswordTooShort_Rejects()
    {
        model.CurrentPassword = "Cont2026#";
        model.NewPassword = "corta1";
        model.ConfirmPassword = "corta1";

        model.OnPost();

        var user = repository.FindByUsername("operador")!;
        Assert.True(PasswordHasher.Verify("Cont2026#", user.PasswordHash, user.PasswordSalt, user.Iterations));
        Assert.True(model.MessageIsError);
    }

    [Fact]
    public void OnGet_LoadsCurrentEmail()
    {
        repository.UpdateEmail(repository.FindByUsername("operador")!.Id, "operador@munivalpo.cl");

        model.OnGet();

        Assert.Equal("operador@munivalpo.cl", model.Email);
    }

    [Fact]
    public void OnPostUpdateEmail_ValidEmail_SavesIt()
    {
        model.Email = "operador@munivalpo.cl";

        model.OnPostUpdateEmail();

        Assert.Equal("operador@munivalpo.cl", repository.FindByUsername("operador")!.Email);
        Assert.False(model.MessageIsError);
    }

    [Fact]
    public void OnPostUpdateEmail_InvalidShape_IsRejected()
    {
        model.Email = "no-es-un-correo";

        model.OnPostUpdateEmail();

        Assert.Null(repository.FindByUsername("operador")!.Email);
        Assert.True(model.MessageIsError);
    }

    [Fact]
    public void OnGet_LoadsCurrentEmailFooter()
    {
        repository.UpdateEmailFooter(repository.FindByUsername("operador")!.Id, "María Pérez\nDepto. Licencias");

        model.OnGet();

        Assert.Equal("María Pérez\nDepto. Licencias", model.EmailFooter);
    }

    [Fact]
    public void OnPostUpdateEmailFooter_SavesIt()
    {
        model.EmailFooter = "María Pérez\nDepto. Licencias de Conducir";

        model.OnPostUpdateEmailFooter();

        Assert.Equal("María Pérez\nDepto. Licencias de Conducir", repository.FindByUsername("operador")!.EmailFooter);
        Assert.False(model.MessageIsError);
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
