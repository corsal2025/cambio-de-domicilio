namespace CambioDeDomicilio.Domain;

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

    /// <summary>Operator-entered "S/C" (Sin Carpeta) in place of a date — the contributor has no
    /// previous folder. Mutually exclusive with FechaUltimaCarpeta: setting one clears the other.</summary>
    public bool SinCarpeta { get; set; }

    public DateTimeOffset? UploadedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>Who pressed "Enviar confirmación" — a real email goes out to another municipality, so this is attributed.</summary>
    public long? ConfirmedByUserId { get; set; }

    /// <summary>Set when a non-delivery report (bounce) for this case's confirmation email was found
    /// in the mailbox inbox — the requesting comuna never received the "carpeta subida" notice.
    /// Cleared by the operator with "Marcar resuelto" once they have re-sent it or handled it
    /// another way. Null means the confirmation is presumed delivered.</summary>
    public DateTimeOffset? ConfirmationBouncedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Operator-only bookkeeping checkbox, independent of Status — lets the operator tick off
    /// cases they've already cross-checked manually, with no effect on the routing/confirmation flow.</summary>
    public bool Marked { get; set; }

    /// <summary>When Marked was last ticked on. Drives the display/print order of marked cases —
    /// null while unmarked.</summary>
    public DateTimeOffset? MarkedAt { get; set; }

    /// <summary>When this case was last included in a printed sector document (see the Sector page's
    /// "Imprimir / Guardar como PDF" action). Null means it has never been printed. A case is excluded
    /// from future sector documents once this is set, so re-printing doesn't repeat already-requested names.</summary>
    public DateTimeOffset? SectorPdfGeneratedAt { get; set; }

    /// <summary>When this case was last included in the printed Penúltimas Carpetas document.
    /// Tracked independently of SectorPdfGeneratedAt so printing one document doesn't affect the
    /// other's "already printed" state. Null means it has never been printed there.</summary>
    public DateTimeOffset? PenultimasCarpetasPdfGeneratedAt { get; set; }

    /// <summary>Physical location of the folder, derived from the última-carpeta date. Null until the date is entered.</summary>
    public FolderSector? Sector => FechaUltimaCarpeta is { } fecha
        ? fecha < new DateOnly(2023, 7, 1) ? FolderSector.Archivo : FolderSector.Oficina43
        : null;

    /// <summary>Operator-ticked flag: the physical folder could not be located, so the case is
    /// handled through the F8 process instead of the normal upload flow.</summary>
    public bool FolderNotFound { get; set; }

    /// <summary>Operator-ticked flag: the physical folder is pending retrieval. Purely a visual
    /// work-in-progress marker (highlights the row yellow) — independent of Marked/FolderNotFound
    /// and has no effect on the routing/confirmation flow or sector-PDF selection.</summary>
    public bool PendienteCarpeta { get; set; }

    /// <summary>Manually-entered F8 case code, editable by the operator at any time — free text, no fixed format enforced.</summary>
    public string? CodigoF8 { get; set; }

    /// <summary>Which dedicated screen this case was transferred to, if any. None means it still
    /// shows in Casos (Index) — ticking the FolderNotFound checkbox alone does not change this,
    /// only clicking "Traspaso a F8" does.</summary>
    public CaseDestination Destination { get; set; } = CaseDestination.None;

    /// <summary>When the operator confirmed the transfer to whichever Destination is set. Null means
    /// not yet transferred. Once set, the case is excluded from Casos (Index) and shows only on its
    /// Destination's dedicated screen.</summary>
    public DateTimeOffset? TransferredAt { get; set; }
}

public enum CaseDestination
{
    /// <summary>Not yet transferred out of Casos — default for every case.</summary>
    None,
    /// <summary>Transferred to the /F8 screen.</summary>
    F8
}
