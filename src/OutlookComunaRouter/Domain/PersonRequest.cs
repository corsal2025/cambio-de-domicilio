namespace OutlookComunaRouter.Domain;

public enum RequestStatus
{
    /// <summary>Request registered from CARP. PARA PEDIR; folder not uploaded yet.</summary>
    Pending,

    /// <summary>The operator moved the email to CARP. YA SUBIDAS: folder uploaded to Conaset, confirmation not sent yet.</summary>
    Uploaded,

    /// <summary>Confirmation email sent to the requesting comuna (operator pressed the send button).</summary>
    Confirmed
}

public enum FolderSector
{
    /// <summary>Última carpeta before July 2023 — stored in Archivo.</summary>
    Archivo,

    /// <summary>Última carpeta from July 2023 onwards — stored in Oficina 43.</summary>
    Oficina43
}

/// <summary>A folder request another comuna made to Valparaíso for a contributor's file.</summary>
public sealed class PersonRequest
{
    public long Id { get; set; }
    public string? FullName { get; set; }
    public string? Rut { get; set; }
    public string? Comuna { get; set; }
    public required string SourceMessageId { get; set; }
    public string? SourceConversationId { get; set; }
    public required string SourceSubject { get; set; }
    public required string SourceSender { get; set; }
    public bool NeedsReview { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    /// <summary>When the request email was received in the mailbox — the legal upload deadline counts from this date.</summary>
    public DateTimeOffset ReceivedAt { get; set; }

    /// <summary>Date of the contributor's última carpeta, typed in manually by the operator (paso 4).</summary>
    public DateOnly? FechaUltimaCarpeta { get; set; }

    public DateTimeOffset? UploadedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>Who pressed "Enviar confirmación" — a real email goes out to another municipality, so this is attributed.</summary>
    public long? ConfirmedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Operator-only bookkeeping checkbox, independent of Status — lets the operator tick off
    /// cases they've already cross-checked manually, with no effect on the routing/confirmation flow.</summary>
    public bool Marked { get; set; }

    /// <summary>When this case was last included in a printed sector document (see the Sector page's
    /// "Imprimir / Guardar como PDF" action). Null means it has never been printed. A case is excluded
    /// from future sector documents once this is set, so re-printing doesn't repeat already-requested names.</summary>
    public DateTimeOffset? SectorPdfGeneratedAt { get; set; }

    /// <summary>Physical location of the folder, derived from the última-carpeta date. Null until the date is entered.</summary>
    public FolderSector? Sector => FechaUltimaCarpeta is { } fecha
        ? fecha < new DateOnly(2023, 7, 1) ? FolderSector.Archivo : FolderSector.Oficina43
        : null;

    /// <summary>Operator-ticked flag: the physical folder could not be located, so a certification
    /// request must go to Secretaría Municipal instead of the normal upload flow.</summary>
    public bool FolderNotFound { get; set; }

    /// <summary>When this case was last included in a "carpeta no encontrada" batch notification
    /// (see Index page's "Avisar certificado" action). Null means it hasn't been notified yet —
    /// a case is excluded from future batches once this is set, so re-sending doesn't repeat names.</summary>
    public DateTimeOffset? FolderNotFoundNotifiedAt { get; set; }
}
