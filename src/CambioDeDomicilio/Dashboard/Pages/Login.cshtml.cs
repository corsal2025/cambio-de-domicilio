using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Dashboard.Auth;

namespace CambioDeDomicilio.Dashboard.Pages;

public class LoginModel(ILoginService loginService, IUserRepository users) : PageModel
{
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var outcome = await loginService.TryLoginAsync(Username, Password);

        if (outcome != LoginOutcome.Success)
        {
            ErrorMessage = outcome == LoginOutcome.LockedOut
                ? "Cuenta bloqueada temporalmente por intentos fallidos. Intente más tarde."
                : "Usuario o contraseña incorrectos.";
            return Page();
        }

        var user = users.FindByUsername(Username)!;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties { IsPersistent = true };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), authProperties);

        return RedirectToPage("/Index");
    }
}
