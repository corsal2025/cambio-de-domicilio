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
    public int PlazoDiasHabiles => options.PlazoDiasHabiles;

    public void OnGet()
    {
        Load();
    }

    public IActionResult OnPostSetFecha(long id, string fecha)
    {
        if (string.IsNullOrWhiteSpace(fecha))
        {
            repository.ClearFechaUltimaCarpeta(id);
            return RedirectToPage();
        }

        if (fecha.Trim().Equals("S/C", StringComparison.OrdinalIgnoreCase))
        {
            repository.SetSinCarpeta(id);
            return RedirectToPage();
        }

        if (!SpanishDate.TryParse(fecha, out var parsed))
        {
            Message = "Fecha no reconocida. Formatos aceptados: 15/03/2024 o 15 marzo 2024.";
            MessageIsError = true;
            Load();
            return Page();
        }

        repository.SetFechaUltimaCarpeta(id, parsed);
        return RedirectToPage();
    }

    public IActionResult OnPostSetCodigoF8(long id, string? codigoF8)
    {
        repository.SetCodigoF8(id, string.IsNullOrWhiteSpace(codigoF8) ? null : codigoF8.Trim());
        return RedirectToPage();
    }

    /// <summary>Undoes "Traspaso a F8" — clears <see cref="PersonRequest.Destination"/> so the case
    /// leaves this F8 screen and reappears in Casos (still F8-ticked, ready to be re-transferred).</summary>
    public IActionResult OnPostUndoTransfer(long id)
    {
        repository.ClearDestination(id);
        return RedirectToPage();
    }

    public IActionResult OnPostSetPersonData(long id, string nombre, string rut)
    {
        var normalizedRut = RutValidator.NormalizeAndValidate(rut);
        nombre = (nombre ?? string.Empty).Trim().ToUpperInvariant();

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

    public IActionResult OnPostToggleMarked(long id, string? markedValue)
    {
        var marked = markedValue == "on";
        repository.SetMarked(id, marked);
        return RedirectToPage();
    }

    public IActionResult OnPostTogglePendienteCarpeta(long id, string? pendienteCarpetaValue)
    {
        var pendienteCarpeta = pendienteCarpetaValue == "on";
        repository.SetPendienteCarpeta(id, pendienteCarpeta);
        return RedirectToPage();
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
        return RedirectToPage();
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

        // Once confirmed, the row goes fully blue (row-confirmed) — any leftover Marcar/Pendiente
        // Carpeta tick would otherwise still highlight it yellow (row-pendiente-carpeta) or keep it
        // selected for the next PDF run, both of which no longer make sense for a closed case.
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

    public async Task<IActionResult> OnPostRectifyConfirmationAsync(long id)
    {
        var contacts = routingService.LoadDirectory();
        var result = await routingService.RectifyConfirmationAsync(id, contacts, HttpContext.RequestAborted);

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
        Cases = repository.GetAll()
            .Where(c => c.Destination == CaseDestination.F8)
            .OrderBy(c => c.Status == RequestStatus.Confirmed)
            .ThenBy(c => c.ConfirmedAt)
            .ThenByDescending(c => c.ReceivedAt)
            .ToList();
    }
}
