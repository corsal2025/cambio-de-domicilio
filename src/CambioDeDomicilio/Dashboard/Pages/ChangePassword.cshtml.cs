using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Dashboard.Auth;
using CambioDeDomicilio.Domain;

namespace CambioDeDomicilio.Dashboard.Pages;

public class ChangePasswordModel(IUserRepository users) : PageModel
{
    private const int MinPasswordLength = 8;

    [BindProperty]
    public string CurrentPassword { get; set; } = string.Empty;

    [BindProperty]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string EmailFooter { get; set; } = string.Empty;

    public string? Message { get; set; }
    public bool MessageIsError { get; set; }

    public void OnGet()
    {
        var user = users.FindByUsername(User.Identity!.Name!);
        Email = user?.Email ?? string.Empty;
        EmailFooter = user?.EmailFooter ?? string.Empty;
    }

    public IActionResult OnPostUpdateEmail()
    {
        var user = users.FindByUsername(User.Identity!.Name!);
        if (user is null || !EmailShapeValidator.IsValidEmailShape(Email))
        {
            Message = "El correo ingresado no es válido.";
            MessageIsError = true;
            return Page();
        }

        users.UpdateEmail(user.Id, Email);
        Message = "Correo de recuperación actualizado.";
        MessageIsError = false;
        return Page();
    }

    public IActionResult OnPostUpdateEmailFooter()
    {
        var user = users.FindByUsername(User.Identity!.Name!);
        if (user is null)
        {
            Message = "No se pudo guardar el pie de correo.";
            MessageIsError = true;
            return Page();
        }

        users.UpdateEmailFooter(user.Id, EmailFooter);
        Message = "Pie de correo actualizado.";
        MessageIsError = false;
        return Page();
    }


    public IActionResult OnPost()
    {
        var username = User.Identity!.Name!;
        var user = users.FindByUsername(username);

        if (user is null || !PasswordHasher.Verify(CurrentPassword, user.PasswordHash, user.PasswordSalt, user.Iterations))
        {
            Message = "La contraseña actual no es correcta.";
            MessageIsError = true;
            return Page();
        }

        if (NewPassword.Length < MinPasswordLength)
        {
            Message = $"La nueva contraseña debe tener al menos {MinPasswordLength} caracteres.";
            MessageIsError = true;
            return Page();
        }

        if (NewPassword != ConfirmPassword)
        {
            Message = "La confirmación no coincide con la nueva contraseña.";
            MessageIsError = true;
            return Page();
        }

        var (hash, salt, iterations) = PasswordHasher.Hash(NewPassword);
        users.UpdatePassword(user.Id, hash, salt, iterations);

        Message = "Contraseña actualizada correctamente.";
        MessageIsError = false;
        return Page();
    }
}
