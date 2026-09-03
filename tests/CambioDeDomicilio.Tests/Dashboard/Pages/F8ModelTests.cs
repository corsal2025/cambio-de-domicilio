using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Dashboard.Pages;
using CambioDeDomicilio.Directories;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Mail;
using CambioDeDomicilio.Notifications;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Routing;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Pages;

public class F8ModelTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"f8-page-test-{Guid.NewGuid():N}.db");
    private readonly string csvPath = Path.Combine(Path.GetTempPath(), $"f8-page-test-{Guid.NewGuid():N}.csv");
    private readonly IPersonRequestRepository repository;
    private readonly F8Model model;

    public F8ModelTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        repository.EnsureSchema();
        var discardedRepository = new DiscardedEmailRepository($"Data Source={dbPath}");
        discardedRepository.EnsureSchema();
        File.WriteAllText(csvPath, "Comuna,ContactEmail,Domain\nCatemu,rfloresc@municatemu.cl,municatemu.cl\n");

        var options = new RouterOptions
        {
            Ews = new EwsOptions { Url = "https://mail.munivalpo.cl/EWS/Exchange.asmx", Username = "u", Password = "p" },
            MailboxAddress = "cambiodedomicilio@munivalpo.cl",
            OwnDomain = "munivalpo.cl",
            SqliteDbPath = dbPath,
            ComunaDirectoryCsvPath = csvPath,
            ReportCsvPath = "unused-report.csv",
            NotificationEmailAddress = "raul.salazar1984@gmail.com"
        };

        var routingService = new AddressChangeRoutingService(
            repository,
            discardedRepository,
            new ComunaDirectory(),
            new NoOpMailSender(),
            new NoOpEmailMover(),
            [],
            options,
            NullLogger<AddressChangeRoutingService>.Instance);

        model = new F8Model(repository, routingService, options);
    }

    [Fact]
    public void OnGet_OnlyReturnsCasesMovedToF8()
    {
        var f8Id = repository.Insert(NewRequest("msg-1", "Persona F8"));
        repository.SetFolderNotFound(f8Id, true);
        repository.SetDestination(f8Id, CaseDestination.F8, DateTimeOffset.UtcNow);

        repository.Insert(NewRequest("msg-2", "Persona Normal"));

        model.OnGet();

        var result = Assert.Single(model.Cases);
        Assert.Equal("Persona F8", result.FullName);
    }

    [Fact]
    public void OnGet_FolderNotFoundButNotMovedToF8_IsExcluded()
    {
        // Ticking the F8 checkbox alone is not enough — only an explicit "Traspaso a F8"
        // (Index.OnPostTransferToF8) should make a case show up here.
        var id = repository.Insert(NewRequest("msg-1", "Persona F8 Pendiente"));
        repository.SetFolderNotFound(id, true);

        model.OnGet();

        Assert.Empty(model.Cases);
    }

    [Fact]
    public void OnGet_NoFolderNotFoundCases_ReturnsEmpty()
    {
        repository.Insert(NewRequest("msg-1", "Persona Normal"));

        model.OnGet();

        Assert.Empty(model.Cases);
    }

    [Fact]
    public void OnGet_FolderNotFoundAndMovedToF8_IsIncluded()
    {
        var id = repository.Insert(NewRequest("msg-1", "Persona F8 traspasada"));
        repository.SetFolderNotFound(id, true);
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);

        model.OnGet();

        var result = Assert.Single(model.Cases);
        Assert.Equal(id, result.Id);
    }

    [Fact]
    public void OnPostSetCodigoF8_SetsValueAndClearsWhenBlank()
    {
        var id = repository.Insert(NewRequest("msg-1", "Persona F8"));
        repository.SetFolderNotFound(id, true);

        model.OnPostSetCodigoF8(id, "F8-9999");
        Assert.Equal("F8-9999", repository.FindById(id)!.CodigoF8);

        model.OnPostSetCodigoF8(id, "   ");
        Assert.Null(repository.FindById(id)!.CodigoF8);
    }

    [Fact]
    public void OnPostUndoTransfer_ClearsMovedToF8At()
    {
        var id = repository.Insert(NewRequest("msg-1", "Persona F8 traspasada"));
        repository.SetFolderNotFound(id, true);
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);

        model.OnPostUndoTransfer(id);

        var stored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.None, stored.Destination);
        Assert.Null(stored.TransferredAt);
    }

    [Fact]
    public void OnPostToggleMarked_SetsAndClearsMarked()
    {
        var id = repository.Insert(NewRequest("msg-1", "Persona F8"));
        repository.SetFolderNotFound(id, true);

        model.OnPostToggleMarked(id, "on");
        Assert.True(repository.FindById(id)!.Marked);

        model.OnPostToggleMarked(id, null);
        Assert.False(repository.FindById(id)!.Marked);
    }

    [Fact]
    public void OnPostTogglePendienteCarpeta_SetsAndClearsFlag()
    {
        var id = repository.Insert(NewRequest("msg-1", "Persona F8"));
        repository.SetFolderNotFound(id, true);

        model.OnPostTogglePendienteCarpeta(id, "on");
        Assert.True(repository.FindById(id)!.PendienteCarpeta);

        model.OnPostTogglePendienteCarpeta(id, null);
        Assert.False(repository.FindById(id)!.PendienteCarpeta);
    }

    private static PersonRequest NewRequest(string sourceMessageId, string fullName) => new()
    {
        FullName = fullName,
        Rut = "18.785.387-7",
        Comuna = "Catemu",
        SourceMessageId = sourceMessageId,
        SourceSubject = "Solicitud de carpeta",
        SourceSender = "rfloresc@municatemu.cl",
        NeedsReview = false,
        Status = RequestStatus.Pending
    };

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
        if (File.Exists(csvPath))
        {
            File.Delete(csvPath);
        }
    }

    private sealed class NoOpMailSender : IMailSender
    {
        public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class NoOpEmailMover : IEmailMover
    {
        public Task<bool> MoveAndMarkUnreadAsync(string messageId, string sourceFolderDisplayName, string destinationFolderDisplayName, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }
}
