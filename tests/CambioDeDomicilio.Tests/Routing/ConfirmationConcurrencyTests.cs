using Microsoft.Extensions.Logging.Abstractions;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Directories;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Mail;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Routing;
using Xunit;

namespace CambioDeDomicilio.Tests.Routing;

/// <summary>A confirmation email is the only irreversible effect toward a third party, so a double
/// click (or two browser tabs) must never send it twice.</summary>
public class ConfirmationConcurrencyTests : IDisposable
{
    private static readonly IReadOnlyList<ComunaContact> Contacts =
    [
        new ComunaContact("Catemu", "rfloresc@municatemu.cl", "municatemu.cl")
    ];

    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"concurrency-test-{Guid.NewGuid():N}.db");
    private readonly IPersonRequestRepository repository;
    private readonly GatedMailSender mailSender = new();
    private readonly AddressChangeRoutingService sut;

    public ConfirmationConcurrencyTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        TestDatabase.Migrate(dbPath);
        var discardedRepository = new DiscardedEmailRepository($"Data Source={dbPath}");
        TestDatabase.Migrate(dbPath);

        var options = new RouterOptions
        {
            Ews = new EwsOptions { Url = "https://mail.example/EWS/Exchange.asmx", Username = "u", Password = "p" },
            MailboxAddress = "cambiodedomicilio@munivalpo.cl",
            OwnDomain = "munivalpo.cl",
            SqliteDbPath = dbPath,
            ComunaDirectoryCsvPath = "unused.csv",
            ReportCsvPath = "unused-report.csv",
            NotificationEmailAddress = "ops@example.com"
        };

        sut = new AddressChangeRoutingService(
            repository,
            discardedRepository,
            new MessageTombstoneRepository($"Data Source={dbPath}"),
            new ComunaDirectory(),
            mailSender,
            new NoopEmailMover(),
            [],
            options,
            NullLogger<AddressChangeRoutingService>.Instance);
    }

    [Fact]
    public async Task SendConfirmationAsync_ConcurrentCallsForSameCase_SendsOnlyOneEmail()
    {
        var id = InsertUploaded();

        var first = sut.SendConfirmationAsync(id, Contacts, CancellationToken.None);
        await mailSender.FirstSendStarted.Task; // first call is now inside SendAsync, case still Uploaded in the DB
        var second = await sut.SendConfirmationAsync(id, Contacts, CancellationToken.None);
        mailSender.Release.SetResult();
        var firstResult = await first;

        Assert.False(second.Sent);
        Assert.True(firstResult.Sent);
        Assert.Equal(1, mailSender.SendCount);
        Assert.Equal(RequestStatus.Confirmed, repository.FindById(id)!.Status);
    }

    [Fact]
    public async Task SendConfirmationAsync_SendFails_CaseStaysUploadedAndCanBeRetried()
    {
        var id = InsertUploaded();
        mailSender.Release.SetResult();
        mailSender.FailNextSend = true;

        var failed = await sut.SendConfirmationAsync(id, Contacts, CancellationToken.None);
        Assert.False(failed.Sent);
        Assert.Equal(RequestStatus.Uploaded, repository.FindById(id)!.Status);

        var retry = await sut.SendConfirmationAsync(id, Contacts, CancellationToken.None);

        Assert.True(retry.Sent);
        Assert.Equal(RequestStatus.Confirmed, repository.FindById(id)!.Status);
    }

    [Fact]
    public async Task MarkUploadedAndConfirmAsync_ConcurrentCallsForSameCase_SendsOnlyOneEmail()
    {
        var id = InsertPending();

        var first = sut.MarkUploadedAndConfirmAsync(id, Contacts, CancellationToken.None);
        await mailSender.FirstSendStarted.Task;
        var second = await sut.MarkUploadedAndConfirmAsync(id, Contacts, CancellationToken.None);
        mailSender.Release.SetResult();
        await first;

        Assert.False(second.Sent);
        Assert.Equal(1, mailSender.SendCount);
    }

    private long InsertPending()
    {
        var id = repository.Insert(new PersonRequest
        {
            FullName = "JUAN PEREZ SOTO",
            Rut = "18785387-7",
            Comuna = "Catemu",
            SourceMessageId = "msg-1",
            SourceSubject = "Solicitud de carpeta",
            SourceSender = "rfloresc@municatemu.cl",
            NeedsReview = false,
            Status = RequestStatus.Pending,
            ReceivedAt = DateTimeOffset.UtcNow
        });
        repository.SetFechaUltimaCarpeta(id, new DateOnly(2024, 3, 15));
        return id;
    }

    private long InsertUploaded()
    {
        var id = InsertPending();
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);
        return id;
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class GatedMailSender : IMailSender
    {
        private int sendCount;

        public int SendCount => sendCount;
        public bool FailNextSend { get; set; }
        public TaskCompletionSource FirstSendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref sendCount);
            FirstSendStarted.TrySetResult();
            await Release.Task;
            if (FailNextSend)
            {
                FailNextSend = false;
                throw new InvalidOperationException("EWS down");
            }
        }
    }

    private sealed class NoopEmailMover : IEmailMover
    {
        public Task<bool> MoveAndMarkUnreadAsync(string messageId, string sourceFolderDisplayName, string destinationFolderDisplayName, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }
}
