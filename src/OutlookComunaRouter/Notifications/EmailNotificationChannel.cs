using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Graph;

namespace OutlookComunaRouter.Notifications;

/// <summary>Works identically on PC and on a headless VPS — always fired regardless of the toast channel.</summary>
public sealed class EmailNotificationChannel(IMailSender mailSender, RouterOptions options) : INotificationChannel
{
    public void NotifyResponded(string fullName, string rut, string comuna)
    {
        var (subject, body) = EmailTemplates.ReplyNotification(fullName, rut, comuna);
        // Fire-and-forget is intentional here: notification delivery must not block or fail the polling cycle.
        _ = mailSender.SendAsync(options.NotificationEmailAddress, subject, body, CancellationToken.None);
    }
}
