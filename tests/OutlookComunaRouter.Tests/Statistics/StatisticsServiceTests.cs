using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Statistics;
using Xunit;

namespace OutlookComunaRouter.Tests.Statistics;

public class StatisticsServiceTests
{
    private static readonly RouterOptions Options = new()
    {
        Ews = new EwsOptions { Url = "https://mail.munivalpo.cl/EWS/Exchange.asmx", Username = "u", Password = "p" },
        MailboxAddress = "cambiodedomicilio@munivalpo.cl",
        OwnDomain = "munivalpo.cl",
        SqliteDbPath = "unused.db",
        ComunaDirectoryCsvPath = "unused.csv",
        ReportCsvPath = "unused-report.csv",
        NotificationEmailAddress = "raul.salazar1984@gmail.com",
        PlazoDiasHabiles = 15
    };
    private readonly StatisticsService service = new(Options);

    private static PersonRequest NewRequest(
        string sourceMessageId,
        string comuna = "Catemu",
        RequestStatus status = RequestStatus.Pending,
        DateTimeOffset? receivedAt = null,
        DateTimeOffset? confirmedAt = null,
        DateOnly? fechaUltimaCarpeta = null,
        CaseDestination destination = CaseDestination.None,
        bool folderNotFound = false,
        DateTimeOffset? certificadoNotifiedAt = null,
        DateTimeOffset? sectorPdfGeneratedAt = null) => new()
    {
        FullName = "GUSTAVO ANDRÉS PEÑA CASTRO",
        Rut = "18.785.387-7",
        Comuna = comuna,
        SourceMessageId = sourceMessageId,
        SourceSubject = "Solicitud de carpeta",
        SourceSender = "rfloresc@municatemu.cl",
        NeedsReview = false,
        Status = status,
        ReceivedAt = receivedAt ?? DateTimeOffset.UtcNow,
        ConfirmedAt = confirmedAt,
        FechaUltimaCarpeta = fechaUltimaCarpeta,
        Destination = destination,
        FolderNotFound = folderNotFound,
        CertificadoNotifiedAt = certificadoNotifiedAt,
        SectorPdfGeneratedAt = sectorPdfGeneratedAt
    };

    [Fact]
    public void GetStatusCounts_ReturnsCorrectCountsPerStatus()
    {
        var cases = new[]
        {
            NewRequest("m1", status: RequestStatus.Pending),
            NewRequest("m2", status: RequestStatus.Pending),
            NewRequest("m3", status: RequestStatus.Pending),
            NewRequest("m4", status: RequestStatus.Uploaded),
            NewRequest("m5", status: RequestStatus.Uploaded),
            NewRequest("m6", status: RequestStatus.Confirmed),
            NewRequest("m7", status: RequestStatus.Confirmed),
            NewRequest("m8", status: RequestStatus.Confirmed),
            NewRequest("m9", status: RequestStatus.Confirmed),
            NewRequest("m10", status: RequestStatus.Confirmed),
        };

        var result = service.GetStatusCounts(cases);

        Assert.Equal(3, result.Pending);
        Assert.Equal(2, result.Uploaded);
        Assert.Equal(5, result.Confirmed);
    }

    [Fact]
    public void GetWeeklyIntake_IncludesZeroCountWeeksBetweenEarliestAndLatest()
    {
        var week1Start = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero); // Monday
        var week3Start = week1Start.AddDays(14); // two weeks later, week 2 has zero cases

        var cases = new[]
        {
            NewRequest("m1", receivedAt: week1Start),
            NewRequest("m2", receivedAt: week3Start),
        };

