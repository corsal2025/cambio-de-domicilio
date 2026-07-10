using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Persistence;

namespace OutlookComunaRouter.Dashboard.Pages;

[Authorize]
public class DiscardedModel(IDiscardedEmailRepository repository) : PageModel
{
    public IReadOnlyList<DiscardedEmail> Items { get; private set; } = [];

    public void OnGet() => Items = repository.GetAll();

    /// <summary>Operator-triggered permanent removal of a single discarded record — for entries
    /// that will never be resolved (e.g. spam, or a comuna that will never be registered).</summary>
    public IActionResult OnPostDelete(long id)
    {
        repository.Delete(id);
        return RedirectToPage();
    }

    /// <summary>Operator-triggered bulk cleanup of every discarded record currently listed.</summary>
    public IActionResult OnPostDeleteAll()
    {
        foreach (var item in repository.GetAll())
        {
            repository.Delete(item.Id);
        }

        return RedirectToPage();
    }
}
