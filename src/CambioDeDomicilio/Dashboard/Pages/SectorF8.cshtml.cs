using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;

namespace CambioDeDomicilio.Dashboard.Pages;

/// <summary>F8 counterpart of <see cref="SectorModel"/> — same sector-document mechanics (Marcar
/// checkbox selects which cases print, SectorPdfGeneratedAt excludes already-printed ones), but
/// scoped to cases already transferred to F8 (<see cref="PersonRequest.Destination"/>) instead of
/// the main Casos list. The date shown/printed is the same <see cref="PersonRequest.FechaUltimaCarpeta"/>
/// field, labeled "penúltima carpeta" here to match the F8 screen's terminology.</summary>
public class SectorF8Model(IPersonRequestRepository repository) : PageModel
{
    public FolderSector SelectedSector { get; private set; }
    public IReadOnlyList<PersonRequest> Cases { get; private set; } = [];

    public void OnGet(FolderSector sector)
    {
        SelectedSector = sector;
        // Ordered by MarkedAt (the order the operator ticked "Marcar" in), matching Sector.cshtml.cs.
        Cases = PendingForPrint(sector)
            .OrderBy(c => c.MarkedAt)
            .ToList();
    }

    public IActionResult OnPostMarkPrinted(FolderSector sector)
    {
        MarkAllVisibleAsPrinted(sector);
        return new EmptyResult();
    }

    public IActionResult OnPostRemoveOne(long id, FolderSector sector)
    {
        repository.SetSectorPdfGenerated(id, DateTimeOffset.UtcNow);
        return RedirectToPage(new { sector });
    }

    public IActionResult OnPostClearAll(FolderSector sector)
    {
        MarkAllVisibleAsPrinted(sector);
        return RedirectToPage(new { sector });
    }

    /// <summary>In F8, in this sector, marked or pending retrieval, and not yet printed.</summary>
    private IEnumerable<PersonRequest> PendingForPrint(FolderSector sector) =>
        repository.Find(new CaseQuery { Destinations = [CaseDestination.F8] })
            .Where(c => c.Sector == sector && (c.Marked || c.PendienteCarpeta) && c.SectorPdfGeneratedAt is null);

    private void MarkAllVisibleAsPrinted(FolderSector sector)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in PendingForPrint(sector))
        {
            repository.SetSectorPdfGenerated(item.Id, now);
        }
    }
}
