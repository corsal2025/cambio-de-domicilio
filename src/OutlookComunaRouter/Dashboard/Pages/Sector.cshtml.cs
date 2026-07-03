using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Persistence;

namespace OutlookComunaRouter.Dashboard.Pages;

[Authorize]
public class SectorModel(IPersonRequestRepository repository) : PageModel
{
    public FolderSector SelectedSector { get; private set; }
    public IReadOnlyList<PersonRequest> Cases { get; private set; } = [];

    public void OnGet(FolderSector sector)
    {
        SelectedSector = sector;
        Cases = repository.GetAll()
            .Where(c => c.Sector == sector)
            .OrderBy(c => c.FullName)
            .ToList();
    }
}
