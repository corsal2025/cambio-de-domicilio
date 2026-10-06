using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;

namespace CambioDeDomicilio.Dashboard.Pages;

/// <summary>Terminal, read-only list: a case closed without folder (penultimate folder not found)
/// stays here with no further actions.</summary>
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

    private void Load()
    {
        var all = repository.GetAll();
        var sinCarpetaCases = all
            .Where(c => c.Destination == CaseDestination.SinCarpetas || c.ClosedWithoutFolderAt is not null || c.SinCarpeta)
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
