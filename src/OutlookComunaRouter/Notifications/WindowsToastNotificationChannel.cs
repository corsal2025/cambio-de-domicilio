using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OutlookComunaRouter.Configuration;

namespace OutlookComunaRouter.Notifications;

/// <summary>
/// Shows a Windows toast via a native PowerShell/WinRT call, with no third-party
/// dependency (avoids pinning the whole project to a Windows-only target framework).
/// No-op on non-Windows or when disabled via configuration — this is the channel that
/// has no effect on a headless VPS deployment; use <see cref="EmailNotificationChannel"/> for that case.
/// </summary>
public sealed class WindowsToastNotificationChannel(RouterOptions options, ILogger<WindowsToastNotificationChannel> logger) : INotificationChannel
{
    public void NotifyResponded(string fullName, string rut, string comuna)
    {
        if (!options.ToastNotificationsEnabled || !OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var title = Escape("Respuesta de comuna recibida");
            var message = Escape($"{fullName} (RUT {rut}) - {comuna}");

            var script = $$"""
                [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null
                [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom, ContentType = WindowsRuntime] > $null
                $template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
                $texts = $template.GetElementsByTagName('text')
                $texts.Item(0).AppendChild($template.CreateTextNode('{{title}}')) > $null
                $texts.Item(1).AppendChild($template.CreateTextNode('{{message}}')) > $null
                $toast = [Windows.UI.Notifications.ToastNotification]::new($template)
                [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('OutlookComunaRouter').Show($toast)
                """;

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -Command -",
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return;
            }

            process.StandardInput.Write(script);
            process.StandardInput.Close();
            process.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            // Toast delivery is best-effort; never fail the pipeline because of it.
            logger.LogWarning(ex, "No se pudo mostrar la notificación en pantalla");
        }
    }

    private static string Escape(string value) => value.Replace("'", "''");
}
