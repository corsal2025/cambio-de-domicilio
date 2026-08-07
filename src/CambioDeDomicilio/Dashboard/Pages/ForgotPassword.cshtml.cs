using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Dashboard.Auth;

namespace CambioDeDomicilio.Dashboard.Pages;

public class ForgotPasswordModel(IPasswordResetService passwordResetService) : PageModel
{
    /// <summary>Shown for every outcome — never reveals whether the username exists or has an
    /// email on file, so the form can't be used to enumerate accounts.</summary>
    public const string GenericMessage = "Si el usuario existe y tiene un correo de recuperación registrado, se envió un enlace para restablecer la contraseña.";

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    public string? Message { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await passwordResetService.RequestResetAsync(Username, HttpContext.RequestAborted);
        Message = GenericMessage;
        return Page();
    }
}
