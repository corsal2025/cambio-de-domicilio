using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Persistence;

namespace OutlookComunaRouter.Dashboard.Pages;

/// <summary>F8 counterpart of <see cref="SectorModel"/> — same sector-document mechanics (Marcar
/// checkbox selects which cases print, SectorPdfGeneratedAt excludes already-printed ones), but
/// scoped to cases already transferred to F8 (<see cref="PersonRequest.Destination"/>) instead of
/// the main Casos list. The date shown/printed is the same <see cref="PersonRequest.FechaUltimaCarpeta"/>
/// field, labeled "penúltima carpeta" here to match the F8 screen's terminology.</summary>
[Authorize]
public class SectorF8Model(IPersonRequestRepository repository) : PageModel
{
    public FolderSector SelectedSector { get; private set; }
    public IReadOnlyList<PersonRequest> Cases { get; private set; } = [];

    public void OnGet(FolderSector sector)
    {
        SelectedSector = sector;
        Cases = repository.GetAll()
            .Where(c => c.Destination == CaseDestination.F8 && c.Sector == sector && (c.Marked || c.PendienteCarpeta) && c.SectorPdfGeneratedAt is null)
            .OrderBy(c => c.FullName)
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

    private void MarkAllVisibleAsPrinted(FolderSector sector)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in repository.GetAll().Where(c => c.Destination == CaseDestination.F8 && c.Sector == sector && (c.Marked || c.PendienteCarpeta) && c.SectorPdfGeneratedAt is null))
        {
            repository.SetSectorPdfGenerated(item.Id, now);
        }
    }
}
