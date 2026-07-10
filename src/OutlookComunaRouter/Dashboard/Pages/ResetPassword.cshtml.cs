using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OutlookComunaRouter.Dashboard.Auth;

namespace OutlookComunaRouter.Dashboard.Pages;

public class ResetPasswordModel(IPasswordResetService passwordResetService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = string.Empty;

    [BindProperty]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? Message { get; set; }
    public bool MessageIsError { get; set; }
    public bool Completed { get; set; }

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        if (NewPassword != ConfirmPassword)
        {
            Message = "La confirmación no coincide con la nueva contraseña.";
            MessageIsError = true;
            return Page();
        }

        var outcome = passwordResetService.CompleteReset(Token, NewPassword);
        (Message, MessageIsError, Completed) = outcome switch
        {
            PasswordResetCompletionOutcome.Success => ("Contraseña actualizada. Ya puedes iniciar sesión.", false, true),
            PasswordResetCompletionOutcome.PasswordTooShort => ("La nueva contraseña debe tener al menos 8 caracteres.", true, false),
            _ => ("El enlace no es válido o ya expiró. Solicita uno nuevo desde '¿Olvidaste tu contraseña?'.", true, false)
        };

        return Page();
    }
}
