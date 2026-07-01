using Microsoft.Extensions.Logging;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Directories;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Extraction;
using OutlookComunaRouter.Graph;
using OutlookComunaRouter.Notifications;
using OutlookComunaRouter.Persistence;

namespace OutlookComunaRouter.Routing;

public sealed class AddressChangeRoutingService(
    IPersonRequestRepository repository,
    IComunaDirectory directory,
    IMailSender mailSender,
    IEnumerable<INotificationChannel> notificationChannels,
    RouterOptions options,
    ILogger<AddressChangeRoutingService> logger)
{
    public IReadOnlyList<ComunaContact> LoadDirectory() => directory.LoadFromCsv(options.ComunaDirectoryCsvPath);

    /// <summary>Processes one incoming email: detect, extract, dedupe, and send the folder request if eligible.</summary>
    public async Task ProcessNotificationAsync(IncomingEmail email, IReadOnlyList<ComunaContact> contacts, CancellationToken cancellationToken)
    {
        if (repository.ExistsBySourceMessageId(email.MessageId))
        {
            return; // already processed this exact message
        }

        var senderDomain = ExtractDomain(email.SenderAddress);
        var comunaContact = directory.ResolveByDomain(senderDomain, options.OwnDomain, contacts);
        if (comunaContact is null)
        {
            return; // not a recognized comuna domain, not routing-relevant
        }

        var extracted = PersonDataExtractor.Extract(email.BodyText);
        var needsReview = extracted.FullName is null || extracted.Rut is null;

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
            Status = RequestStatus.Pending
        };

        if (needsReview)
        {
            repository.Insert(request);
            logger.LogInformation("Notificación insertada como pendiente de revisión (datos incompletos)");
            return;
        }

        var existingActive = repository.FindActiveByRutAndComuna(request.Rut!, request.Comuna!);
        if (existingActive is not null)
        {
            request.NeedsReview = false;
            repository.Insert(request); // linked record, kept pending, no new outgoing email
            logger.LogInformation("Solicitud duplicada detectada para RUT/comuna ya {Status}, no se reenvía", existingActive.Status);
            return;
        }

        var id = repository.Insert(request);

        var (subject, body) = EmailTemplates.FolderRequest(request.FullName!, request.Rut!);
        await mailSender.SendAsync(comunaContact.ContactEmail, subject, body, cancellationToken);
        // Graph's sendMail endpoint returns 202 Accepted with no message ID, so the outgoing
        // message ID cannot be captured here; the sent timestamp is what idempotency relies on.
        repository.UpdateStatusToSent(id, requestMessageId: "n/a", sentAt: DateTimeOffset.UtcNow);
        logger.LogInformation("Solicitud de carpeta enviada a la comuna correspondiente");
    }

    /// <summary>Processes one incoming email as a potential reply from a comuna to a previously-sent request.</summary>
    public void ProcessPotentialReply(IncomingEmail email, IReadOnlyList<ComunaContact> contacts)
    {
        var senderDomain = ExtractDomain(email.SenderAddress);
        var comunaContact = directory.ResolveByDomain(senderDomain, options.OwnDomain, contacts);
        if (comunaContact is null)
        {
            return;
        }

        var byThread = string.IsNullOrEmpty(email.ConversationId)
            ? null
            : repository.FindSentByConversationId(email.ConversationId);

        var match = byThread ?? FindByRutFallback(email, comunaContact);
        if (match is null)
        {
            return;
        }

        repository.UpdateStatusToResponded(match.Id, email.MessageId, email.ReceivedAt, lastFolderDate: null);
        logger.LogInformation("Respuesta detectada y vinculada a la solicitud original");

        foreach (var channel in notificationChannels)
        {
            channel.NotifyResponded(match.FullName!, match.Rut!, match.Comuna!);
        }
    }

    private PersonRequest? FindByRutFallback(IncomingEmail email, ComunaContact comunaContact)
    {
        var extracted = PersonDataExtractor.Extract(email.BodyText);
        if (extracted.Rut is null)
        {
            return null;
        }

        return repository.FindActiveByRutAndComuna(extracted.Rut, comunaContact.Comuna) is { Status: RequestStatus.Sent } candidate
            ? candidate
            : null;
    }

    private static string ExtractDomain(string emailAddress)
    {
        var at = emailAddress.LastIndexOf('@');
        return at >= 0 ? emailAddress[(at + 1)..] : emailAddress;
    }
}