        var result = service.GetWeeklyIntake(cases);

        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].Count);
        Assert.Equal(0, result[1].Count);
        Assert.Equal(1, result[2].Count);
    }

    [Fact]
    public void GetTopComunas_OrdersDescendingAndCapsAtTen()
    {
        var cases = new List<PersonRequest>();
        for (var i = 0; i < 5; i++) cases.Add(NewRequest($"a{i}", comuna: "Viña del Mar"));
        for (var i = 0; i < 3; i++) cases.Add(NewRequest($"b{i}", comuna: "Quilpué"));
        cases.Add(NewRequest("c0", comuna: "Casablanca"));

        var result = service.GetTopComunas(cases);

        Assert.Equal(3, result.Count);
        Assert.Equal("Viña del Mar", result[0].Comuna);
        Assert.Equal(5, result[0].Count);
        Assert.Equal("Quilpué", result[1].Comuna);
        Assert.Equal("Casablanca", result[2].Comuna);
    }

    [Fact]
    public void GetTopComunas_CapsAtTenDistinctComunas()
    {
        var cases = new List<PersonRequest>();
        for (var i = 0; i < 12; i++)
        {
            cases.Add(NewRequest($"m{i}", comuna: $"Comuna{i}"));
        }

        var result = service.GetTopComunas(cases);

        Assert.Equal(10, result.Count);
    }

    [Fact]
    public void GetDiscardedByReason_GroupsStablyAcrossCalls()
    {
        var emails = new[]
        {
            NewDiscarded("d1", "Dominio desconocido"),
            NewDiscarded("d2", "Dominio desconocido"),
            NewDiscarded("d3", "Sin comuna registrada"),
        };

        var first = service.GetDiscardedByReason(emails);
        var second = service.GetDiscardedByReason(emails);

        Assert.Equal(first.Select(r => (r.Reason, r.Count)), second.Select(r => (r.Reason, r.Count)));
        Assert.Contains(first, r => r.Reason == "Dominio desconocido" && r.Count == 2);
        Assert.Contains(first, r => r.Reason == "Sin comuna registrada" && r.Count == 1);
    }

    private static DiscardedEmail NewDiscarded(string sourceMessageId, string reason) => new()
    {
        SourceMessageId = sourceMessageId,
        SourceSubject = "asunto",
        SourceSender = "alguien@algunacomuna.cl",
        Reason = reason
    };

    [Fact]
    public void GetAverageTurnaroundDays_OnlyCountsConfirmedCases()
    {
        var received = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var cases = new[]
        {
            NewRequest("m1", status: RequestStatus.Confirmed, receivedAt: received, confirmedAt: received.AddDays(4)),
            NewRequest("m2", status: RequestStatus.Confirmed, receivedAt: received, confirmedAt: received.AddDays(6)),
            NewRequest("m3", status: RequestStatus.Pending, receivedAt: received, confirmedAt: null),
            NewRequest("m4", status: RequestStatus.Uploaded, receivedAt: received, confirmedAt: null),
        };

        var result = service.GetAverageTurnaroundDays(cases);

        Assert.True(result.HasData);
        Assert.Equal(5.0, result.AverageDays);
    }

    [Fact]
    public void GetAverageTurnaroundDays_NoConfirmedCases_ReturnsNoDataInsteadOfZero()
    {
        var cases = new[]
        {
            NewRequest("m1", status: RequestStatus.Pending),
            NewRequest("m2", status: RequestStatus.Uploaded),
        };

        var result = service.GetAverageTurnaroundDays(cases);

        Assert.False(result.HasData);
    }

    [Fact]
    public void GetSectorDistribution_ExcludesCasesWithoutFecha()
    {
        var cases = new[]
        {
            NewRequest("m1", fechaUltimaCarpeta: new DateOnly(2022, 1, 1)), // Archivo
            NewRequest("m2", fechaUltimaCarpeta: new DateOnly(2024, 1, 1)), // Oficina43
            NewRequest("m3", fechaUltimaCarpeta: new DateOnly(2024, 6, 1)), // Oficina43
            NewRequest("m4", fechaUltimaCarpeta: null),
        };

        var result = service.GetSectorDistribution(cases);

        Assert.Equal(1, result.Archivo);
        Assert.Equal(2, result.Oficina43);
    }

    [Fact]
    public void GetF8DeadlineBacklog_BucketsUsingSameRuleAsF8Badge()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var overdueReceived = today.AddDays(-40).ToDateTime(TimeOnly.MinValue); // well past 15 business days
        var freshReceived = today.ToDateTime(TimeOnly.MinValue); // just received, plenty of time left

        var cases = new[]
        {
            NewRequest("m1", destination: CaseDestination.F8, receivedAt: overdueReceived),
            NewRequest("m2", destination: CaseDestination.F8, receivedAt: freshReceived),
            NewRequest("m3", destination: CaseDestination.None, receivedAt: overdueReceived), // not F8, excluded
        };

        var result = service.GetF8DeadlineBacklog(cases);

        Assert.Equal(1, result.WithinDeadline);
        Assert.Equal(1, result.PastDeadline);
    }

    [Fact]
    public void GetF8PdfStatus_SplitsGeneratedVsPending()
    {
        var cases = new[]
        {
            NewRequest("m1", destination: CaseDestination.F8, fechaUltimaCarpeta: new DateOnly(2024, 1, 1), sectorPdfGeneratedAt: DateTimeOffset.UtcNow),
            NewRequest("m2", destination: CaseDestination.F8, fechaUltimaCarpeta: new DateOnly(2024, 1, 1), sectorPdfGeneratedAt: null),
            NewRequest("m3", destination: CaseDestination.F8, fechaUltimaCarpeta: null, sectorPdfGeneratedAt: null), // no sector assigned, excluded
        };

        var result = service.GetF8PdfStatus(cases);

        Assert.Equal(1, result.Generated);
        Assert.Equal(1, result.Pending);
    }

    [Fact]
    public void GetCertificadoFolderStatus_SplitsByFolderNotFound()
    {
        var cases = new[]
        {
            NewRequest("m1", destination: CaseDestination.Certificado, folderNotFound: false),
            NewRequest("m2", destination: CaseDestination.Certificado, folderNotFound: true),
            NewRequest("m3", destination: CaseDestination.Certificado, folderNotFound: true),
            NewRequest("m4", destination: CaseDestination.F8, folderNotFound: true), // not Certificado, excluded
        };

        var result = service.GetCertificadoFolderStatus(cases);

        Assert.Equal(1, result.Found);
        Assert.Equal(2, result.NotFound);
    }

    [Fact]
    public void GetCertificadoNotificationStatus_SplitsByNotifiedAt()
    {
        var cases = new[]
        {
            NewRequest("m1", destination: CaseDestination.Certificado, certificadoNotifiedAt: DateTimeOffset.UtcNow),
            NewRequest("m2", destination: CaseDestination.Certificado, certificadoNotifiedAt: null),
            NewRequest("m3", destination: CaseDestination.Certificado, certificadoNotifiedAt: null),
        };

        var result = service.GetCertificadoNotificationStatus(cases);

        Assert.Equal(1, result.Notified);
        Assert.Equal(2, result.Pending);
    }
}
