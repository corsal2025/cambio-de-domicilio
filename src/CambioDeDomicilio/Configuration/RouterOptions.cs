namespace CambioDeDomicilio.Configuration;

public sealed class RouterOptions
{
    public const string SectionName = "Router";

    public required EwsOptions Ews { get; set; }
    public required string MailboxAddress { get; set; }
    public required string OwnDomain { get; set; }
    public string SourceFolderName { get; set; } = "CARP. PARA PEDIR";
    public string ConfirmationFolderName { get; set; } = "CARP. YA SUBIDAS";

    /// <summary>Legal deadline to upload the folder, in business days from the request email's received date.</summary>
    public int PlazoDiasHabiles { get; set; } = 15;

    /// <summary>How far back (in days) each sync scans the mailbox inbox for non-delivery reports of
    /// confirmation emails. A bounce arrives within minutes to hours of the send, so a short window
    /// is enough; the ProcessedBounce tombstone prevents re-processing regardless.</summary>
    public int BounceLookbackDays { get; set; } = 14;
    public required string SqliteDbPath { get; set; }
    public required string ComunaDirectoryCsvPath { get; set; }
    public required string ReportCsvPath { get; set; }
    public required string NotificationEmailAddress { get; set; }

    public bool ToastNotificationsEnabled { get; set; } = true;
}

public sealed class EwsOptions
{
    public required string Url { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }
}
