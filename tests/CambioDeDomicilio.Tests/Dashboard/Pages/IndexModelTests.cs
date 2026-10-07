using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Dashboard.Pages;
using CambioDeDomicilio.Directories;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Mail;
using CambioDeDomicilio.Notifications;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Reporting;
using CambioDeDomicilio.Routing;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Pages;

public class IndexModelTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"index-page-test-{Guid.NewGuid():N}.db");
    private IMessageTombstoneRepository Tombstones => new MessageTombstoneRepository($"Data Source={dbPath}");
    private IBoxRepository Boxes => new BoxRepository($"Data Source={dbPath}");
    private readonly string csvPath = Path.Combine(Path.GetTempPath(), $"index-page-test-{Guid.NewGuid():N}.csv");
    private readonly IPersonRequestRepository repository;
    private readonly AddressChangeRoutingService routingService;
    private readonly IndexModel model;
    private readonly Func<IndexModel> newModel;

    public IndexModelTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        TestDatabase.Migrate(dbPath);
        var discardedRepository = new DiscardedEmailRepository($"Data Source={dbPath}");
        TestDatabase.Migrate(dbPath);
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
            new MessageTombstoneRepository($"Data Source={dbPath}"),
            new ComunaDirectory(),
            new NoOpMailSender(),
            new NoOpEmailMover(),
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

        newModel = () => new IndexModel(repository, discardedRepository, new MessageTombstoneRepository($"Data Source={dbPath}"), new BoxRepository($"Data Source={dbPath}"), routingService, routerWorker, options, NullLogger<IndexModel>.Instance)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        model = newModel();
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

        // Newest request on top, oldest at the bottom.
        Assert.Equal(2, model.Cases.Count);
        Assert.Equal("msg-2", model.Cases[0].SourceMessageId);
        Assert.Equal("msg-1", model.Cases[1].SourceMessageId);
    }

    [Fact]
    public void OnGet_MarkedCases_StayOrderedByReceivedAtNewestFirst_NotByTickOrder()
    {
        // The order the operator ticked the checkboxes in must not scramble the list: marked cases
        // are still listed newest request first.
        var jan = NewRequest("msg-jan");
        jan.ReceivedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var mar = NewRequest("msg-mar");
        mar.ReceivedAt = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var jun = NewRequest("msg-jun");
        jun.ReceivedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var janId = repository.Insert(jan);
        var marId = repository.Insert(mar);
        var junId = repository.Insert(jun);

        // Tick the oldest first, then the newest, then the middle one.
        repository.SetMarked(janId, true);
        Thread.Sleep(20);
        repository.SetMarked(junId, true);
        Thread.Sleep(20);
        repository.SetMarked(marId, true);

        model.OnGet(status: null);

        Assert.Equal(["msg-jun", "msg-mar", "msg-jan"], model.Cases.Select(c => c.SourceMessageId).ToArray());
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
    public void OnPostSetFecha_IsoDate_UpdatesDate()
    {
        var id = repository.Insert(NewRequest("msg-iso"));

        model.OnPostSetFecha(id, "2024-05-01");

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
    public async Task OnPostConfirmAsync_UploadedCase_ConfirmsSuccessfully()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetFechaUltimaCarpeta(id, new DateOnly(2024, 3, 15));
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);

        await model.OnPostConfirmAsync(id);

        var stored = repository.GetAll().Single(c => c.Id == id);
        Assert.Equal(RequestStatus.Confirmed, stored.Status);
    }

    [Fact]
    public async Task OnPostMarkUploadedAndConfirmAsync_PendingCase_MovesUpdatesAndConfirms()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetFechaUltimaCarpeta(id, new DateOnly(2024, 3, 15));

        await model.OnPostMarkUploadedAndConfirmAsync(id);

        var stored = repository.GetAll().Single(c => c.Id == id);
        Assert.Equal(RequestStatus.Confirmed, stored.Status);
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

        Assert.True(Tombstones.IsSourceMessageDeleted("msg-1"));
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
    public async Task OnPostSyncNowAsync_BrowserAbortsRequestMidCycle_DoesNotCancelTheCycle()
    {
        // A slow cycle (e.g. many inbox messages during the bounce check) can outlive the browser
        // request — a closed tab or proxy timeout aborts HttpContext.RequestAborted while the EWS
        // calls are still in flight. The cycle must not be tied to that token, or the operator sees
        // the sync silently fail mid-way with no clear reason.
        var reader = new RecordingEmailReader();
        var discardedRepository = new DiscardedEmailRepository($"Data Source={dbPath}");
        var workerOptions = new RouterOptions
        {
            Ews = new EwsOptions { Url = "https://mail.munivalpo.cl/EWS/Exchange.asmx", Username = "u", Password = "p" },
            MailboxAddress = "cambiodedomicilio@munivalpo.cl",
            OwnDomain = "munivalpo.cl",
            SqliteDbPath = dbPath,
            ComunaDirectoryCsvPath = csvPath,
            ReportCsvPath = "unused-report-aborted.csv",
            NotificationEmailAddress = "raul.salazar1984@gmail.com"
        };
        var worker = new RouterWorker(routingService, reader, repository, new NoOpCsvReportWriter(), workerOptions, NullLogger<RouterWorker>.Instance);
        var abortedModel = new IndexModel(repository, discardedRepository, new MessageTombstoneRepository($"Data Source={dbPath}"), new BoxRepository($"Data Source={dbPath}"), routingService, worker, workerOptions, NullLogger<IndexModel>.Instance)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext { RequestAborted = new CancellationToken(canceled: true) }
            }
        };

        await abortedModel.OnPostSyncNowAsync();

        Assert.False(reader.AnyCallSawCancelledToken);
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
    public void OnPostAddManualCases_OldFoldersDirectToCaja_QueuesThemInOrderWithoutConfirmation()
    {
        model.OnPostAddManualCases(
            "Catemu",
            ["Gustavo Peña Castro", "Ana María Soto"],
            ["18785387-7", "9098162-5"],
            directToCaja: true);

        var queue = repository.GetCajaQueue();
        Assert.Equal(["GUSTAVO PEÑA CASTRO", "ANA MARÍA SOTO"], queue.Select(c => c.FullName));
        Assert.All(queue, c => Assert.Null(c.ConfirmedAt)); // no confirmation email went out
        model.OnGet(status: null);
        Assert.Empty(model.Cases);
        Assert.False(model.MessageIsError);
    }

    [Fact]
    public void OnPostAddManualCases_WithoutDirectToCaja_StaysPendingInCasos()
    {
        model.OnPostAddManualCases("Catemu", ["Gustavo Peña Castro"], ["18785387-7"]);

        var stored = Assert.Single(repository.GetAll());
        Assert.Equal(RequestStatus.Pending, stored.Status);
        Assert.Equal(CaseDestination.None, stored.Destination);
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

    [Fact]
    public void OnPostToggleFolderNotFound_OnValue_SetsFlag()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        model.OnPostToggleFolderNotFound(id, "on");

        Assert.True(repository.FindById(id)!.FolderNotFound);
    }

    [Fact]
    public void OnPostToggleFolderNotFound_NoValue_ClearsFlag()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetFolderNotFound(id, true);

        model.OnPostToggleFolderNotFound(id, null);

        Assert.False(repository.FindById(id)!.FolderNotFound);
    }

    [Fact]
    public void OnPostTogglePendienteCarpeta_OnValue_SetsFlag()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        model.OnPostTogglePendienteCarpeta(id, "on");

        Assert.True(repository.FindById(id)!.PendienteCarpeta);
    }

    [Fact]
    public void OnPostTogglePendienteCarpeta_NoValue_ClearsFlag()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetPendienteCarpeta(id, true);

        model.OnPostTogglePendienteCarpeta(id, null);

        Assert.False(repository.FindById(id)!.PendienteCarpeta);
    }

    [Fact]
    public void OnGet_CaseMovedToF8_IsExcludedFromCases()
    {
        var movedId = repository.Insert(NewRequest("msg-1"));
        repository.SetFolderNotFound(movedId, true);
        repository.SetDestination(movedId, CaseDestination.F8, DateTimeOffset.UtcNow);

        var stillInCasesId = repository.Insert(NewRequest("msg-2"));

        model.OnGet(status: null);

        var result = Assert.Single(model.Cases);
        Assert.Equal(stillInCasesId, result.Id);
    }

    [Fact]
    public void OnGet_CaseWithFolderNotFoundButNotMovedToF8_StillAppearsInCases()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetFolderNotFound(id, true);

        model.OnGet(status: null);

        var result = Assert.Single(model.Cases);
        Assert.Equal(id, result.Id);
    }

    [Fact]
    public void OnPostTransferToF8_SetsMovedToF8AtTimestamp()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetFolderNotFound(id, true);

        model.OnPostTransferToF8(id);

        var stored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.F8, stored.Destination);
        Assert.NotNull(stored.TransferredAt);
    }

    [Fact]
    public void OnPostTransferToF8_ClearsSoloCajaOnExplicitF8Return()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);
        repository.RevertF8AndReturnToCasos(id);
        repository.SetFolderNotFound(id, true);

        model.OnPostTransferToF8(id);

        var stored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.F8, stored.Destination);
        Assert.False(stored.SoloCaja);
    }

    [Fact]
    public void OnPostResolveBounce_ClearsTheBounceFlag()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.UpdateStatusToConfirmed(id, DateTimeOffset.UtcNow);
        repository.SetConfirmationBounced(id, DateTimeOffset.UtcNow);

        model.OnPostResolveBounce(id);

        Assert.Null(repository.FindById(id)!.ConfirmationBouncedAt);
    }

    [Fact]
    public void OnGet_BouncedFilter_ShowsOnlyCasesWithABounce()
    {
        var bounced = repository.Insert(NewRequest("msg-1"));
        repository.UpdateStatusToConfirmed(bounced, DateTimeOffset.UtcNow);
        repository.SetConfirmationBounced(bounced, DateTimeOffset.UtcNow);
        var normal = repository.Insert(NewRequest("msg-2"));
        repository.UpdateStatusToConfirmed(normal, DateTimeOffset.UtcNow);

        model.OnGet(status: null, needsReview: false, search: null, bounced: true);

        var shown = Assert.Single(model.Cases);
        Assert.Equal(bounced, shown.Id);
    }

    [Fact]
    public void OnGet_AlwaysExposesBouncedCount()
    {
        var bounced = repository.Insert(NewRequest("msg-1"));
        repository.UpdateStatusToConfirmed(bounced, DateTimeOffset.UtcNow);
        repository.SetConfirmationBounced(bounced, DateTimeOffset.UtcNow);

        model.OnGet(status: null);

        Assert.Equal(1, model.BouncedCount);
    }

    [Fact]
    public void OnGet_SearchMatchesCaseInClosedBox_ReportsCajaMatchWithBoxCode()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);
        repository.SendToCaja([id], DateTimeOffset.UtcNow);
        Boxes.CloseBox("A7-CD", DateTimeOffset.UtcNow);

        model.OnGet(status: null, search: "18.785.387-7");

        Assert.Empty(model.Cases);
        Assert.Equal(1, model.CajaMatchCount);
        Assert.Equal("A7-CD", model.CajaMatchBoxCode);
    }

    [Fact]
    public void OnGet_SearchMatchesCaseInOpenCajaQueue_ReportsQueueAsLocation()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);
        repository.SendToCaja([id], DateTimeOffset.UtcNow);

        model.OnGet(status: null, search: "PEÑA");

        Assert.Equal(1, model.CajaMatchCount);
        Assert.Equal("cola de Caja", model.CajaMatchBoxCode);
    }

    [Fact]
    public void OnGet_NoSearch_CajaMatchCountIsZero()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);
        repository.SendToCaja([id], DateTimeOffset.UtcNow);

        model.OnGet(status: null);

        Assert.Equal(0, model.CajaMatchCount);
    }

    [Fact]
    public void OnPostSubirACaja_ConfirmedCaseWithoutFecha_MovesToCajaAndLeavesCasos()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);
        repository.UpdateStatusToConfirmed(id, DateTimeOffset.UtcNow);

        model.OnPostSubirACaja(id);

        Assert.Equal(CaseDestination.Caja, repository.FindById(id)!.Destination);
        model.OnGet(status: null);
        Assert.DoesNotContain(model.Cases, c => c.Id == id);
    }

    [Fact]
    public void OnGet_ClosedWithoutFolderCase_MovesToSinCarpetasAndHiddenFromCasos()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);
        repository.CloseWithoutFolder(id, DateTimeOffset.UtcNow);

        model.OnGet(status: null);

        Assert.DoesNotContain(model.Cases, c => c.Id == id);
    }

    [Theory]
    [InlineData(nameof(IndexModel.StatusFilter), "status")]
    [InlineData(nameof(IndexModel.OnlyNeedsReview), "needsReview")]
    [InlineData(nameof(IndexModel.OnlyBounced), "bounced")]
    [InlineData(nameof(IndexModel.SearchQuery), "search")]
    public void ListStateProperties_AreBoundOnGetAndPost(string propertyName, string fieldName)
    {
        var bind = typeof(IndexModel).GetProperty(propertyName)!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.BindPropertyAttribute), false)
            .Cast<Microsoft.AspNetCore.Mvc.BindPropertyAttribute>()
            .SingleOrDefault();

        Assert.NotNull(bind);
        Assert.True(bind!.SupportsGet);
        Assert.Equal(fieldName, bind.Name);
    }

    [Fact]
    public void Message_OnRedirect_IsStoredOnceInTempDataAndShownOnNextGet()
    {
        var tempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(new DefaultHttpContext(), new InMemoryTempDataProvider());
        model.TempData = tempData;
        model.Message = "Carpeta enviada a Caja.";

        model.PersistMessageForRedirect(new Microsoft.AspNetCore.Mvc.RedirectToPageResult("/Index"));
        tempData.Save(); // end of the POST request

        var next = NewModelSharingTempData(tempData);
        next.OnGet(status: null);
        Assert.Equal("Carpeta enviada a Caja.", next.Message);
        tempData.Save(); // end of the GET request: read values are dropped

        var afterThat = NewModelSharingTempData(tempData);
        afterThat.OnGet(status: null);
        Assert.Null(afterThat.Message);
    }

    [Fact]
    public void Message_OnSamePageResponse_IsNotRepeatedOnNextGet()
    {
        var tempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(new DefaultHttpContext(), new InMemoryTempDataProvider());
        model.TempData = tempData;
        model.Message = "Fecha no reconocida.";

        model.PersistMessageForRedirect(new Microsoft.AspNetCore.Mvc.RazorPages.PageResult());
        tempData.Save();

        var next = NewModelSharingTempData(tempData);
        next.OnGet(status: null);
        Assert.Null(next.Message);
    }

    private IndexModel NewModelSharingTempData(Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary tempData)
    {
        var next = newModel();
        next.TempData = tempData;
        return next;
    }

    private sealed class InMemoryTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        private IDictionary<string, object> store = new Dictionary<string, object>();
        public IDictionary<string, object> LoadTempData(HttpContext context) => store;
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) => store = new Dictionary<string, object>(values);
    }

    [Fact]
    public void OnPostSubirACaja_RedirectsWithCurrentListState()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);
        model.SearchQuery = "18.785.387-7";
        model.StatusFilter = "Uploaded";

        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToPageResult>(model.OnPostSubirACaja(id));

        Assert.Equal("18.785.387-7", result.RouteValues!["search"]);
        Assert.Equal("Uploaded", result.RouteValues["status"]);
    }

    [Fact]
    public void OnGet_SearchMatchesCaseInSecondListingWithSameCode_ReportsExactListingAndPosition()
    {
        var sentAt = new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);
        var firstBoxCase = repository.Insert(OtherPerson("msg-a", "12.345.678-5"));
        repository.SetDestination(firstBoxCase, CaseDestination.Caja, sentAt);
        Boxes.CloseBox("A1-CD", sentAt.AddMinutes(1));

        var before = repository.Insert(OtherPerson("msg-b", "9.868.019-K"));
        repository.SetDestination(before, CaseDestination.Caja, sentAt.AddMinutes(2));
        var target = repository.Insert(NewRequest("msg-target"));
        repository.SetDestination(target, CaseDestination.Caja, sentAt.AddMinutes(3));
        var secondClosedAt = sentAt.AddMinutes(4);
        var secondBox = Boxes.CloseBox("A1-CD", secondClosedAt);

        model.OnGet(status: null, search: "18.785.387-7");

        var location = Assert.Single(model.CajaMatches);
        Assert.Equal(target, location.CaseId);
        Assert.Equal(secondBox.Id, location.BoxId);
        Assert.Equal("A1-CD", location.BoxCode);
        Assert.Equal(secondClosedAt, location.ClosedAt);
        Assert.Equal(2, location.Position);
    }

    [Fact]
    public void OnGet_SearchMatchesCaseInQueue_ReportsQueuePosition()
    {
        var sentAt = new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);
        var first = repository.Insert(OtherPerson("msg-a", "12.345.678-5"));
        repository.SetDestination(first, CaseDestination.Caja, sentAt);
        var second = repository.Insert(OtherPerson("msg-b", "9.868.019-K"));
        repository.SetDestination(second, CaseDestination.Caja, sentAt.AddMinutes(1));
        var target = repository.Insert(NewRequest("msg-target"));
        repository.SetDestination(target, CaseDestination.Caja, sentAt.AddMinutes(2));

        model.OnGet(status: null, search: "18.785.387-7");

        var location = Assert.Single(model.CajaMatches);
        Assert.Null(location.BoxId);
        Assert.Equal("cola de Caja", location.BoxCode);
        Assert.Equal(3, location.Position);
    }

    [Fact]
    public void OnGet_SearchMatchesCaseInSubidas_ComputesSubidasMatchCount()
    {
        var target = repository.Insert(NewRequest("msg-target"));
        repository.SetDestination(target, CaseDestination.Subidas, DateTimeOffset.UtcNow);

        model.OnGet(status: null, search: "18.785.387-7");

        Assert.Equal(1, model.SubidasMatchCount);
        Assert.Empty(model.Cases);
    }

    private static PersonRequest OtherPerson(string sourceMessageId, string rut)
    {
        var request = NewRequest(sourceMessageId);
        request.FullName = "OTRA PERSONA DISTINTA";
        request.Rut = rut;
        return request;
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

    private sealed class FakeMailSender : IMailSender
    {
        public List<(string To, string Subject, string Body)> SentMessages { get; } = [];

        public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
        {
            SentMessages.Add((toAddress, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpEmailReader : IEmailReader
    {
        public Task<IReadOnlyList<IncomingEmail>> GetMessagesInFolderAsync(string folderDisplayName, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IncomingEmail>>([]);

        public Task<IReadOnlyList<IncomingEmail>> GetInboxMessagesSinceAsync(DateTimeOffset receivedSince, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IncomingEmail>>([]);
    }

    private sealed class RecordingEmailReader : IEmailReader
    {
        public bool AnyCallSawCancelledToken { get; private set; }

        public Task<IReadOnlyList<IncomingEmail>> GetMessagesInFolderAsync(string folderDisplayName, CancellationToken cancellationToken)
        {
            AnyCallSawCancelledToken |= cancellationToken.IsCancellationRequested;
            return Task.FromResult<IReadOnlyList<IncomingEmail>>([]);
        }

        public Task<IReadOnlyList<IncomingEmail>> GetInboxMessagesSinceAsync(DateTimeOffset receivedSince, CancellationToken cancellationToken)
        {
            AnyCallSawCancelledToken |= cancellationToken.IsCancellationRequested;
            return Task.FromResult<IReadOnlyList<IncomingEmail>>([]);
        }
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
