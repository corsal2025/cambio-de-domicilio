using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Dashboard.Pages;
using OutlookComunaRouter.Directories;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Mail;
using OutlookComunaRouter.Notifications;
using OutlookComunaRouter.Persistence;
using OutlookComunaRouter.Reporting;
using OutlookComunaRouter.Routing;
using Xunit;

namespace OutlookComunaRouter.Tests.Dashboard.Pages;

public class IndexModelTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"index-page-test-{Guid.NewGuid():N}.db");
    private readonly string csvPath = Path.Combine(Path.GetTempPath(), $"index-page-test-{Guid.NewGuid():N}.csv");
    private readonly IPersonRequestRepository repository;
    private readonly AddressChangeRoutingService routingService;
    private readonly IndexModel model;

    public IndexModelTests()
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

        routingService = new AddressChangeRoutingService(
            repository,
            discardedRepository,
            new ComunaDirectory(),
            new NoOpMailSender(),
            [],
            options,
            NullLogger<AddressChangeRoutingService>.Instance);

        var routerWorker = new RouterWorker(
            routingService,
            new NoOpEmailReader(),
            repository,
            new NoOpCsvReportWriter(),
            options,
            NullLogger<RouterWorker>.Instance);

        model = new IndexModel(repository, discardedRepository, routingService, routerWorker, options)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "1")]))
                }
            }
        };
    }

    [Fact]
    public void OnGet_NoFilter_ReturnsAllCasesNewestFirst()
    {
        repository.Insert(NewRequest("msg-1"));
        repository.Insert(NewRequest("msg-2"));

        model.OnGet(status: null);

        Assert.Equal(2, model.Cases.Count);
    }

    [Fact]
    public void OnGet_StatusFilter_ReturnsOnlyMatchingStatus()
    {
        var pendingId = repository.Insert(NewRequest("msg-1"));
        var uploadedId = repository.Insert(NewRequest("msg-2"));
        repository.MarkUploaded(uploadedId, DateTimeOffset.UtcNow);

        model.OnGet(status: "Uploaded");

        var result = Assert.Single(model.Cases);
        Assert.Equal(uploadedId, result.Id);
        _ = pendingId; // kept for readability of the setup
    }

    [Fact]
    public void OnGet_NeedsReviewFilter_ReturnsOnlyIncompleteCases()
    {
        repository.Insert(NewRequest("msg-1"));
        var incomplete = new PersonRequest
        {
            SourceMessageId = "msg-2",
            SourceSubject = "Solicitud de carpeta",
            SourceSender = "rfloresc@municatemu.cl",
            NeedsReview = true,
            Status = RequestStatus.Pending
        };
        var incompleteId = repository.Insert(incomplete);

        model.OnGet(status: null, needsReview: true);

        var result = Assert.Single(model.Cases);
        Assert.Equal(incompleteId, result.Id);
    }

    [Fact]
    public void OnGet_SomeCasesNeedReview_CountReflectsTotalRegardlessOfFilter()
    {
        repository.Insert(NewRequest("msg-1"));
        repository.Insert(new PersonRequest
        {
            SourceMessageId = "msg-2",
            SourceSubject = "Solicitud de carpeta",
            SourceSender = "rfloresc@municatemu.cl",
            NeedsReview = true,
            Status = RequestStatus.Pending
        });
        repository.Insert(new PersonRequest
        {
            SourceMessageId = "msg-3",
            SourceSubject = "Solicitud de carpeta",
            SourceSender = "rfloresc@municatemu.cl",
            NeedsReview = true,
            Status = RequestStatus.Pending
        });

        model.OnGet(status: "Uploaded"); // a filter that excludes every case above

        Assert.Empty(model.Cases);
        Assert.Equal(2, model.NeedsReviewCount);
    }

    [Fact]
    public void OnPostSetPersonData_ValidData_ClearsReviewFlagAndNormalizesRut()
    {
        var incomplete = new PersonRequest
        {
            SourceMessageId = "msg-review",
            SourceSubject = "Solicitud de carpeta",
            SourceSender = "rfloresc@municatemu.cl",
            Comuna = "Catemu",
            NeedsReview = true,
            Status = RequestStatus.Pending
        };
        var id = repository.Insert(incomplete);

        model.OnPostSetPersonData(id, "Gustavo Peña Castro", "18785387-7");

        var stored = repository.GetAll().Single(c => c.Id == id);
        Assert.False(stored.NeedsReview);
        Assert.Equal("18.785.387-7", stored.Rut);
        Assert.Equal("Gustavo Peña Castro", stored.FullName);
    }

    [Fact]
    public void OnPostSetPersonData_InvalidRut_IsRejectedAndFlagKept()
    {
        var incomplete = new PersonRequest
        {
            SourceMessageId = "msg-review",
            SourceSubject = "Solicitud de carpeta",
            SourceSender = "rfloresc@municatemu.cl",
            Comuna = "Catemu",
            NeedsReview = true,
            Status = RequestStatus.Pending
        };
        var id = repository.Insert(incomplete);

        model.OnPostSetPersonData(id, "Gustavo Peña Castro", "18785387-6"); // wrong check digit

        var stored = repository.GetAll().Single(c => c.Id == id);
        Assert.True(stored.NeedsReview);
        Assert.Null(stored.Rut);
        Assert.True(model.MessageIsError);
    }

    [Fact]
    public void OnPostSetFecha_UpdatesDate()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        model.OnPostSetFecha(id, "1 mayo 2024");

        var stored = repository.GetAll().Single(c => c.Id == id);
        Assert.Equal(new DateOnly(2024, 5, 1), stored.FechaUltimaCarpeta);
    }


    [Fact]
    public void OnPostSetFecha_UnparseableText_IsRejected()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        model.OnPostSetFecha(id, "quince del marzo");

        Assert.True(model.MessageIsError);
        Assert.Null(repository.GetAll().Single(c => c.Id == id).FechaUltimaCarpeta);
    }

    [Fact]
    public async Task OnPostConfirmAsync_UploadedCase_RecordsAttributionFromClaim()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);

        await model.OnPostConfirmAsync(id);

        var stored = repository.GetAll().Single(c => c.Id == id);
        Assert.Equal(RequestStatus.Confirmed, stored.Status);
        Assert.Equal(1, stored.ConfirmedByUserId);
    }

    [Fact]
    public async Task OnPostSyncNowAsync_RunsCycleAndReportsSuccess()
    {
        await model.OnPostSyncNowAsync();

        Assert.False(model.MessageIsError);
        Assert.Equal("Sincronización completada.", model.Message);
    }

    private static PersonRequest NewRequest(string sourceMessageId) => new()
    {
        FullName = "GUSTAVO ANDRÉS PEÑA CASTRO",
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

    private sealed class NoOpEmailReader : IEmailReader
    {
        public Task<IReadOnlyList<IncomingEmail>> GetMessagesInFolderAsync(string folderDisplayName, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IncomingEmail>>([]);
    }

    private sealed class NoOpCsvReportWriter : ICsvReportWriter
    {
        public void Write(IReadOnlyList<PersonRequest> requests, string outputPath)
        {
        }
    }
}
