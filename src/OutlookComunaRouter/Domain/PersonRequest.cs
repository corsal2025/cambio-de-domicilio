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

    /// <summary>Physical location of the folder, derived from the última-carpeta date. Null until the date is entered.</summary>
    public FolderSector? Sector => FechaUltimaCarpeta is { } fecha
        ? fecha < new DateOnly(2023, 7, 1) ? FolderSector.Archivo : FolderSector.Oficina43
        : null;
}
