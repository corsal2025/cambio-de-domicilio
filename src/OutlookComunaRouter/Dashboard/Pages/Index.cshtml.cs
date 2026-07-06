using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Extraction;
using OutlookComunaRouter.Persistence;
using OutlookComunaRouter.Routing;

namespace OutlookComunaRouter.Dashboard.Pages;

[Authorize]
public class IndexModel(
    IPersonRequestRepository repository,
    IDiscardedEmailRepository discardedRepository,
    AddressChangeRoutingService routingService,
    RouterWorker routerWorker,
    RouterOptions options) : PageModel
{
    public IReadOnlyList<PersonRequest> Cases { get; private set; } = [];
    public int NeedsReviewCount { get; private set; }
    public int DiscardedCount { get; private set; }
    public string? StatusFilter { get; set; }
    public bool OnlyNeedsReview { get; set; }
    public string? Message { get; set; }
    public bool MessageIsError { get; set; }
    public int PlazoDiasHabiles => options.PlazoDiasHabiles;

    public void OnGet(string? status, bool needsReview = false)
    {
        StatusFilter = status;
        OnlyNeedsReview = needsReview;
        Load();
    }

    public IActionResult OnPostSetFecha(long id, string fecha)
    {
        if (!SpanishDate.TryParse(fecha, out var parsed))
        {
            Message = "Fecha no reconocida. Use el formato: día mes año — por ejemplo: 15 marzo 2024.";
            MessageIsError = true;
            Load();
            return Page();
        }

        repository.SetFechaUltimaCarpeta(id, parsed);
        return RedirectToPage(new { status = StatusFilter, needsReview = OnlyNeedsReview });
    }

    public IActionResult OnPostSetPersonData(long id, string nombre, string rut)
    {
        var normalizedRut = RutValidator.NormalizeAndValidate(rut);
        nombre = (nombre ?? string.Empty).Trim();

        if (normalizedRut is null || nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
        {
            Message = normalizedRut is null
                ? "El RUT ingresado no es válido (revise el dígito verificador)."
                : "Ingrese el nombre completo (al menos nombre y apellido).";
            MessageIsError = true;
            Load();
            return Page();
        }

        repository.SetPersonData(id, nombre, normalizedRut);
        Message = "Datos guardados. El caso ya no requiere revisión.";
        Load();
        return Page();
    }

    public async Task<IActionResult> OnPostSyncNowAsync()
    {
        var ran = await routerWorker.RunCycleAsync(HttpContext.RequestAborted);
        Message = ran
            ? "Sincronización completada."
            : "Ya hay una sincronización en curso, intente en unos segundos.";
        MessageIsError = !ran;
        Load();
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(long id)
    {
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var contacts = routingService.LoadDirectory();
        var result = await routingService.SendConfirmationAsync(id, userId, contacts, HttpContext.RequestAborted);

        Message = result.Reason;
        MessageIsError = !result.Sent;
        Load();
        return Page();
    }

    /// <summary>Business days remaining until the legal upload deadline for this case, from today.</summary>
    public int DiasHabilesRestantes(PersonRequest request)
    {
        var received = DateOnly.FromDateTime(request.ReceivedAt.LocalDateTime);
        var deadline = DeadlineCalculator.AddBusinessDays(received, options.PlazoDiasHabiles);
        return DeadlineCalculator.BusinessDaysRemaining(DateOnly.FromDateTime(DateTime.Today), deadline);
    }

    private void Load()
    {
        var everything = repository.GetAll();
        NeedsReviewCount = everything.Count(c => c.NeedsReview);
        DiscardedCount = discardedRepository.GetAll().Count;

        var all = everything.AsEnumerable();

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
