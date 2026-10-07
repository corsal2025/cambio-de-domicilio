using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Directories;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Mail;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Reporting;
using CambioDeDomicilio.Routing;
using Xunit;

namespace CambioDeDomicilio.Tests;

public class RouterWorkerTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"worker-test-{Guid.NewGuid():N}.db");
    private readonly string reportPath = Path.Combine(Path.GetTempPath(), $"worker-report-{Guid.NewGuid():N}.csv");
    private readonly IPersonRequestRepository repository;
    private readonly FakeEmailReader emailReader = new();
    private readonly FakeCsvReportWriter reportWriter = new();
    private readonly ListLogger<RouterWorker> logger = new();

    public RouterWorkerTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        TestDatabase.Migrate(dbPath);
    }

    [Fact]
    public async Task RunCycleAsync_MissingComunaDirectory_SkipsCycleAndLogsCritical()
    {
        var sut = BuildWorker(comunaDirectoryCsvPath: "no-existe.csv");

        var ran = await sut.RunCycleAsync(CancellationToken.None);

        Assert.True(ran);
        Assert.False(emailReader.WasCalled);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task RunCycleAsync_ComunaDirectoryPresent_ProcessesFolders()
    {
        var csvPath = Path.Combine(Path.GetTempPath(), $"worker-comunas-{Guid.NewGuid():N}.csv");
        File.WriteAllText(csvPath, "Comuna,ContactEmail,Domain\nCatemu,rfloresc@municatemu.cl,municatemu.cl\n");
        var sut = BuildWorker(comunaDirectoryCsvPath: csvPath);

        var ran = await sut.RunCycleAsync(CancellationToken.None);

        Assert.True(ran);
        Assert.True(emailReader.WasCalled);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Critical);
        File.Delete(csvPath);
    }

    [Fact]
    public async Task RunCycleAsync_CalledWhileAnotherCycleInFlight_SecondCallReturnsFalse()
    {
        var csvPath = Path.Combine(Path.GetTempPath(), $"worker-comunas-{Guid.NewGuid():N}.csv");
        File.WriteAllText(csvPath, "Comuna,ContactEmail,Domain\nCatemu,rfloresc@municatemu.cl,municatemu.cl\n");
        var blockingReader = new BlockingEmailReader();
        var sut = BuildWorker(comunaDirectoryCsvPath: csvPath, emailReaderOverride: blockingReader);

        var firstCall = sut.RunCycleAsync(CancellationToken.None);
        await blockingReader.EnteredFirstCall.Task; // first call is now blocked mid-cycle, holding the guard

        var secondResult = await sut.RunCycleAsync(CancellationToken.None);
        blockingReader.Release();
        var firstResult = await firstCall;

        Assert.False(secondResult);
        Assert.True(firstResult);
        File.Delete(csvPath);
    }

    [Fact]
    public async Task RunCycleAsync_InboxHasBounceForConfirmedCase_FlagsTheCase()
    {
        var csvPath = Path.Combine(Path.GetTempPath(), $"worker-comunas-{Guid.NewGuid():N}.csv");
        File.WriteAllText(csvPath, "Comuna,ContactEmail,Domain\nCatemu,rfloresc@municatemu.cl,municatemu.cl\n");

        var confirmedId = repository.Insert(new PersonRequest
        {
            FullName = "JUAN PEREZ SOTO",
            Rut = "12.345.678-5",
            Comuna = "Catemu",
            SourceMessageId = "msg-1",
            SourceSubject = "Solicitud",
            SourceSender = "rfloresc@municatemu.cl",
            NeedsReview = false,
            Status = RequestStatus.Pending,
            ReceivedAt = DateTimeOffset.UtcNow
        });
        repository.UpdateStatusToConfirmed(confirmedId, DateTimeOffset.UtcNow);

        emailReader.InboxMessages.Add(new IncomingEmail(
            "ndr-1", "c", "Undeliverable: Carpeta subida a Conaset",
            "postmaster@munivalpo.cl",
            "Your message could not be delivered.\nSe informa que la carpeta del contribuyente JUAN PEREZ SOTO, RUT 12.345.678-5, ya fue subida al sistema de Conaset.",
            DateTimeOffset.UtcNow));

        var sut = BuildWorker(comunaDirectoryCsvPath: csvPath);
        await sut.RunCycleAsync(CancellationToken.None);

        Assert.NotNull(repository.FindById(confirmedId)!.ConfirmationBouncedAt);
        File.Delete(csvPath);
    }

    private RouterWorker BuildWorker(string comunaDirectoryCsvPath, IEmailReader? emailReaderOverride = null)
    {
        var options = new RouterOptions
        {
            Ews = new EwsOptions { Url = "https://mail.munivalpo.cl/EWS/Exchange.asmx", Username = "u", Password = "p" },
            MailboxAddress = "cambiodedomicilio@munivalpo.cl",
            OwnDomain = "munivalpo.cl",
            SourceFolderName = "CARP. PARA PEDIR",
            ConfirmationFolderName = "CARP. YA SUBIDAS",
            SqliteDbPath = dbPath,
            ComunaDirectoryCsvPath = comunaDirectoryCsvPath,
            ReportCsvPath = reportPath,
            NotificationEmailAddress = "raul.salazar1984@gmail.com"
        };

        var discardedRepository = new DiscardedEmailRepository($"Data Source={dbPath}");
        TestDatabase.Migrate(dbPath);

        var routingService = new AddressChangeRoutingService(
            repository,
            discardedRepository,
            new ComunaDirectory(),
            new FakeMailSender(),
            new NoOpEmailMover(),
            [],
            options,
            NullLogger<AddressChangeRoutingService>.Instance);

        return new RouterWorker(routingService, emailReaderOverride ?? emailReader, repository, reportWriter, options, logger);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(dbPath);
        File.Delete(reportPath);
    }

    private sealed class FakeEmailReader : IEmailReader
    {
        public bool WasCalled { get; private set; }
        public List<IncomingEmail> InboxMessages { get; } = [];

        public Task<IReadOnlyList<IncomingEmail>> GetMessagesInFolderAsync(string folderDisplayName, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult<IReadOnlyList<IncomingEmail>>([]);
        }

        public Task<IReadOnlyList<IncomingEmail>> GetInboxMessagesSinceAsync(DateTimeOffset receivedSince, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult<IReadOnlyList<IncomingEmail>>(InboxMessages);
        }
    }

    private sealed class BlockingEmailReader : IEmailReader
    {
        private readonly TaskCompletionSource gate = new();
        public TaskCompletionSource EnteredFirstCall { get; } = new();

        public async Task<IReadOnlyList<IncomingEmail>> GetMessagesInFolderAsync(string folderDisplayName, CancellationToken cancellationToken)
        {
            EnteredFirstCall.TrySetResult();
            await gate.Task;
            return [];
        }

        public Task<IReadOnlyList<IncomingEmail>> GetInboxMessagesSinceAsync(DateTimeOffset receivedSince, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IncomingEmail>>([]);

        public void Release() => gate.TrySetResult();
    }

    private sealed class FakeCsvReportWriter : ICsvReportWriter
    {
        public void Write(IReadOnlyList<PersonRequest> requests, string outputPath)
        {
        }
    }

    private sealed class FakeMailSender : IMailSender
    {
        public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class NoOpEmailMover : IEmailMover
    {
        public Task<bool> MoveAndMarkUnreadAsync(string messageId, string sourceFolderDisplayName, string destinationFolderDisplayName, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
