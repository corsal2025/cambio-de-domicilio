using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Extraction;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Routing;

namespace CambioDeDomicilio.Dashboard.Pages;

/// <summary>Lists only F8-marked cases (<see cref="PersonRequest.FolderNotFound"/>) with the same
/// row interactions as the main Casos table (Index page) — a dedicated, filtered view so the
/// operator doesn't have to hunt F8 cases inside the full list. This page is self-contained (does
/// not inherit from IndexModel, following this project's one-page-one-model convention).</summary>
public class F8Model(
    IPersonRequestRepository repository,
    AddressChangeRoutingService routingService,
    RouterOptions options) : PageModel
{
    public IReadOnlyList<PersonRequest> Cases { get; private set; } = [];
    public string? Message { get; set; }
    public bool MessageIsError { get; set; }
        [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? HighlightId { get; set; }

    public int TotalF8Count { get; private set; }

    /// <summary>Number of cases still in Casos (not transferred) matching the search query.</summary>
    public int CasosMatchCount { get; private set; }

    public int PlazoDiasHabiles => options.PlazoDiasHabiles;

    public void OnGet(string? search = null, long? highlightId = null)
    {
        Search = search;
        HighlightId = highlightId;
        Load();
    }

    public IActionResult OnPostSetFecha(long id, string fecha)
    {
        var isAjax = string.Equals(HttpContext?.Request?.Headers?.XRequestedWith.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                     || (HttpContext?.Request?.Headers?.Accept.ToString()?.Contains("application/json") ?? false);

        if (string.IsNullOrWhiteSpace(fecha))
        {
            repository.ClearFechaUltimaCarpeta(id);
            if (isAjax)
            {
                return new JsonResult(new { success = true, fecha = "", sector = "—" });
            }
            return RedirectToPage(new { search = Search, highlightId = HighlightId });
        }

        if (fecha.Trim().Equals("S/C", StringComparison.OrdinalIgnoreCase))
        {
            repository.SetSinCarpeta(id);
            if (isAjax)
            {
                return new JsonResult(new { success = true, fecha = "S/C", sector = "—" });
            }
            return RedirectToPage(new { search = Search, highlightId = HighlightId });
        }

        if (!DateOnly.TryParseExact(fecha.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed)
            && !SpanishDate.TryParse(fecha, out parsed))
        {
            if (isAjax)
            {
                return new JsonResult(new { success = false, message = "Fecha no válida. Ingrese una fecha válida (ej: dd/mm/aaaa)." }) { StatusCode = 400 };
            }
            Message = "Fecha no válida. Ingrese una fecha válida (ej: dd/mm/aaaa).";
            MessageIsError = true;
            Load();
            return Page();
        }

        repository.SetFechaUltimaCarpeta(id, parsed);
        if (isAjax)
        {
            var sector = parsed < new DateOnly(2023, 7, 1) ? "Archivo" : "Oficina 43";
            return new JsonResult(new { success = true, fecha = parsed.ToString("yyyy-MM-dd"), sector });
        }
        return RedirectToPage(new { search = Search, highlightId = HighlightId });
    }

    public IActionResult OnPostSetCodigoF8(long id, string? codigoF8)
    {
        var isAjax = string.Equals(HttpContext?.Request?.Headers?.XRequestedWith.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                     || (HttpContext?.Request?.Headers?.Accept.ToString()?.Contains("application/json") ?? false);

        var clean = string.IsNullOrWhiteSpace(codigoF8) ? null : codigoF8.Trim();
        repository.SetCodigoF8(id, clean);
        if (isAjax)
        {
            return new JsonResult(new { success = true, codigoF8 = clean ?? "" });
        }
        return RedirectToPage(new { search = Search, highlightId = HighlightId });
    }

    /// <summary>The single F8 "Revertir": clears the F8 status (whether or not the F8 was already
    /// uploaded), keeping the typed data, and returns the case to Casos with only the "Caja" action. No email is sent.</summary>
    public IActionResult OnPostRevertToCasos(long id)
    {
        repository.RevertF8AndReturnToCasos(id);
        Message = "Caso revertido a Cambio de Domicilio: datos ingresados conservados, listo para enviar a Caja (sin enviar correo).";
        return RedirectToPage(new { search = Search, highlightId = HighlightId });
    }

    /// <summary>"Caja": the physical folder was found, so the case goes straight to the Caja queue. No email is sent.</summary>
    public IActionResult OnPostSendToCaja(long id)
    {
        repository.SendToCaja([id], DateTimeOffset.UtcNow);
        Message = "Carpeta enviada a la cola de Caja.";
        return RedirectToPage(new { search = Search, highlightId = HighlightId });
    }

    /// <summary>"Sin carpeta": closes the process without a folder. The case moves to the Sin Carpetas screen.</summary>
    public IActionResult OnPostCloseWithoutFolder(long id)
    {
        repository.CloseWithoutFolder(id, DateTimeOffset.UtcNow);
        Message = "Caso cerrado sin carpeta física y trasladado a la sección 'Sin Carpetas'.";
        return RedirectToPage(new { search = Search, highlightId = HighlightId });
    }

    public IActionResult OnPostSetPersonData(long id, string nombre, string rut)
    {
        var isAjax = string.Equals(HttpContext?.Request?.Headers?.XRequestedWith.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                     || (HttpContext?.Request?.Headers?.Accept.ToString()?.Contains("application/json") ?? false);

        var normalizedRut = RutValidator.NormalizeAndValidate(rut);
        nombre = (nombre ?? string.Empty).Trim().ToUpperInvariant();

        if (normalizedRut is null || nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
        {
            var errorMsg = normalizedRut is null
                ? "El RUT ingresado no es válido (revise el dígito verificador)."
                : "Ingrese el nombre completo (al menos nombre y apellido).";

            if (isAjax)
            {
                return new JsonResult(new { success = false, message = errorMsg }) { StatusCode = 400 };
            }

            Message = errorMsg;
            MessageIsError = true;
            Load();
            return Page();
        }

        repository.SetPersonData(id, nombre, normalizedRut);
        if (isAjax)
        {
            return new JsonResult(new { success = true, nombre, rut = normalizedRut });
        }
        Message = "Datos guardados. El caso ya no requiere revisión.";
        Load();
        return Page();
    }

    public IActionResult OnPostToggleMarked(long id, string? markedValue)
    {
        var marked = markedValue == "on";
        repository.SetMarked(id, marked);
        return RedirectToPage(new { search = Search, highlightId = HighlightId });
    }

    public IActionResult OnPostTogglePendienteCarpeta(long id, string? pendienteCarpetaValue)
    {
        var pendienteCarpeta = pendienteCarpetaValue == "on";
        repository.SetPendienteCarpeta(id, pendienteCarpeta);
        return RedirectToPage(new { search = Search, highlightId = HighlightId });
    }

    public IActionResult OnPostDeleteCase(long id)
    {
        var sourceMessageId = repository.FindById(id)?.SourceMessageId;
        repository.Delete(id);
        if (sourceMessageId is not null)
        {
            repository.RecordDeletedSourceMessage(sourceMessageId);
        }

        Message = "Caso eliminado.";
        return RedirectToPage(new { search = Search, highlightId = HighlightId });
    }

    public async Task<IActionResult> OnPostConfirmAsync(long id)
    {
        var contacts = routingService.LoadDirectory();
        var result = await routingService.SendConfirmationAsync(id, contacts, HttpContext.RequestAborted, viaF8: true);

        Message = result.Reason;
        MessageIsError = !result.Sent;
        Load();
        return Page();
    }

    public async Task<IActionResult> OnPostMarkUploadedAndConfirmAsync(long id)
    {
        var contacts = routingService.LoadDirectory();
        var result = await routingService.MarkUploadedAndConfirmAsync(id, contacts, HttpContext.RequestAborted, viaF8: true);

        if (result.Sent)
        {
            repository.SetMarked(id, false);
            repository.SetPendienteCarpeta(id, false);
        }

        Message = result.Reason;
        MessageIsError = !result.Sent;
        Load();
        return Page();
    }

    /// <summary>Operator "Marcar resuelto" on a bounced F8 confirmation — clears the bounce flag.</summary>
    public IActionResult OnPostResolveBounce(long id)
    {
        repository.ClearConfirmationBounced(id);
        return RedirectToPage(new { search = Search, highlightId = HighlightId });
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
        var all = repository.GetAll();
        var f8Cases = all
            .Where(c => c.Destination == CaseDestination.F8)
            .OrderBy(c => c.Status == RequestStatus.Confirmed)
            .ThenBy(c => c.ConfirmedAt)
            .ThenByDescending(c => c.ReceivedAt)
            .ToList();

        TotalF8Count = f8Cases.Count;

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var query = Search.Trim().ToUpperInvariant();
            var matches = f8Cases.Where(c => MatchesQuery(c, query)).ToList();
            Cases = matches;
            CasosMatchCount = all.Count(c => c.TransferredAt is null && MatchesQuery(c, query));
            if (!HighlightId.HasValue && matches.Count > 0)
            {
                HighlightId = matches[0].Id;
            }
        }
        else
        {
            Cases = f8Cases;
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
