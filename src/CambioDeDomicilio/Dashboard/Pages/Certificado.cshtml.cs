using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Extraction;
using CambioDeDomicilio.Mail;
using CambioDeDomicilio.Notifications;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Routing;

namespace CambioDeDomicilio.Dashboard.Pages;

/// <summary>Lists only cases transferred to the Certificado destination
/// (<see cref="PersonRequest.Destination"/> == <see cref="CaseDestination.Certificado"/>) — the
/// dedicated screen for cases whose physical folder could not be located, resolved via the
/// certificate-request flow instead of the normal upload/confirm flow. Cases here never go
/// through Marcar subida/Confirmar/Deshacer y rectificar (that's F8/Casos-only); the only special
/// action is "Avisar certificado", which emails Secretaría Municipal a batch list plus an
/// acknowledgement to each contributor's comuna.</summary>
[Authorize]
public class CertificadoModel(
    IPersonRequestRepository repository,
    AddressChangeRoutingService routingService,
    IMailSender mailSender,
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

    /// <summary>Undoes "Traspaso a Certificado" — clears <see cref="PersonRequest.Destination"/> so
    /// the case leaves this screen and reappears in Casos (still F8-ticked, ready to be re-transferred).</summary>
    public IActionResult OnPostUndoTransfer(long id)
    {
        repository.ClearDestination(id);
        return RedirectToPage();
    }

    /// <summary>Sends the "carpeta no encontrada" batch email to Secretaría Municipal listing every
    /// not-yet-notified Certificado case, plus an individual acknowledgement email to each
    /// contributor's comuna, then marks every case included as notified so the next batch doesn't
    /// repeat names.</summary>
    public async Task<IActionResult> OnPostNotifyCertificadoAsync()
    {
        var pending = repository.GetAll()
            .Where(c => c.Destination == CaseDestination.Certificado && c.CertificadoNotifiedAt is null)
            .ToList();

        if (pending.Count == 0)
        {
            Message = "No hay casos Certificado pendientes de aviso.";
            MessageIsError = true;
            Load();
            return Page();
        }

        var contacts = routingService.LoadDirectory();
        var rows = new List<(string FullName, string Rut, string Comuna, string ComunaEmail)>();
        foreach (var item in pending)
        {
            var contact = contacts.FirstOrDefault(c => string.Equals(c.Comuna, item.Comuna, StringComparison.OrdinalIgnoreCase));
            rows.Add((item.FullName ?? string.Empty, item.Rut ?? string.Empty, item.Comuna ?? string.Empty, contact?.ContactEmail ?? "(sin correo registrado)"));
        }

        var (batchSubject, batchBody) = EmailTemplates.CertificateRequestBatch(rows);
        await mailSender.SendAsync(options.CertificateRequestEmailAddress, batchSubject, batchBody, HttpContext.RequestAborted);

        foreach (var item in pending)
        {
            var contact = contacts.FirstOrDefault(c => string.Equals(c.Comuna, item.Comuna, StringComparison.OrdinalIgnoreCase));
            if (contact is not null)
            {
                var (subject, body) = EmailTemplates.CertificateAcknowledgement(item.FullName ?? string.Empty, item.Rut ?? string.Empty);
                await mailSender.SendAsync(contact.ContactEmail, subject, body, HttpContext.RequestAborted);
            }
            repository.SetCertificadoNotified(item.Id, DateTimeOffset.UtcNow);
        }

        Message = $"Aviso de certificado enviado para {pending.Count} caso(s).";
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
            .Where(c => c.Destination == CaseDestination.Certificado)
            .OrderBy(c => c.Status == RequestStatus.Confirmed)
            .ThenBy(c => c.ConfirmedAt)
            .ThenByDescending(c => c.ReceivedAt)
            .ToList();
    }
}
