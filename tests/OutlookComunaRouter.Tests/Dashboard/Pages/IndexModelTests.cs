using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Dashboard.Auth;
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
        var users = new UserRepository($"Data Source={dbPath}");
        users.EnsureSchema();
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
            new NoOpEmailMover(),
            users,
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

        model = new IndexModel(repository, discardedRepository, routingService, routerWorker, options, NullLogger<IndexModel>.Instance)
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
    public void OnGet_NoFilter_OrdersByReceivedAtNewestFirst()
    {
        // Order must follow when the email actually arrived (ReceivedAt), not when the row was
        // inserted into the database (CreatedAt) — otherwise a case re-tracked later (e.g. after
        // being reverted) would jump to the top even though the original request is old.
        var older = NewRequest("msg-1");
        older.ReceivedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var newer = NewRequest("msg-2");
        newer.ReceivedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        repository.Insert(older);
        repository.Insert(newer);

        model.OnGet(status: null);

        Assert.Equal(2, model.Cases.Count);
        Assert.Equal("msg-2", model.Cases[0].SourceMessageId);
        Assert.Equal("msg-1", model.Cases[1].SourceMessageId);
    }

    [Fact]
    public void OnGet_MarkedCase_DoesNotAffectSortOrder()
    {
        // Marking is pure personal bookkeeping (highlights the row) — it must NOT move the case,
        // unlike Confirmed status which does sink to the end (see the Confirmed-sort test below).
        var newestMarked = NewRequest("msg-1");
        newestMarked.ReceivedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var olderUnmarked = NewRequest("msg-2");
        olderUnmarked.ReceivedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var markedId = repository.Insert(newestMarked);
        repository.Insert(olderUnmarked);
        repository.SetMarked(markedId, true);

        model.OnGet(status: null);

        Assert.Equal(2, model.Cases.Count);
        Assert.Equal("msg-1", model.Cases[0].SourceMessageId); // marked, but still newest -> stays on top
        Assert.Equal("msg-2", model.Cases[1].SourceMessageId);
    }

    [Fact]
    public void OnGet_ConfirmedCases_SortToTheEndOrderedByConfirmedAtAscending()
    {
        // Confirmed cases (blue row) sink below everything else, including marked-but-not-yet-
        // confirmed ones — and among themselves they order by when they were confirmed, not by
        // ReceivedAt, so the operator can see confirmations in the order they happened.
        var pending = NewRequest("msg-pending");
        pending.ReceivedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var confirmedFirst = NewRequest("msg-confirmed-first");
        confirmedFirst.ReceivedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var confirmedSecond = NewRequest("msg-confirmed-second");
        confirmedSecond.ReceivedAt = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);

        repository.Insert(pending);
        var firstId = repository.Insert(confirmedFirst);
        var secondId = repository.Insert(confirmedSecond);
        // Confirm "second" chronologically before "first" — ConfirmedAt order must win, not insert order.
        repository.UpdateStatusToConfirmed(secondId, new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), 1);
        repository.UpdateStatusToConfirmed(firstId, new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero), 1);

        model.OnGet(status: null);

        Assert.Equal(3, model.Cases.Count);
        Assert.Equal("msg-pending", model.Cases[0].SourceMessageId);
        Assert.Equal("msg-confirmed-second", model.Cases[1].SourceMessageId); // confirmed 07-01, earlier
        Assert.Equal("msg-confirmed-first", model.Cases[2].SourceMessageId); // confirmed 07-05, later
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
    public void OnPostSetPersonData_ValidData_ClearsReviewFlagAndNormalizesRutAndName()
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
        Assert.Equal("GUSTAVO PEÑA CASTRO", stored.FullName);
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
    public void OnPostSetFecha_EmptyValue_ClearsDateWithoutError()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        model.OnPostSetFecha(id, "1 mayo 2024");
        Assert.NotNull(repository.GetAll().Single(c => c.Id == id).FechaUltimaCarpeta);

        model.OnPostSetFecha(id, "");

        Assert.Null(repository.GetAll().Single(c => c.Id == id).FechaUltimaCarpeta);
        Assert.False(model.MessageIsError);
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
        repository.SetFechaUltimaCarpeta(id, new DateOnly(2024, 3, 15));
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);

        await model.OnPostConfirmAsync(id);

        var stored = repository.GetAll().Single(c => c.Id == id);
        Assert.Equal(RequestStatus.Confirmed, stored.Status);
        Assert.Equal(1, stored.ConfirmedByUserId);
    }

    [Fact]
    public async Task OnPostMarkUploadedAndConfirmAsync_PendingCase_MovesUpdatesAndConfirms()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetFechaUltimaCarpeta(id, new DateOnly(2024, 3, 15));

        await model.OnPostMarkUploadedAndConfirmAsync(id);

        var stored = repository.GetAll().Single(c => c.Id == id);
        Assert.Equal(RequestStatus.Confirmed, stored.Status);
        Assert.Equal(1, stored.ConfirmedByUserId);
        Assert.False(model.MessageIsError);
    }

    [Fact]
    public async Task OnPostRectifyConfirmationAsync_ConfirmedCase_RevertsToPending()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetFechaUltimaCarpeta(id, new DateOnly(2024, 3, 15));
        await model.OnPostMarkUploadedAndConfirmAsync(id);

        await model.OnPostRectifyConfirmationAsync(id);

        var stored = repository.GetAll().Single(c => c.Id == id);
        Assert.Equal(RequestStatus.Pending, stored.Status);
        Assert.False(model.MessageIsError);
    }

    [Fact]
    public void OnPostDeleteCase_RemovesTheCase()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        model.OnPostDeleteCase(id);

        Assert.Empty(repository.GetAll());
        Assert.Equal("Caso eliminado.", model.Message);
    }

    [Fact]
    public void OnPostDeleteCase_TombstonesTheSourceMessage_SoResyncCannotRecreateIt()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        model.OnPostDeleteCase(id);

        Assert.True(repository.IsSourceMessageDeleted("msg-1"));
    }

    [Fact]
    public void OnPostMarkAllVisible_MarkTrue_MarksAllCurrentlyVisibleCases()
    {
        var id1 = repository.Insert(NewRequest("msg-1"));
        var id2 = repository.Insert(NewRequest("msg-2"));

        model.OnPostMarkAllVisible(marked: true, status: null, needsReview: false, search: null);

        Assert.True(repository.GetAll().Single(c => c.Id == id1).Marked);
        Assert.True(repository.GetAll().Single(c => c.Id == id2).Marked);
    }

    [Fact]
    public void OnPostMarkAllVisible_RespectsStatusFilter()
    {
        var pendingId = repository.Insert(NewRequest("msg-1"));
        var uploadedId = repository.Insert(NewRequest("msg-2"));
        repository.MarkUploaded(uploadedId, DateTimeOffset.UtcNow);

        model.OnPostMarkAllVisible(marked: true, status: "Pending", needsReview: false, search: null);

        Assert.True(repository.GetAll().Single(c => c.Id == pendingId).Marked);
        Assert.False(repository.GetAll().Single(c => c.Id == uploadedId).Marked); // filtered out, untouched
    }

    [Fact]
    public void OnPostMarkAllVisible_MarkFalse_UnmarksAll()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetMarked(id, true);

        model.OnPostMarkAllVisible(marked: false, status: null, needsReview: false, search: null);

        Assert.False(repository.GetAll().Single(c => c.Id == id).Marked);
    }

    [Fact]
    public void OnPostMarkAllBySector_SelectsExactlyThatSector_UnmarkingEverythingElse()
    {
        var archivoId = repository.Insert(NewRequest("msg-1"));
        repository.SetFechaUltimaCarpeta(archivoId, new DateOnly(2022, 1, 1)); // Archivo

        var oficinaId = repository.Insert(NewRequest("msg-2"));
        repository.SetFechaUltimaCarpeta(oficinaId, new DateOnly(2024, 1, 1)); // Oficina43
        repository.SetMarked(oficinaId, true); // stale mark from earlier — must be cleared

        var noSectorId = repository.Insert(NewRequest("msg-3")); // no fecha -> no sector
        repository.SetMarked(noSectorId, true); // also stale — must be cleared

        // Status filter is irrelevant to this action — every Archivo case gets marked,
        // not just the ones currently visible under whatever filter is active.
        model.OnPostMarkAllBySector(FolderSector.Archivo, status: "Uploaded", needsReview: false, search: null);

        Assert.True(repository.GetAll().Single(c => c.Id == archivoId).Marked);
        Assert.False(repository.GetAll().Single(c => c.Id == oficinaId).Marked);
        Assert.False(repository.GetAll().Single(c => c.Id == noSectorId).Marked);
    }

    [Fact]
    public void OnPostMarkAllBySector_ExcludesAlreadyConfirmedCases()
    {
        // Confirmed (blue) cases are already done — selecting them again for a fresh print run
        // makes no sense, so they're skipped even though they belong to the sector.
        var pendingId = repository.Insert(NewRequest("msg-1"));
        repository.SetFechaUltimaCarpeta(pendingId, new DateOnly(2022, 1, 1)); // Archivo, still open

        var confirmedId = repository.Insert(NewRequest("msg-2"));
        repository.SetFechaUltimaCarpeta(confirmedId, new DateOnly(2022, 1, 1)); // Archivo, already confirmed
        repository.UpdateStatusToConfirmed(confirmedId, DateTimeOffset.UtcNow, confirmedByUserId: 1);

        model.OnPostMarkAllBySector(FolderSector.Archivo, status: null, needsReview: false, search: null);

        Assert.True(repository.GetAll().Single(c => c.Id == pendingId).Marked);
        Assert.False(repository.GetAll().Single(c => c.Id == confirmedId).Marked);
    }

    [Fact]
    public void AllVisibleMarked_TrueOnlyWhenEveryVisibleCaseIsMarked()
    {
        var id1 = repository.Insert(NewRequest("msg-1"));
        var id2 = repository.Insert(NewRequest("msg-2"));
        model.OnGet(status: null);
        Assert.False(model.AllVisibleMarked);

        repository.SetMarked(id1, true);
        repository.SetMarked(id2, true);
        model.OnGet(status: null);
        Assert.True(model.AllVisibleMarked);
    }

    [Fact]
    public void AllVisibleMarked_NoCases_IsFalse()
    {
        model.OnGet(status: null);

        Assert.False(model.AllVisibleMarked);
    }

    [Fact]
    public void OnPostToggleMarked_SetsMarkedFlag()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        model.OnPostToggleMarked(id, markedValue: "on");
        Assert.True(repository.GetAll().Single(c => c.Id == id).Marked);

        model.OnPostToggleMarked(id, markedValue: "");
        Assert.False(repository.GetAll().Single(c => c.Id == id).Marked);
    }

    [Fact]
    public async Task OnPostSyncNowAsync_RunsCycleAndReportsSuccess()
    {
        await model.OnPostSyncNowAsync();

        Assert.False(model.MessageIsError);
        Assert.Equal("Sincronización completada.", model.Message);
    }

    [Fact]
    public void OnPostAddManualCase_ValidData_InsertsPendingCaseNotNeedingReview()
    {
        model.OnPostAddManualCase("Catemu", "Gustavo Peña Castro", "18785387-7");

        var stored = Assert.Single(repository.GetAll());
        Assert.Equal("GUSTAVO PEÑA CASTRO", stored.FullName);
        Assert.Equal("18.785.387-7", stored.Rut);
        Assert.Equal("Catemu", stored.Comuna);
        Assert.Equal(RequestStatus.Pending, stored.Status);
        Assert.False(stored.NeedsReview);
        Assert.False(model.MessageIsError);
    }

    [Fact]
    public void OnPostAddManualCase_UnknownComuna_IsRejectedWithoutInserting()
    {
        model.OnPostAddManualCase("NoExiste", "Gustavo Peña Castro", "18785387-7");

        Assert.Empty(repository.GetAll());
        Assert.True(model.MessageIsError);
    }

    [Fact]
    public void OnPostAddManualCase_InvalidRut_IsRejectedWithoutInserting()
    {
        model.OnPostAddManualCase("Catemu", "Gustavo Peña Castro", "18785387-6"); // wrong check digit

        Assert.Empty(repository.GetAll());
        Assert.True(model.MessageIsError);
    }

    [Fact]
    public void OnPostAddManualCase_IncompleteName_IsRejectedWithoutInserting()
    {
        model.OnPostAddManualCase("Catemu", "Gustavo", "18785387-7");

        Assert.Empty(repository.GetAll());
        Assert.True(model.MessageIsError);
    }

    [Fact]
    public void OnPostAddManualCase_DuplicateRutAndComuna_IsRejectedWithoutSecondRow()
    {
        model.OnPostAddManualCase("Catemu", "Gustavo Peña Castro", "18785387-7");

        model.OnPostAddManualCase("Catemu", "Gustavo Andrés Peña Castro", "18785387-7");

        Assert.Single(repository.GetAll());
        Assert.True(model.MessageIsError);
    }

    [Fact]
    public void OnPostAddManualCases_ValidRows_InsertsAllUnderSameComuna()
    {
        model.OnPostAddManualCases(
            "Catemu",
            ["Gustavo Peña Castro", "Ana María Soto"],
            ["18785387-7", "9098162-5"]);

        var stored = repository.GetAll();
        Assert.Equal(2, stored.Count);
        Assert.All(stored, c => Assert.Equal("Catemu", c.Comuna));
        Assert.Contains(stored, c => c.FullName == "GUSTAVO PEÑA CASTRO" && c.Rut == "18.785.387-7");
        Assert.Contains(stored, c => c.FullName == "ANA MARÍA SOTO" && c.Rut == "09.098.162-5");
        Assert.False(model.MessageIsError);
    }

    [Fact]
    public void OnPostAddManualCases_BlankTrailingRow_IsIgnored()
    {
        model.OnPostAddManualCases(
            "Catemu",
            ["Gustavo Peña Castro", ""],
            ["18785387-7", ""]);

        var stored = Assert.Single(repository.GetAll());
        Assert.Equal("GUSTAVO PEÑA CASTRO", stored.FullName);
        Assert.False(model.MessageIsError);
    }

    [Fact]
    public void OnPostAddManualCases_OneRowInvalid_InsertsNoneAndReportsRow()
    {
        model.OnPostAddManualCases(
            "Catemu",
            ["Gustavo Peña Castro", "Ana María Soto"],
            ["18785387-7", "9098162-1"]); // second RUT has wrong check digit

        Assert.Empty(repository.GetAll());
        Assert.True(model.MessageIsError);
        Assert.Contains("Fila 2", model.Message);
    }

    [Fact]
    public void OnPostAddManualCases_DuplicateRutWithinSameSubmission_IsRejected()
    {
        model.OnPostAddManualCases(
            "Catemu",
            ["Gustavo Peña Castro", "Gustavo Andrés Peña Castro"],
            ["18785387-7", "18785387-7"]);

        Assert.Empty(repository.GetAll());
        Assert.True(model.MessageIsError);
        Assert.Contains("Fila 2", model.Message);
    }

    [Fact]
    public void OnPostAddManualCases_UnknownComuna_IsRejectedWithoutInserting()
    {
        model.OnPostAddManualCases("NoExiste", ["Gustavo Peña Castro"], ["18785387-7"]);

        Assert.Empty(repository.GetAll());
        Assert.True(model.MessageIsError);
    }

    [Fact]
    public void OnPostAddManualCases_NoRows_IsRejected()
    {
        model.OnPostAddManualCases("Catemu", [], []);

        Assert.Empty(repository.GetAll());
        Assert.True(model.MessageIsError);
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

    private sealed class NoOpEmailMover : IEmailMover
    {
        public Task<bool> MoveAndMarkUnreadAsync(string messageId, string sourceFolderDisplayName, string destinationFolderDisplayName, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class NoOpCsvReportWriter : ICsvReportWriter
    {
        public void Write(IReadOnlyList<PersonRequest> requests, string outputPath)
        {
        }
    }
}
