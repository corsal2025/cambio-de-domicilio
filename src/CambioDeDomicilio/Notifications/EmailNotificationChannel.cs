using Microsoft.Extensions.Logging;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Mail;

namespace CambioDeDomicilio.Notifications;

/// <summary>Works identically on PC and on a headless VPS — always fired regardless of the toast channel.</summary>
public sealed class EmailNotificationChannel(IMailSender mailSender, RouterOptions options, ILogger<EmailNotificationChannel> logger) : INotificationChannel
{
    public void NotifyConfirmationSent(string fullName, string rut, string comuna)
    {
        var (subject, body) = EmailTemplates.ConfirmationSentNotification(fullName, rut, comuna);
        // Fire-and-forget is intentional here: notification delivery must not block or fail the polling cycle.
        // The task is wrapped so a failed send is logged instead of surfacing as an unobserved task exception.
        _ = SendAndLogAsync(subject, body);
    }

    private async Task SendAndLogAsync(string subject, string body)
    {
        try
        {
            await mailSender.SendAsync(options.NotificationEmailAddress, subject, body, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Notification delivery is best-effort; never fail the pipeline because of it.
            logger.LogWarning(ex, "No se pudo enviar el correo de notificación");
        }
    }
}
