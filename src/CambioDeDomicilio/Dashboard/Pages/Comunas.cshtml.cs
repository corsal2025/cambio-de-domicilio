using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Directories;
using CambioDeDomicilio.Domain;

namespace CambioDeDomicilio.Dashboard.Pages;

[Authorize]
public class ComunasModel(IComunaDirectory directory, RouterOptions options) : PageModel
{
    public IReadOnlyList<ComunaContact> Contacts { get; private set; } = [];
    public string? Message { get; set; }
    public bool MessageIsError { get; set; }

    public void OnGet() => Load();

    public IActionResult OnPostUpdateEmail(string comuna, string email)
    {
        if (directory.UpdateContactEmail(options.ComunaDirectoryCsvPath, comuna, email))
        {
            Message = $"Correo de {comuna} actualizado. Los próximos envíos usarán la nueva dirección.";
        }
        else
        {
            Message = $"No se pudo actualizar {comuna}: revise que el correo tenga un formato válido.";
            MessageIsError = true;
        }

        Load();
        return Page();
    }

    public IActionResult OnPostAddContact(string comuna, string domain, string email)
    {
        if (directory.AddContact(options.ComunaDirectoryCsvPath, comuna, email, domain))
        {
            Message = $"Comuna {comuna} agregada al directorio.";
        }
        else
        {
            Message = "No se pudo agregar: revise que la comuna, el dominio y el correo tengan un formato válido.";
            MessageIsError = true;
        }

        Load();
        return Page();
    }

    private void Load() =>
        Contacts = directory.LoadFromCsv(options.ComunaDirectoryCsvPath)
            .OrderBy(c => c.Comuna)
            .ToList();
}
