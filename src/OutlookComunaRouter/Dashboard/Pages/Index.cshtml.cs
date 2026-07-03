using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Persistence;
using OutlookComunaRouter.Routing;

namespace OutlookComunaRouter.Dashboard.Pages;

[Authorize]
public class IndexModel(IPersonRequestRepository repository, AddressChangeRoutingService routingService) : PageModel
{
    public IReadOnlyList<PersonRequest> Cases { get; private set; } = [];
    public string? StatusFilter { get; set; }
    public bool OnlyNeedsReview { get; set; }
    public string? Message { get; set; }

    public void OnGet(string? status, bool needsReview = false)
    {
        StatusFilter = status;
        OnlyNeedsReview = needsReview;
        Load();
    }

    public IActionResult OnPostSetFecha(long id, DateOnly fecha)
    {
        repository.SetFechaUltimaCarpeta(id, fecha);
        return RedirectToPage(new { status = StatusFilter, needsReview = OnlyNeedsReview });
    }

    public async Task<IActionResult> OnPostConfirmAsync(long id)
    {
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var contacts = routingService.LoadDirectory();
        var result = await routingService.SendConfirmationAsync(id, userId, contacts, HttpContext.RequestAborted);

        Message = result.Reason;
        Load();
        return Page();
    }

    private void Load()
    {
        var all = repository.GetAll().AsEnumerable();

        if (!string.IsNullOrEmpty(StatusFilter) && Enum.TryParse<RequestStatus>(StatusFilter, out var status))
        {
            all = all.Where(c => c.Status == status);
        }

        if (OnlyNeedsReview)
        {
            all = all.Where(c => c.NeedsReview);
        }

        Cases = all.OrderByDescending(c => c.CreatedAt).ToList();
    }
}
