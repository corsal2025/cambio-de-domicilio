using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;

namespace CambioDeDomicilio.Dashboard.Pages;

/// <summary>
/// "Última Carpeta": the F8 cases whose last folder has to be requested. From here the operator marks the
/// request, sends the case to Caja when the folder arrives, or closes it "Sin carpeta" when it does not exist.
/// None of these actions sends an email.
/// </summary>
public class UltimaCarpetaModel(IPersonRequestRepository repository) : PageModel
{
    public IReadOnlyList<PersonRequest> Cases { get; private set; } = [];
    public string? Message { get; set; }
    public bool MessageIsError { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    /// <summary>Every case in the module, whatever the search filter says.</summary>
    public int TotalCount { get; private set; }

    /// <summary>Cases whose last folder has not been requested yet.</summary>
    public int PendingCount { get; private set; }

    public void OnGet(string? search = null)
    {
        Search = search;
        Message ??= TempData?["Message"] as string;
        if (TempData?["MessageIsError"] is bool isError) MessageIsError = isError;
        Load();
    }

    /// <summary>"Pedida": records that the last folder of the case was requested.</summary>
    public IActionResult OnPostMarkRequested(long id)
    {
        repository.SetUltimaCarpetaRequested(id, DateTimeOffset.UtcNow);
        return RedirectToPage(new { search = Search });
    }

    public IActionResult OnPostUndoRequested(long id)
    {
        repository.SetUltimaCarpetaRequested(id, null);
        return RedirectToPage(new { search = Search });
    }

    /// <summary>The folder arrived: the case goes to the Caja open queue (no email).</summary>
    public IActionResult OnPostSendToCaja(long id)
    {
        var request = repository.FindById(id);
        if (request?.Destination == CaseDestination.UltimaCarpeta)
        {
            repository.SendToCaja([id], DateTimeOffset.UtcNow);
            SetFlash($"La carpeta de {request.FullName} se envió a la cola de Caja.", isError: false);
        }

        return RedirectToPage(new { search = Search });
    }

    /// <summary>The folder does not exist: the case closes in Sin Carpetas, where the process ends.</summary>
    public IActionResult OnPostCloseWithoutFolder(long id)
    {
        var request = repository.FindById(id);
        if (request?.Destination == CaseDestination.UltimaCarpeta)
        {
            repository.CloseWithoutFolder(id, DateTimeOffset.UtcNow);
            SetFlash($"Caso de {request.FullName} cerrado sin carpeta física y trasladado a Sin Carpetas.", isError: false);
            return RedirectToPage("/SinCarpetas");
        }

        return RedirectToPage(new { search = Search });
    }

    private void SetFlash(string message, bool isError)
    {
        if (TempData is null) return;
        TempData["Message"] = message;
        TempData["MessageIsError"] = isError;
    }

    private void Load()
    {
        var all = repository.Find(new CaseQuery { Destinations = [CaseDestination.UltimaCarpeta] })
            .Where(c => c.ClosedWithoutFolderAt is null && !c.SinCarpeta)
            .OrderBy(c => c.TransferredAt ?? c.ReceivedAt)
            .ThenBy(c => c.Id)
            .ToList();

        TotalCount = all.Count;
        PendingCount = all.Count(c => c.UltimaCarpetaRequestedAt is null);

        Cases = string.IsNullOrWhiteSpace(Search)
            ? all
            : all.Where(c => MatchesQuery(c, Search.Trim().ToUpperInvariant())).ToList();
    }

    private static bool MatchesQuery(PersonRequest c, string query)
    {
        var queryClean = query.Replace(".", string.Empty).Replace("-", string.Empty);
        var rutClean = (c.Rut ?? string.Empty).Replace(".", string.Empty).Replace("-", string.Empty);
        return (c.FullName ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase) ||
               rutClean.Contains(queryClean, StringComparison.OrdinalIgnoreCase) ||
               (c.CodigoF8 ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase) ||
               (c.Comuna ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
