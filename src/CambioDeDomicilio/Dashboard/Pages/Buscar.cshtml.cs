using CambioDeDomicilio.Routing;
using CambioDeDomicilio.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CambioDeDomicilio.Dashboard.Pages;

/// <summary>Result page of the header search box: one query over every module of the system.</summary>
public class BuscarModel(GlobalSearchService search, AddressChangeRoutingService routing) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "search")]
    public string? Query { get; set; }

    public GlobalSearchResult Result { get; private set; } = new(string.Empty, []);

    public bool QueryTooShort =>
        !string.IsNullOrWhiteSpace(Query) && Query.Trim().Length < GlobalSearchService.MinQueryLength;

    public void OnGet()
    {
        Result = search.Search(Query, routing.LoadDirectory());
    }
}
