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
    public int PollIntervalMinutes { get; set; } = 30;
    public required string SqliteDbPath { get; set; }
    public required string ComunaDirectoryCsvPath { get; set; }
    public required string ReportCsvPath { get; set; }
    public required string NotificationEmailAddress { get; set; }

    public bool ToastNotificationsEnabled { get; set; } = true;

    /// <summary>Base URL used to build password-reset links in emails — adjust per machine if the
    /// dashboard is reached via a LAN hostname instead of localhost.</summary>
    public string PublicBaseUrl { get; set; } = "https://localhost:5001";
}

public sealed class EwsOptions
{
    public required string Url { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }
}
