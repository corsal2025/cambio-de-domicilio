using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Directories;
using CambioDeDomicilio.Domain;

namespace CambioDeDomicilio.Dashboard.Pages;

public class ComunasModel(IComunaDirectory directory, RouterOptions options) : PageModel
{
    public IReadOnlyList<ComunaContact> Contacts { get; private set; } = [];
    public string? Message { get; set; }
    public bool MessageIsError { get; set; }

    public void OnGet() => Load();

    public IActionResult OnPostUpdateContact(string comuna, string domain, string newDomain, string email)
    {
        if (directory.UpdateContact(options.ComunaDirectoryCsvPath, comuna, domain, newDomain, email))
        {
            Message = $"Datos de {comuna} actualizados. Los próximos envíos usarán la nueva dirección.";
        }
        else
        {
            Message = $"No se pudo actualizar {comuna}: revise que el dominio y el correo tengan un formato válido.";
            MessageIsError = true;
        }

        Load();
        return Page();
    }

    public IActionResult OnPostDeleteContact(string comuna, string domain)
    {
        if (directory.DeleteContact(options.ComunaDirectoryCsvPath, comuna, domain))
        {
            Message = $"Comuna {comuna} ({domain}) eliminada del directorio.";
        }
        else
        {
            Message = $"No se pudo eliminar {comuna}: no se encontró en el directorio.";
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
