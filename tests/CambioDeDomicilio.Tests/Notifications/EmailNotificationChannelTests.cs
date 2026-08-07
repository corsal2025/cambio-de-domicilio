using Microsoft.Extensions.Logging;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Mail;
using CambioDeDomicilio.Notifications;
using Xunit;

namespace CambioDeDomicilio.Tests.Notifications;

public class EmailNotificationChannelTests
{
    private static RouterOptions BuildOptions() => new()
    {
        Ews = new EwsOptions { Url = "https://ews.example.com/EWS/Exchange.asmx", Username = "u", Password = "p" },
        MailboxAddress = "mailbox@example.com",
        OwnDomain = "example.com",
        SqliteDbPath = "unused.db",
        ComunaDirectoryCsvPath = "unused.csv",
        ReportCsvPath = "unused-report.csv",
        NotificationEmailAddress = "notify@example.com"
    };

    private sealed class FailingMailSender : IMailSender
    {
        public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("SMTP unreachable");
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }

    [Fact]
    public async Task NotifyConfirmationSent_MailSenderThrows_LogsWarningInsteadOfThrowing()
    {
        var logger = new ListLogger<EmailNotificationChannel>();
        var channel = new EmailNotificationChannel(new FailingMailSender(), BuildOptions(), logger);

        var exception = Record.Exception(() => channel.NotifyConfirmationSent("Juan Perez", "12345678-9", "Santiago"));
        Assert.Null(exception);

        // Give the fire-and-forget task a chance to run and be observed by the logger.
        await Task.Delay(200);

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.NotNull(warning.Exception);
        Assert.IsType<InvalidOperationException>(warning.Exception);
    }
}
