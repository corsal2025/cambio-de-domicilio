using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Routing;

namespace CambioDeDomicilio.Dashboard.Pages;

public class SubidasASistemaModel(IPersonRequestRepository repository, AddressChangeRoutingService routingService) : PageModel
{
    public IReadOnlyList<PersonRequest> Cases { get; private set; } = [];

    [TempData]
    public string? Message { get; set; }

    [TempData]
    public bool MessageIsError { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public int TotalCount { get; private set; }

    public void OnGet(string? search = null)
    {
        Search = search;
        Load();
    }

    public IActionResult OnPostSendToCaja(long id)
    {
        var request = repository.FindById(id);
        if (request is null)
        {
            Message = "El caso no existe.";
            MessageIsError = true;
            return RedirectToPage(new { search = Search });
        }

        repository.SendToCaja([id], DateTimeOffset.UtcNow);
        Message = $"La carpeta de {request.FullName} se envió a la cola de Caja con éxito.";
        MessageIsError = false;
        return RedirectToPage(new { search = Search });
    }

    public IActionResult OnPostCloseWithoutFolder(long id)
    {
        var request = repository.FindById(id);
        if (request is null)
        {
            Message = "El caso no existe.";
            MessageIsError = true;
            return RedirectToPage(new { search = Search });
        }

        repository.CloseWithoutFolder(id, DateTimeOffset.UtcNow);
        if (TempData is not null)
        {
            TempData["Message"] = $"El caso de {request.FullName} se cerró sin carpeta física y se trasladó a Sin Carpetas.";
            TempData["MessageIsError"] = false;
        }
        return RedirectToPage("/SinCarpetas");
    }

    public async Task<IActionResult> OnPostRectifyConfirmationAsync(long id)
    {
        var contacts = routingService.LoadDirectory();
        var result = await routingService.RectifyConfirmationAsync(id, contacts, HttpContext.RequestAborted);

        Message = result.Reason;
        MessageIsError = !result.Sent;
        return RedirectToPage(new { search = Search });
    }

    private void Load()
    {
        // Transferred to Subidas, plus uploaded/confirmed cases still sitting in Casos. Two SQL slices,
        // merged back into Id order so ties sort exactly as they did when everything came from GetAll().
        var subidas = repository.Find(new CaseQuery { Destinations = [CaseDestination.Subidas] })
            .Concat(repository.Find(new CaseQuery
            {
                Destinations = [CaseDestination.None],
                Statuses = [RequestStatus.Uploaded, RequestStatus.Confirmed]
            }))
            .DistinctBy(c => c.Id) // the two slices are read at different instants; a case moved in between must not show twice
            .OrderBy(c => c.Id)
            .Where(c => c.ClosedWithoutFolderAt is null && !c.SinCarpeta)
            .OrderByDescending(c => c.ConfirmedAt ?? c.UploadedAt ?? c.ReceivedAt)
            .ToList();

        TotalCount = subidas.Count;

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var query = Search.Trim().ToUpperInvariant();
            Cases = subidas.Where(c => MatchesQuery(c, query)).ToList();
        }
        else
        {
            Cases = subidas;
        }
    }

    private static bool MatchesQuery(PersonRequest c, string query)
    {
        var queryClean = query.Replace(".", string.Empty).Replace("-", string.Empty);
        var rutClean = (c.Rut ?? string.Empty).Replace(".", string.Empty).Replace("-", string.Empty);
        return (c.FullName ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase) ||
               rutClean.Contains(queryClean, StringComparison.OrdinalIgnoreCase) ||
               (c.Comuna ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
