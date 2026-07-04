using Microsoft.Extensions.Logging;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Directories;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Extraction;
using OutlookComunaRouter.Mail;
using OutlookComunaRouter.Notifications;
using OutlookComunaRouter.Persistence;

namespace OutlookComunaRouter.Routing;

public sealed record ConfirmationResult(bool Sent, string Reason);

/// <summary>
/// Tracks folder requests other comunas make to Valparaíso: registers incoming requests found in
/// "CARP. PARA PEDIR", marks them as uploaded when the operator moves the email to
/// "CARP. YA PEDIDAS", and sends the confirmation email only when the operator explicitly
/// triggers it (button) — never automatically.
/// </summary>
public sealed class AddressChangeRoutingService(
    IPersonRequestRepository repository,
    IComunaDirectory directory,
    IMailSender mailSender,
    IEnumerable<INotificationChannel> notificationChannels,
    RouterOptions options,
    ILogger<AddressChangeRoutingService> logger)
{
    public IReadOnlyList<ComunaContact> LoadDirectory() => directory.LoadFromCsv(options.ComunaDirectoryCsvPath);

    /// <summary>Processes one email found in the source folder ("CARP. PARA PEDIR").</summary>
    public void ProcessIncomingRequest(IncomingEmail email, IReadOnlyList<ComunaContact> contacts)
    {
        if (repository.ExistsBySourceMessageId(email.MessageId))
        {
            return; // already tracked
        }

        var senderDomain = ExtractDomain(email.SenderAddress);
        var comunaContact = directory.ResolveByDomain(senderDomain, options.OwnDomain, contacts);
        if (comunaContact is null)
        {
            return; // not a recognized comuna domain, not routing-relevant
        }

        var extracted = PersonDataExtractor.Extract(email.BodyText);
        var needsReview = extracted.FullName is null || extracted.Rut is null;

        if (!needsReview && repository.FindByRutAndComuna(extracted.Rut!, comunaContact.Comuna) is not null)
        {
            // Same person already tracked for this comuna (e.g. a resend) — do not create a
            // second row, which would otherwise duplicate this person in the report.
            logger.LogInformation("Solicitud ya registrada para esta persona y comuna, se omite duplicado");
            return;
        }

        var request = new PersonRequest
        {
            FullName = extracted.FullName,
            Rut = extracted.Rut,
            Comuna = comunaContact.Comuna,
            SourceMessageId = email.MessageId,
            SourceConversationId = email.ConversationId,
            SourceSubject = email.Subject,
            SourceSender = email.SenderAddress,
            NeedsReview = needsReview,
            Status = RequestStatus.Pending,
            ReceivedAt = email.ReceivedAt
        };

        repository.Insert(request);
        logger.LogInformation(
            needsReview
                ? "Solicitud entrante registrada como pendiente de revisión (datos incompletos)"
                : "Solicitud entrante registrada como pendiente");
    }

    /// <summary>
    /// Processes one email found in the confirmation folder ("CARP. YA PEDIDAS"): marks the
    /// matching pending case as Uploaded. Does NOT send anything — sending is an explicit
    /// operator action via <see cref="SendConfirmationAsync"/>.
    /// </summary>
    public void ProcessUploadedCase(IncomingEmail email)
    {
        var pending = repository.FindPendingBySourceMessageId(email.MessageId);
        if (pending is null)
        {
            return; // not a tracked request, or already uploaded/confirmed
        }

        repository.MarkUploaded(pending.Id, DateTimeOffset.UtcNow);
        logger.LogInformation("Caso marcado como subido a Conaset, a la espera de confirmación manual");
    }

    /// <summary>Operator-triggered (button): sends the confirmation email for an Uploaded case.</summary>
    public async Task<ConfirmationResult> SendConfirmationAsync(long requestId, long confirmedByUserId, IReadOnlyList<ComunaContact> contacts, CancellationToken cancellationToken)
    {
        var request = repository.FindById(requestId);
        if (request is null)
        {
            return new ConfirmationResult(false, "El caso no existe");
        }

        if (request.Status != RequestStatus.Uploaded)
        {
            return new ConfirmationResult(false, $"El caso está en estado {request.Status}, solo se confirman casos subidos");
        }

        if (request.NeedsReview || request.Rut is null || request.FullName is null || request.Comuna is null)
        {
            return new ConfirmationResult(false, "El caso tiene datos incompletos, corregir antes de confirmar");
        }

        var comunaContact = contacts.FirstOrDefault(c => c.Comuna == request.Comuna);
        if (comunaContact is null)
        {
            return new ConfirmationResult(false, $"La comuna {request.Comuna} no está en el directorio de contactos");
        }

        var (subject, body) = EmailTemplates.UploadConfirmation(request.FullName, request.Rut);
        await mailSender.SendAsync(comunaContact.ContactEmail, subject, body, cancellationToken);
        repository.UpdateStatusToConfirmed(request.Id, DateTimeOffset.UtcNow, confirmedByUserId);
        logger.LogInformation("Confirmación de subida enviada a la comuna correspondiente");

        foreach (var channel in notificationChannels)
        {
            channel.NotifyConfirmationSent(request.FullName, request.Rut, request.Comuna);
        }

        return new ConfirmationResult(true, "Confirmación enviada");
    }

    private static string ExtractDomain(string emailAddress)
    {
        var at = emailAddress.LastIndexOf('@');
        return at >= 0 ? emailAddress[(at + 1)..] : emailAddress;
    }
}
