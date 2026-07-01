namespace OutlookComunaRouter.Configuration;

public sealed class RouterOptions
{
    public const string SectionName = "Router";

    public required string TenantId { get; set; }
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
    public required string MailboxAddress { get; set; }
    public required string OwnDomain { get; set; }
    public int PollIntervalMinutes { get; set; } = 30;
    public required string SqliteDbPath { get; set; }
    public required string ComunaDirectoryCsvPath { get; set; }
    public required string ReportCsvPath { get; set; }
    public required string NotificationEmailAddress { get; set; }
    public bool ToastNotificationsEnabled { get; set; } = true;
}
