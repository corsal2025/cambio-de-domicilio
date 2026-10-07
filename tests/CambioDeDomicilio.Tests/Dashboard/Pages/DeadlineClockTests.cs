using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Statistics;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Pages;

/// <summary>Deadline math must depend on an injected clock, not on the machine's current date.</summary>
public class DeadlineClockTests
{
    private static readonly RouterOptions Options = new()
    {
        Ews = new EwsOptions { Url = "https://mail.example/EWS/Exchange.asmx", Username = "u", Password = "p" },
        MailboxAddress = "m@example.com",
        OwnDomain = "example.com",
        SqliteDbPath = "unused.db",
        ComunaDirectoryCsvPath = "unused.csv",
        ReportCsvPath = "unused.csv",
        NotificationEmailAddress = "ops@example.com",
        PlazoDiasHabiles = 15
    };

    [Fact]
    public void GetF8DeadlineBacklog_UsesInjectedClock()
    {
        // Received Monday 2026-01-05 → deadline 15 business days later = Monday 2026-01-26.
        var received = new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero).ToLocalTime();
        var f8Case = new PersonRequest { SourceMessageId = "m", SourceSubject = "s", SourceSender = "a@b.cl", Destination = CaseDestination.F8, ReceivedAt = received };

        var onTime = new StatisticsService(Options, new FixedClock(new DateTimeOffset(2026, 1, 26, 12, 0, 0, TimeSpan.Zero)))
            .GetF8DeadlineBacklog([f8Case]);
        var overdue = new StatisticsService(Options, new FixedClock(new DateTimeOffset(2026, 1, 27, 12, 0, 0, TimeSpan.Zero)))
            .GetF8DeadlineBacklog([f8Case]);

        Assert.Equal(new F8DeadlineBacklog(1, 0), onTime);
        Assert.Equal(new F8DeadlineBacklog(0, 1), overdue);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }
}
