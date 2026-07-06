using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Persistence;

namespace OutlookComunaRouter.Dashboard.Pages;

[Authorize]
public class DiscardedModel(IDiscardedEmailRepository repository) : PageModel
{
    public IReadOnlyList<DiscardedEmail> Items { get; private set; } = [];

    public void OnGet() => Items = repository.GetAll();
}
