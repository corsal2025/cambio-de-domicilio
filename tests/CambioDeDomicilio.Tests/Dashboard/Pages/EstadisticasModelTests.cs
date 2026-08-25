using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Dashboard.Pages;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Statistics;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Pages;

public class EstadisticasModelTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"estadisticas-page-test-{Guid.NewGuid():N}.db");
    private readonly IPersonRequestRepository repository;
    private readonly IDiscardedEmailRepository discardedRepository;
    private readonly EstadisticasModel model;

    public EstadisticasModelTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        discardedRepository = new DiscardedEmailRepository($"Data Source={dbPath}");

        repository.EnsureSchema();
        discardedRepository.EnsureSchema();

        var options = new RouterOptions
        {
            Ews = new Configuration.EwsOptions { Url = "https://mail.munivalpo.cl/EWS/Exchange.asmx", Username = "u", Password = "p" },
            MailboxAddress = "cambiodedomicilio@munivalpo.cl",
            OwnDomain = "munivalpo.cl",
            SqliteDbPath = dbPath,
            ComunaDirectoryCsvPath = "unused.csv",
            ReportCsvPath = "unused-report.csv",
            NotificationEmailAddress = "raul.salazar1984@gmail.com"
        };

        var statisticsService = new StatisticsService(options);
        model = new EstadisticasModel(repository, discardedRepository, statisticsService);
    }

    [Fact]
    public void OnGet_PopulatesEveryDtoFromRepositoryData()
    {
        repository.Insert(NewRequest("m1", status: RequestStatus.Pending));
        repository.Insert(NewRequest("m2", status: RequestStatus.Confirmed, confirmedAt: DateTimeOffset.UtcNow));
        discardedRepository.Insert(new DiscardedEmail
        {
            SourceMessageId = "d1",
            SourceSubject = "asunto",
            SourceSender = "alguien@algunacomuna.cl",
            Reason = "Dominio desconocido"
        });

        model.OnGet();

        Assert.Equal(1, model.StatusCounts.Pending);
        Assert.Equal(1, model.StatusCounts.Confirmed);
        Assert.NotNull(model.WeeklyIntake);
        Assert.NotNull(model.TopComunas);
        Assert.True(model.Turnaround.HasData);
        Assert.NotNull(model.SectorDistribution);
        Assert.NotNull(model.F8DeadlineBacklog);
        Assert.NotNull(model.F8PdfStatus);
        Assert.NotNull(model.CertificadoFolderStatus);
        Assert.NotNull(model.CertificadoNotificationStatus);
        Assert.Single(model.DiscardedByReason);
    }

    private static PersonRequest NewRequest(string sourceMessageId, RequestStatus status, DateTimeOffset? confirmedAt = null) => new()
    {
        FullName = "GUSTAVO ANDRÉS PEÑA CASTRO",
        Rut = "18.785.387-7",
        Comuna = "Catemu",
        SourceMessageId = sourceMessageId,
        SourceSubject = "Solicitud de carpeta",
        SourceSender = "rfloresc@municatemu.cl",
        NeedsReview = false,
        Status = status,
        ReceivedAt = DateTimeOffset.UtcNow.AddDays(-5),
        ConfirmedAt = confirmedAt
    };

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(dbPath);
    }
}
