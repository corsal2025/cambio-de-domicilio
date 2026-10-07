using Microsoft.Extensions.Logging;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Directories;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Extraction;
using CambioDeDomicilio.Mail;
using CambioDeDomicilio.Notifications;
using CambioDeDomicilio.Persistence;

namespace CambioDeDomicilio.Routing;

public sealed record ConfirmationResult(bool Sent, string Reason);

/// <summary>
/// Tracks folder requests other comunas make to Valparaíso: registers incoming requests found in
/// "CARP. PARA PEDIR", marks them as uploaded when the operator moves the email to
/// "CARP. YA SUBIDAS" (or via the one-click <see cref="MarkUploadedAndConfirmAsync"/>), and sends
/// the confirmation email only when the operator explicitly triggers it — never on a poll cycle by
/// itself.
/// </summary>
public sealed class AddressChangeRoutingService(
    IPersonRequestRepository repository,
    IDiscardedEmailRepository discardedRepository,
    IMessageTombstoneRepository tombstones,
    IComunaDirectory directory,
    IMailSender mailSender,
    IEmailMover emailMover,
    IEnumerable<INotificationChannel> notificationChannels,
    RouterOptions options,
    ILogger<AddressChangeRoutingService> logger)
{
    /// <summary>Cases whose confirmation email is being sent right now. The app is a single process
    /// (guarded by a named mutex in Program.cs), so an in-memory claim is enough to stop a double
    /// click or a second browser tab from sending the same email twice: both requests would
    /// otherwise read Status == Uploaded before either one persists Confirmed.</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, byte> confirmationsInFlight = new();

    private static readonly ConfirmationResult ConfirmationAlreadyInProgress =
        new(false, "Ya hay una confirmación en curso para este caso, espere unos segundos");

    private async Task<ConfirmationResult> RunExclusivelyAsync(long requestId, Func<Task<ConfirmationResult>> action)
    {
        if (!confirmationsInFlight.TryAdd(requestId, 0))
        {
            return ConfirmationAlreadyInProgress;
        }

        try
        {
            return await action();
        }
        finally
        {
            confirmationsInFlight.TryRemove(requestId, out _);
        }
    }

    public IReadOnlyList<ComunaContact> LoadDirectory() => directory.LoadFromCsv(options.ComunaDirectoryCsvPath);

    /// <summary>Processes one email found in the source folder ("CARP. PARA PEDIR").</summary>
    public void ProcessIncomingRequest(IncomingEmail email, IReadOnlyList<ComunaContact> contacts)
    {
        if (tombstones.IsSourceMessageDeleted(email.MessageId))
        {
            // The operator explicitly deleted every case tracked from this email — as long as it
            // sits unmoved in the source folder, every poll cycle would otherwise recreate it.
            return;
        }

        if (repository.ExistsBySourceMessageId(email.MessageId))
        {
            // The operator may have moved this email back to the source folder to undo an
            // accidental "Marcar subida" or manual move to "CARP. YA SUBIDAS" — revert any
            // Uploaded rows to Pending so the dashboard reflects reality. Confirmed rows are
            // never touched: a real confirmation email already went to the comuna, so silently
            // reverting would create a mismatch with what the comuna was already told.
            var reverted = repository.RevertUploadedBySourceMessageId(email.MessageId);
            if (reverted > 0)
            {
                logger.LogInformation(
                    "Correo encontrado de nuevo en '{Folder}': {Count} caso(s) revertido(s) de Subido a Pendiente",
                    options.SourceFolderName, reverted);
            }

            return; // already tracked
        }

        var senderDomain = ExtractDomain(email.SenderAddress);
        if (string.Equals(senderDomain, options.OwnDomain, StringComparison.OrdinalIgnoreCase))
        {
            return; // internal correspondence, not routing-relevant, not worth flagging
        }

        var comunaContact = directory.ResolveByDomain(email.SenderAddress, options.OwnDomain, contacts);
        if (comunaContact is null)
        {
            if (!discardedRepository.ExistsBySourceMessageId(email.MessageId))
            {
                var sharedByMultipleComunas = contacts.Count(c => string.Equals(c.Domain, senderDomain, StringComparison.OrdinalIgnoreCase)) > 1;
                var reason = sharedByMultipleComunas
                    ? $"Dominio compartido por varias comunas ({senderDomain}); la dirección remitente no está registrada para ninguna"
                    : $"Dominio no reconocido en el directorio de comunas: {senderDomain}";

                discardedRepository.Insert(new DiscardedEmail
                {
                    SourceMessageId = email.MessageId,
                    SourceSubject = email.Subject,
                    SourceSender = email.SenderAddress,
                    Reason = reason
                });
                logger.LogWarning(
                    "Correo descartado: {Reason} (asunto: {Subject})",
                    reason, email.Subject);
            }

            return; // not a recognized comuna domain, not routing-relevant
        }

        // This message may have been discarded on a previous cycle before the directory knew this
        // domain (or before an exact address was registered for a shared domain) — now that it
        // resolves, drop the stale discard record so it doesn't linger next to the tracked case.
        discardedRepository.DeleteBySourceMessageId(email.MessageId);

        // The body and the subject (e.g. a forwarded email's original request) are independent
        // sources — a comuna can name a contributor in either one, or different contributors in
        // each, so both are always checked and merged rather than one short-circuiting the other.
        var fromBody = PersonDataExtractor.ExtractAll(email.BodyText);
        var fromSubject = PersonDataExtractor.ExtractFromSubject(email.Subject);
        var contributors = MergeContributors(fromBody, fromSubject);
        if (contributors.Count == 0)
        {
            contributors = [new ExtractedPersonData(null, null)]; // no anchor at all — one placeholder for manual review
        }

        foreach (var contributor in contributors)
        {
            InsertContributorRequest(email, comunaContact, contributor);
        }

        if (contributors.Count > 1)
        {
            logger.LogInformation("Correo con {Count} contribuyentes: se registró un caso por cada uno", contributors.Count);
        }
    }

    /// <summary>Combines body and subject contributors into one list, matching by RUT so the same
    /// person named in both places yields a single entry (preferring whichever side has a name).</summary>
    private static List<ExtractedPersonData> MergeContributors(IReadOnlyList<ExtractedPersonData> fromBody, ExtractedPersonData fromSubject)
    {
        var merged = fromBody.ToList();
        if (fromSubject.Rut is null)
        {
            return merged;
        }

        var existingIndex = merged.FindIndex(p => p.Rut == fromSubject.Rut);
        if (existingIndex < 0)
        {
            merged.Add(fromSubject);
        }
        else if (merged[existingIndex].FullName is null && fromSubject.FullName is not null)
        {
            merged[existingIndex] = fromSubject;
        }

        return merged;
    }

    private void InsertContributorRequest(IncomingEmail email, ComunaContact comunaContact, ExtractedPersonData extracted)
    {
        var needsReview = extracted.FullName is null || extracted.Rut is null;

        if (!needsReview && repository.FindByRutAndComuna(extracted.Rut!, comunaContact.Comuna) is not null)
        {
            // Same person already tracked for this comuna (e.g. a resend) — do not create a
            // second row, which would otherwise duplicate this person in the report.
            logger.LogInformation("Solicitud ya registrada para esta persona y comuna, se omite duplicado");
            return;
        }

        // No RUT extracted (needsReview) — the RUT-based check above can't run, so fall back to
        // matching by full name + comuna. Without this, resent emails with unreadable RUTs would
        // duplicate the same person every time the sender re-sends the request.
        if (needsReview && extracted.FullName is not null
            && repository.FindByFullNameAndComuna(extracted.FullName, comunaContact.Comuna) is not null)
        {
            logger.LogInformation("Solicitud ya registrada para esta persona (por nombre) y comuna, se omite duplicado");
            return;
        }

        var request = new PersonRequest
        {
            // PersonDataExtractor preserves the source email's original casing (see
            // PersonDataExtractorTests) — normalize to uppercase here, at the storage boundary,
            // so the report and dashboard always show a consistent case.
            FullName = extracted.FullName?.Trim().ToUpperInvariant(),
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
    /// Processes one email found in the confirmation folder ("CARP. YA SUBIDAS"): marks the
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

    /// <summary>
    /// Processes one message from the mailbox inbox: if it is a non-delivery report for one of our
    /// "carpeta subida a Conaset" confirmation emails (see <see cref="BounceDetector"/>), finds the
    /// Confirmed case(s) it refers to by RUT and flags them so the operator sees the comuna never
    /// received the notice. When the NDR body names exactly one known comuna contact address (an
    /// NDR normally echoes back the original recipient), only that comuna's matching case(s) are
    /// flagged — a contributor can have Confirmed cases with the same RUT open for more than one
    /// comuna at once, and only one of them may have actually bounced. Every recognized NDR is
    /// tombstoned so a later poll never re-processes it (the message stays in the inbox); an NDR
    /// that matches no Confirmed case is still tombstoned.
    /// </summary>
    public void ProcessPotentialBounce(IncomingEmail email, IReadOnlyList<ComunaContact> contacts)
    {
        if (tombstones.IsBounceProcessed(email.MessageId))
        {
            return;
        }

        if (!BounceDetector.LooksLikeConfirmationBounce(email))
        {
            return; // a normal comuna reply, or a bounce for some unrelated email — leave it alone
        }

        var ruts = PersonDataExtractor.ExtractAll(email.BodyText)
            .Select(p => p.Rut)
            .Where(r => r is not null)
            .Distinct()
            .ToList();

        var bouncedComuna = FindBouncedRecipientComuna(email.BodyText, contacts);

        var flagged = 0;
        foreach (var rut in ruts)
        {
            var matches = repository.FindConfirmedByRut(rut!);
            var candidates = bouncedComuna is null
                ? matches // recipient not identified — fall back to flagging every Confirmed match for the RUT
                : matches.Where(c => string.Equals(c.Comuna, bouncedComuna, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var confirmed in candidates)
            {
                if (confirmed.ConfirmationBouncedAt is not null)
                {
                    continue; // already flagged from an earlier NDR — keep the first timestamp
                }

                repository.SetConfirmationBounced(confirmed.Id, email.ReceivedAt);
                flagged++;
            }
        }

        tombstones.RecordProcessedBounce(email.MessageId);

        if (flagged > 0)
        {
            logger.LogWarning(
                "Rebote de confirmación detectado: {Count} caso(s) marcado(s) como no entregado(s) a la comuna", flagged);
        }
        else
        {
            logger.LogWarning(
                "Rebote de confirmación detectado pero sin caso Confirmado que coincida (asunto: {Subject})", email.Subject);
        }
    }

    /// <summary>
    /// Identifies which comuna's confirmation actually bounced by looking for that comuna's known
    /// contact address literally quoted in the NDR body (Exchange echoes back the original
    /// recipient, e.g. "Your message to x@y.cl could not be delivered"). Returns null — rather than
    /// guessing — when no known address is found or more than one matches, mirroring
    /// <see cref="IComunaDirectory.ResolveByDomain"/>'s "don't guess among several" rule.
    /// </summary>
    private static string? FindBouncedRecipientComuna(string? bodyText, IReadOnlyList<ComunaContact> contacts)
    {
        if (string.IsNullOrEmpty(bodyText))
        {
            return null;
        }

        var matches = contacts
            .Where(c => bodyText.Contains(c.ContactEmail, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Comuna)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>
    /// Operator-triggered one-click action for a Pending case: moves the original email to
    /// "CARP. YA SUBIDAS" (marking it unread there), transitions the case to Uploaded, and
    /// immediately sends the confirmation email — collapsing what would otherwise be a manual
    /// Outlook drag plus a separate "Enviar confirmación" click. If the email can't be found/moved
    /// (e.g. already moved manually, or a mailbox hiccup), the case still advances — the operator
    /// explicitly asked for this outcome, so a mailbox-side inconsistency shouldn't block it.
    /// </summary>
    public Task<ConfirmationResult> MarkUploadedAndConfirmAsync(long requestId, IReadOnlyList<ComunaContact> contacts, CancellationToken cancellationToken, bool viaF8 = false) =>
        RunExclusivelyAsync(requestId, () => MarkUploadedAndConfirmCoreAsync(requestId, contacts, cancellationToken, viaF8));

    private async Task<ConfirmationResult> MarkUploadedAndConfirmCoreAsync(long requestId, IReadOnlyList<ComunaContact> contacts, CancellationToken cancellationToken, bool viaF8)
    {
        var request = repository.FindById(requestId);
        if (request is null)
        {
            return new ConfirmationResult(false, "El caso no existe");
        }

        if (request.Status != RequestStatus.Pending)
        {
            return new ConfirmationResult(false, $"El caso está en estado {request.Status}, solo se puede marcar como subido un caso Pendiente");
        }

        if (request.NeedsReview || request.Rut is null || request.FullName is null || request.Comuna is null)
        {
            return new ConfirmationResult(false, "El caso tiene datos incompletos, corregir antes de marcar como subido");
        }

        if (request.FechaUltimaCarpeta is null && !request.SinCarpeta)
        {
            return new ConfirmationResult(false, "Debe ingresar la fecha de última carpeta antes de marcar como subido y confirmar");
        }

        var moved = await emailMover.MoveAndMarkUnreadAsync(
            request.SourceMessageId, options.SourceFolderName, options.ConfirmationFolderName, cancellationToken);
        if (!moved)
        {
            logger.LogWarning(
                "No se pudo mover el correo original de '{Source}' a '{Destination}'; se continúa marcando el caso igual",
                options.SourceFolderName, options.ConfirmationFolderName);
        }

        repository.MarkUploaded(request.Id, DateTimeOffset.UtcNow);

        return await SendConfirmationCoreAsync(requestId, contacts, cancellationToken, viaF8);
    }

    /// <summary>Operator-triggered (button): sends the confirmation email for an Uploaded case.</summary>
    public Task<ConfirmationResult> SendConfirmationAsync(long requestId, IReadOnlyList<ComunaContact> contacts, CancellationToken cancellationToken, bool viaF8 = false) =>
        RunExclusivelyAsync(requestId, () => SendConfirmationCoreAsync(requestId, contacts, cancellationToken, viaF8));

    private async Task<ConfirmationResult> SendConfirmationCoreAsync(long requestId, IReadOnlyList<ComunaContact> contacts, CancellationToken cancellationToken, bool viaF8)
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

        if (request.FechaUltimaCarpeta is null && !request.SinCarpeta)
        {
            return new ConfirmationResult(false, "Debe ingresar la fecha de última carpeta antes de confirmar");
        }

        // Ordinal-insensitive: a case can predate a later casing normalization of the directory
        // CSV (e.g. tracked as "Catemu", directory now says "CATEMU") — comuna names are not
        // meant to be case-sensitive identifiers, and rejecting a real match here would silently
        // block confirmation for no real reason.
        var comunaContact = contacts.FirstOrDefault(c => string.Equals(c.Comuna, request.Comuna, StringComparison.OrdinalIgnoreCase));
        if (comunaContact is null)
        {
            return new ConfirmationResult(false, $"La comuna {request.Comuna} no está en el directorio de contactos");
        }

        var (subject, body) = viaF8
            ? EmailTemplates.UploadConfirmationF8(request.FullName, request.Rut)
            : EmailTemplates.UploadConfirmation(request.FullName, request.Rut);
        await mailSender.SendAsync(comunaContact.ContactEmail, subject, body, cancellationToken);
        repository.UpdateStatusToConfirmed(request.Id, DateTimeOffset.UtcNow);
        if (!viaF8)
        {
            repository.SetDestination(request.Id, CaseDestination.Subidas, DateTimeOffset.UtcNow);
        }
        logger.LogInformation("Confirmación de subida enviada a la comuna correspondiente");

        foreach (var channel in notificationChannels)
        {
            channel.NotifyConfirmationSent(request.FullName, request.Rut, request.Comuna);
        }

        return new ConfirmationResult(true, "Confirmación enviada");
    }

    /// <summary>
    /// Operator-triggered undo for a Confirmed case clicked by mistake: sends a rectification
    /// email to the same comuna contact explicitly retracting the earlier confirmation, then
    /// resets the case to Pending. Only Confirmed cases can be rectified this way — an Uploaded
    /// case that was never confirmed has nothing to retract, since no email ever reached the comuna.
    /// </summary>
    public Task<ConfirmationResult> RectifyConfirmationAsync(long requestId, IReadOnlyList<ComunaContact> contacts, CancellationToken cancellationToken) =>
        RunExclusivelyAsync(requestId, () => RectifyConfirmationCoreAsync(requestId, contacts, cancellationToken));

    private async Task<ConfirmationResult> RectifyConfirmationCoreAsync(long requestId, IReadOnlyList<ComunaContact> contacts, CancellationToken cancellationToken)
    {
        var request = repository.FindById(requestId);
        if (request is null)
        {
            return new ConfirmationResult(false, "El caso no existe");
        }

        if (request.Status != RequestStatus.Confirmed)
        {
            return new ConfirmationResult(false, $"El caso está en estado {request.Status}, solo se rectifican casos ya Confirmados");
        }

        if (request.Rut is null || request.FullName is null || request.Comuna is null)
        {
            return new ConfirmationResult(false, "El caso tiene datos incompletos, no se puede rectificar automáticamente");
        }

        var comunaContact = contacts.FirstOrDefault(c => string.Equals(c.Comuna, request.Comuna, StringComparison.OrdinalIgnoreCase));
        if (comunaContact is null)
        {
            return new ConfirmationResult(false, $"La comuna {request.Comuna} no está en el directorio de contactos");
        }

        var (subject, body) = EmailTemplates.ConfirmationRectification(request.FullName, request.Rut);
        await mailSender.SendAsync(comunaContact.ContactEmail, subject, body, cancellationToken);
        repository.RevertConfirmedToPending(request.Id);
        logger.LogInformation("Correo de rectificación enviado y caso revertido a Pendiente");

        return new ConfirmationResult(true, "Correo de rectificación enviado y caso revertido a Pendiente");
    }

    private static string ExtractDomain(string emailAddress)
    {
        var at = emailAddress.LastIndexOf('@');
        return at >= 0 ? emailAddress[(at + 1)..] : emailAddress;
    }
}
