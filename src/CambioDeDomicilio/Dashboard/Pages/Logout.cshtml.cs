using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CambioDeDomicilio.Dashboard.Pages;

public class LogoutModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");
    public IActionResult OnPost() => RedirectToPage("/Index");
}
