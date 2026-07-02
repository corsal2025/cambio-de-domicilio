namespace OutlookComunaRouter.Configuration;

public sealed class RouterOptions
{
    public const string SectionName = "Router";

    public required EwsOptions Ews { get; set; }
    public required string MailboxAddress { get; set; }
    public required string OwnDomain { get; set; }
    public string SourceFolderName { get; set; } = "Para pedir";
    public int PollIntervalMinutes { get; set; } = 30;
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
