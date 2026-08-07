using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Dashboard.Auth;
using CambioDeDomicilio.Dashboard.Pages;
using CambioDeDomicilio.Directories;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Mail;
using CambioDeDomicilio.Notifications;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Routing;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Pages;

public class CertificadoModelTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"certificado-page-test-{Guid.NewGuid():N}.db");
    private readonly string csvPath = Path.Combine(Path.GetTempPath(), $"certificado-page-test-{Guid.NewGuid():N}.csv");
    private readonly IPersonRequestRepository repository;
    private readonly FakeMailSender mailSender = new();
    private readonly RouterOptions options;
    private readonly CertificadoModel model;

    public CertificadoModelTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        repository.EnsureSchema();
        var discardedRepository = new DiscardedEmailRepository($"Data Source={dbPath}");
        discardedRepository.EnsureSchema();
        var users = new UserRepository($"Data Source={dbPath}");
        users.EnsureSchema();
        File.WriteAllText(csvPath, "Comuna,ContactEmail,Domain\nCatemu,rfloresc@municatemu.cl,municatemu.cl\n");

        options = new RouterOptions
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
            users,
            [],
            options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AddressChangeRoutingService>.Instance);

        model = new CertificadoModel(repository, routingService, mailSender, options)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public void OnGet_OnlyReturnsCasesTransferredToCertificado()
    {
        var certId = repository.Insert(NewRequest("msg-1", "Persona Certificado"));
        repository.SetFolderNotFound(certId, true);
        repository.SetDestination(certId, CaseDestination.Certificado, DateTimeOffset.UtcNow);

        var f8Id = repository.Insert(NewRequest("msg-2", "Persona F8"));
        repository.SetFolderNotFound(f8Id, true);
        repository.SetDestination(f8Id, CaseDestination.F8, DateTimeOffset.UtcNow);

        repository.Insert(NewRequest("msg-3", "Persona Normal"));

        model.OnGet();

        var result = Assert.Single(model.Cases);
        Assert.Equal("Persona Certificado", result.FullName);
    }

    [Fact]
    public void OnPostSetFecha_ParsesAndStoresDate()
    {
        var id = repository.Insert(NewRequest("msg-1", "Persona"));
        repository.SetDestination(id, CaseDestination.Certificado, DateTimeOffset.UtcNow);

        model.OnPostSetFecha(id, "15/03/2024");

        Assert.Equal(new DateOnly(2024, 3, 15), repository.FindById(id)!.FechaUltimaCarpeta);
    }

    [Fact]
    public void OnPostToggleMarked_SetsAndClearsMarked()
    {
        var id = repository.Insert(NewRequest("msg-1", "Persona"));
        repository.SetDestination(id, CaseDestination.Certificado, DateTimeOffset.UtcNow);

        model.OnPostToggleMarked(id, "on");
        Assert.True(repository.FindById(id)!.Marked);

        model.OnPostToggleMarked(id, null);
        Assert.False(repository.FindById(id)!.Marked);
    }

    [Fact]
    public void OnPostTogglePendienteCarpeta_SetsAndClearsFlag()
    {
        var id = repository.Insert(NewRequest("msg-1", "Persona"));
        repository.SetDestination(id, CaseDestination.Certificado, DateTimeOffset.UtcNow);

        model.OnPostTogglePendienteCarpeta(id, "on");
        Assert.True(repository.FindById(id)!.PendienteCarpeta);

        model.OnPostTogglePendienteCarpeta(id, null);
        Assert.False(repository.FindById(id)!.PendienteCarpeta);
    }

    [Fact]
    public void OnPostUndoTransfer_ClearsDestination()
    {
        var id = repository.Insert(NewRequest("msg-1", "Persona"));
        repository.SetDestination(id, CaseDestination.Certificado, DateTimeOffset.UtcNow);

        model.OnPostUndoTransfer(id);

        var stored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.None, stored.Destination);
        Assert.Null(stored.TransferredAt);
    }

    [Fact]
    public async Task OnPostNotifyCertificadoAsync_NoPendingCases_ShowsErrorAndSendsNothing()
    {
        var result = await model.OnPostNotifyCertificadoAsync();

        Assert.True(model.MessageIsError);
        Assert.Empty(mailSender.SentMessages);
    }

    [Fact]
    public async Task OnPostNotifyCertificadoAsync_PendingCases_SendsBatchAndAcknowledgementAndMarksNotified()
    {
        var id = repository.Insert(NewRequest("msg-1", "Gustavo Peña Castro"));
        repository.SetDestination(id, CaseDestination.Certificado, DateTimeOffset.UtcNow);

        await model.OnPostNotifyCertificadoAsync();

        // One batch email to Secretaría Municipal + one acknowledgement to the comuna.
        Assert.Equal(2, mailSender.SentMessages.Count);
        Assert.Contains(mailSender.SentMessages, m => m.To == options.CertificateRequestEmailAddress);
        Assert.Contains(mailSender.SentMessages, m => m.To == "rfloresc@municatemu.cl");

        Assert.NotNull(repository.FindById(id)!.CertificadoNotifiedAt);
        Assert.False(model.MessageIsError);
    }

    [Fact]
    public async Task OnPostNotifyCertificadoAsync_AlreadyNotifiedCase_ExcludedFromNextBatch()
    {
        var id = repository.Insert(NewRequest("msg-1", "Persona Ya Avisada"));
        repository.SetDestination(id, CaseDestination.Certificado, DateTimeOffset.UtcNow);
        repository.SetCertificadoNotified(id, DateTimeOffset.UtcNow);

        var result = await model.OnPostNotifyCertificadoAsync();

        Assert.True(model.MessageIsError);
        Assert.Empty(mailSender.SentMessages);
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

    private sealed class FakeMailSender : IMailSender
    {
        public List<(string To, string Subject, string Body)> SentMessages { get; } = [];

        public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
        {
            SentMessages.Add((toAddress, subject, body));
            return Task.CompletedTask;
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
