using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;

namespace CambioDeDomicilio.Dashboard.Pages;

public class SinCarpetasModel(IPersonRequestRepository repository) : PageModel
{
    public IReadOnlyList<PersonRequest> Cases { get; private set; } = [];
    public string? Message { get; set; }
    public bool MessageIsError { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public int TotalCount { get; private set; }

    public void OnGet(string? search = null)
    {
        Search = search;
        Message ??= TempData?["Message"] as string;
        if (TempData?["MessageIsError"] is bool isErr) MessageIsError = isErr;
        Load();
    }

    public IActionResult OnPostRevert(long id)
    {
        repository.ReopenSinCarpetaToF8(id, DateTimeOffset.UtcNow);
        Message = "Caso restituido a la sección F8 con éxito.";
        return RedirectToPage(new { search = Search });
    }

    private void Load()
    {
        var sinCarpetaCases = repository.Find(new CaseQuery { InSinCarpetasBucket = true })
            .OrderByDescending(c => c.ClosedWithoutFolderAt ?? c.TransferredAt ?? c.ReceivedAt)
            .ToList();

        TotalCount = sinCarpetaCases.Count;

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var query = Search.Trim().ToUpperInvariant();
            Cases = sinCarpetaCases.Where(c => MatchesQuery(c, query)).ToList();
        }
        else
        {
            Cases = sinCarpetaCases;
        }
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
