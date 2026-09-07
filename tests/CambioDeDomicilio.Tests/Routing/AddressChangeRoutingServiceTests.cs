using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Directories;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Mail;
using CambioDeDomicilio.Notifications;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Routing;
using Xunit;

namespace CambioDeDomicilio.Tests.Routing;

public class AddressChangeRoutingServiceTests : IDisposable
{
    private static readonly IReadOnlyList<ComunaContact> Contacts =
    [
        new ComunaContact("Catemu", "rfloresc@municatemu.cl", "municatemu.cl")
    ];

    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"routing-test-{Guid.NewGuid():N}.db");
    private readonly IPersonRequestRepository repository;
    private readonly IDiscardedEmailRepository discardedRepository;
    private readonly FakeMailSender mailSender = new();
    private readonly FakeEmailMover emailMover = new();
    private readonly FakeNotificationChannel notificationChannel = new();
    private readonly AddressChangeRoutingService sut;

    public AddressChangeRoutingServiceTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        repository.EnsureSchema();
        discardedRepository = new DiscardedEmailRepository($"Data Source={dbPath}");
        discardedRepository.EnsureSchema();

        var options = new RouterOptions
        {
            Ews = new EwsOptions { Url = "https://mail.munivalpo.cl/EWS/Exchange.asmx", Username = "u", Password = "p" },
            MailboxAddress = "cambiodedomicilio@munivalpo.cl",
            OwnDomain = "munivalpo.cl",
            SourceFolderName = "CARP. PARA PEDIR",
            ConfirmationFolderName = "CARP. YA SUBIDAS",
            SqliteDbPath = dbPath,
            ComunaDirectoryCsvPath = "unused.csv",
            ReportCsvPath = "unused-report.csv",
            NotificationEmailAddress = "raul.salazar1984@gmail.com"
        };

        sut = new AddressChangeRoutingService(
            repository,
            discardedRepository,
            new ComunaDirectory(),
            mailSender,
            emailMover,
            [notificationChannel],
            options,
            NullLogger<AddressChangeRoutingService>.Instance);
    }

    [Fact]
    public void ProcessIncomingRequest_KnownComunaValidData_RecordsAsPendingWithoutSending()
    {
        sut.ProcessIncomingRequest(NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7"), Contacts);

        Assert.Empty(mailSender.SentMessages); // nothing is ever sent from this path
        var stored = repository.GetAll().Single();
        Assert.Equal(RequestStatus.Pending, stored.Status);
        Assert.Equal("Catemu", stored.Comuna);
    }

    [Fact]
    public void ProcessIncomingRequest_NameArrivesInMixedCase_IsStoredUppercase()
    {
        // PersonDataExtractor intentionally preserves the source email's casing (see
        // PersonDataExtractorTests) — normalization to uppercase must happen here, at the
        // storage boundary, so the report and dashboard always show a consistent case
        // regardless of how the comuna's system formatted the name in the original email.
        sut.ProcessIncomingRequest(NewEmail("msg-1", "Gustavo Andrés Peña Castro RUT: 18.785.387-7"), Contacts);

        var stored = repository.GetAll().Single();
        Assert.Equal("GUSTAVO ANDRÉS PEÑA CASTRO", stored.FullName);
    }

    [Fact]
    public void ProcessIncomingRequest_UnknownDomain_IsIgnoredAndRecordedAsDiscarded()
    {
        sut.ProcessIncomingRequest(
            NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7", sender: "alguien@otracomuna.cl"),
            Contacts);

        Assert.Empty(repository.GetAll());
        var discarded = Assert.Single(discardedRepository.GetAll());
        Assert.Equal("msg-1", discarded.SourceMessageId);
        Assert.Contains("otracomuna.cl", discarded.Reason);
    }

    [Fact]
    public void ProcessIncomingRequest_UnknownDomain_SecondCycleDoesNotDuplicateDiscardRecord()
    {
        var email = NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7", sender: "alguien@otracomuna.cl");

        sut.ProcessIncomingRequest(email, Contacts);
        sut.ProcessIncomingRequest(email, Contacts); // next poll cycle, email is still sitting in the folder

        Assert.Single(discardedRepository.GetAll());
    }

    [Fact]
    public void ProcessIncomingRequest_DomainLaterAddedToDirectory_RemovesStaleDiscardRecord()
    {
        var email = NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7", sender: "alguien@nuevacomuna.cl");
        sut.ProcessIncomingRequest(email, Contacts);
        Assert.Single(discardedRepository.GetAll());

        IReadOnlyList<ComunaContact> updatedContacts =
        [
            .. Contacts,
            new ComunaContact("Nueva Comuna", "contacto@nuevacomuna.cl", "nuevacomuna.cl")
        ];

        sut.ProcessIncomingRequest(email, updatedContacts);

        Assert.Empty(discardedRepository.GetAll());
        var stored = repository.GetAll().Single();
        Assert.Equal("Nueva Comuna", stored.Comuna);
    }

    [Fact]
    public void ProcessIncomingRequest_OwnDomain_IsIgnoredWithoutDiscardRecord()
    {
        sut.ProcessIncomingRequest(
            NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7", sender: "interno@munivalpo.cl"),
            Contacts);

        Assert.Empty(repository.GetAll());
        Assert.Empty(discardedRepository.GetAll());
    }

    [Fact]
    public void ProcessIncomingRequest_TwoContributorsInOneEmail_CreatesOneRequestPerPerson()
    {
        var body = """
            Estimados, junto con saludar, solicito tenga a bien hacer llegar carpeta con los
            antecedentes de conductor que se indica para la correspondiente emisión de licencia de
            conducir de

            EDGARD ORLANDO PACHECO CARRASCO 18.552.843-K
            JUAN CARLOS LORENZO PATIÑO GAMONAL 15.409.979-4

            Saludos cordiales,
            """;

        sut.ProcessIncomingRequest(NewEmail("msg-multi", body), Contacts);

        var stored = repository.GetAll().OrderBy(r => r.Rut).ToList();
        Assert.Equal(2, stored.Count);
        Assert.All(stored, r => Assert.Equal("msg-multi", r.SourceMessageId));
        Assert.All(stored, r => Assert.False(r.NeedsReview));
        Assert.Contains(stored, r => r.FullName == "EDGARD ORLANDO PACHECO CARRASCO" && r.Rut == "18.552.843-K");
        Assert.Contains(stored, r => r.FullName == "JUAN CARLOS LORENZO PATIÑO GAMONAL" && r.Rut == "15.409.979-4");
    }

    [Fact]
    public void ProcessIncomingRequest_SecondPollOfSameMultiContributorEmail_DoesNotDuplicate()
    {
        var body = """
            Solicito carpetas de:

            EDGARD ORLANDO PACHECO CARRASCO 18.552.843-K
            JUAN CARLOS LORENZO PATIÑO GAMONAL 15.409.979-4
            """;
        var email = NewEmail("msg-multi", body);

        sut.ProcessIncomingRequest(email, Contacts);
        sut.ProcessIncomingRequest(email, Contacts); // next poll cycle, same email still in the folder

        Assert.Equal(2, repository.GetAll().Count);
    }

    [Fact]
    public void ProcessIncomingRequest_DifferentContributorInSubjectThanBody_CreatesBoth()
    {
        // The body names one contributor, but the subject (forwarded email) names a second,
        // different one — both must end up tracked, not just whichever source is checked first.
        // The subject-derived one always needs manual review (name from subject is never trusted).
        var email = NewEmail(
            "msg-1",
            body: "Se solicita la carpeta de GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7 por cambio de domicilio.",
            subject: "RV: SOLICITUD ANTECEDENTES SOLANGE KATHERINE ARRIAGADA FERNÁNDEZ RUN 16.353.860-1");

        sut.ProcessIncomingRequest(email, Contacts);

        var stored = repository.GetAll().OrderBy(r => r.Rut).ToList();
        Assert.Equal(2, stored.Count);
        Assert.Contains(stored, r => r.FullName == "GUSTAVO ANDRÉS PEÑA CASTRO" && r.Rut == "18.785.387-7" && !r.NeedsReview);
        Assert.Contains(stored, r => r.FullName == null && r.Rut == "16.353.860-1" && r.NeedsReview);
    }

    [Fact]
    public void ProcessIncomingRequest_SameContributorRepeatedInSubjectAndBody_DoesNotDuplicate()
    {
        var email = NewEmail(
            "msg-1",
            body: "Se solicita la carpeta de GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7 por cambio de domicilio.",
            subject: "RV: SOLICITUD ANTECEDENTES GUSTAVO ANDRÉS PEÑA CASTRO RUT 18.785.387-7");

        sut.ProcessIncomingRequest(email, Contacts);

        Assert.Single(repository.GetAll());
    }

    [Fact]
    public void ProcessIncomingRequest_RutOnlyInSubject_FallsBackToSubjectButAlwaysNeedsReview()
    {
        var email = NewEmail(
            "msg-1",
            body: "Estimados, junto con saludar, favor tramitar lo indicado en el asunto. Saludos.",
            subject: "RV: SOLICITUD ANTECEDENTES SOLANGE KATHERINE ARRIAGADA FERNÁNDEZ RUN 16.353.860-1");

        sut.ProcessIncomingRequest(email, Contacts);

        var stored = repository.GetAll().Single();
        Assert.True(stored.NeedsReview); // name from subject is never auto-trusted, even when correct
        Assert.Null(stored.FullName);
        Assert.Equal("16.353.860-1", stored.Rut);
    }

    [Fact]
    public void ProcessIncomingRequest_TombstonedMessageId_IsNeverReinserted()
    {
        // The operator deleted every case tracked from this email, but the email itself is
        // still sitting untouched in the source folder — without a tombstone, the very next
        // poll cycle (auto or manual) would silently recreate the case from scratch, losing
        // whatever the operator had already entered (fecha, corrections, etc.).
        repository.RecordDeletedSourceMessage("msg-1");

        sut.ProcessIncomingRequest(NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7"), Contacts);

        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void ProcessIncomingRequest_MissingData_RecordedAsNeedsReview()
    {
        sut.ProcessIncomingRequest(NewEmail("msg-1", "Correo sin datos reconocibles."), Contacts);

        Assert.True(repository.GetAll().Single().NeedsReview);
    }

    [Fact]
    public void ProcessIncomingRequest_DuplicatePersonAndComuna_DoesNotCreateSecondRow()
    {
        sut.ProcessIncomingRequest(NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7"), Contacts);
        sut.ProcessIncomingRequest(NewEmail("msg-2", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7"), Contacts);

        Assert.Single(repository.GetAll()); // no duplicate row in the tracking table / report
    }

    [Fact]
    public void ProcessIncomingRequest_MessageFoundAgainWhileUploaded_RevertsToPendingWithoutDuplicating()
    {
        var id = InsertPending();
        sut.ProcessUploadedCase(NewEmail("msg-1", "irrelevante"));
        Assert.Equal(RequestStatus.Uploaded, repository.FindById(id)!.Status);

        // The operator moved the email back to "CARP. PARA PEDIR" to undo an accidental upload.
        sut.ProcessIncomingRequest(NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7"), Contacts);

        var stored = repository.FindById(id)!;
        Assert.Equal(RequestStatus.Pending, stored.Status);
        Assert.Null(stored.UploadedAt);
        Assert.Single(repository.GetAll()); // no duplicate row created
    }

    [Fact]
    public async Task ProcessIncomingRequest_MessageFoundAgainWhileConfirmed_DoesNotRevert()
    {
        var id = InsertPending();
        await sut.MarkUploadedAndConfirmAsync(id, Contacts, CancellationToken.None);
        Assert.Equal(RequestStatus.Confirmed, repository.FindById(id)!.Status);

        sut.ProcessIncomingRequest(NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7"), Contacts);

        // A real confirmation email already went to the comuna — reverting would be misleading.
        Assert.Equal(RequestStatus.Confirmed, repository.FindById(id)!.Status);
    }

    [Fact]
    public void ProcessIncomingRequest_MessageFoundAgainWhileStillPending_IsNoOp()
    {
        var id = InsertPending();

        sut.ProcessIncomingRequest(NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7"), Contacts);

        Assert.Equal(RequestStatus.Pending, repository.FindById(id)!.Status);
        Assert.Single(repository.GetAll());
    }

    [Fact]
    public void ProcessUploadedCase_MatchingPending_MarksUploadedWithoutSending()
    {
        var id = InsertPending();

        sut.ProcessUploadedCase(NewEmail("msg-1", "irrelevante"));

        Assert.Empty(mailSender.SentMessages); // moving the email never sends anything by itself
        Assert.Equal(RequestStatus.Uploaded, repository.FindById(id)!.Status);
    }

    [Fact]
    public void ProcessUploadedCase_NoMatchingPending_IsNoOp()
    {
        sut.ProcessUploadedCase(NewEmail("unrelated-msg", "irrelevante"));

        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public async Task MarkUploadedAndConfirmAsync_PendingCase_MovesEmailAndSendsConfirmation()
    {
        var id = InsertPending();

        var result = await sut.MarkUploadedAndConfirmAsync(id, Contacts, CancellationToken.None);

        Assert.True(result.Sent);
        Assert.Equal(RequestStatus.Confirmed, repository.FindById(id)!.Status);
        Assert.Single(mailSender.SentMessages);
        var moveCall = Assert.Single(emailMover.MoveCalls);
        Assert.Equal("msg-1", moveCall.MessageId);
        Assert.Equal("CARP. PARA PEDIR", moveCall.SourceFolder);
        Assert.Equal("CARP. YA SUBIDAS", moveCall.DestinationFolder);
    }

    [Fact]
    public async Task MarkUploadedAndConfirmAsync_MissingFechaUltimaCarpeta_RefusesWithoutMovingOrSending()
    {
        sut.ProcessIncomingRequest(NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7"), Contacts);
        var id = repository.GetAll().Single().Id; // no SetFechaUltimaCarpeta — deliberately missing

        var result = await sut.MarkUploadedAndConfirmAsync(id, Contacts, CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Empty(mailSender.SentMessages);
        Assert.Empty(emailMover.MoveCalls); // blocked before touching the mailbox at all
        Assert.Equal(RequestStatus.Pending, repository.FindById(id)!.Status);
    }

    [Fact]
    public async Task MarkUploadedAndConfirmAsync_EmailMoveFails_StillMarksUploadedAndSendsConfirmation()
    {
        emailMover.NextResult = false; // e.g. operator already moved it manually, or a mailbox hiccup
        var id = InsertPending();

        var result = await sut.MarkUploadedAndConfirmAsync(id, Contacts, CancellationToken.None);

        Assert.True(result.Sent);
        Assert.Equal(RequestStatus.Confirmed, repository.FindById(id)!.Status);
    }

    [Fact]
    public async Task MarkUploadedAndConfirmAsync_AlreadyUploaded_Refuses()
    {
        var id = InsertPending();
        sut.ProcessUploadedCase(NewEmail("msg-1", "irrelevante"));

        var result = await sut.MarkUploadedAndConfirmAsync(id, Contacts, CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Empty(emailMover.MoveCalls);
    }

    [Fact]
    public async Task MarkUploadedAndConfirmAsync_NeedsReview_RefusesWithoutMoving()
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

        var result = await sut.MarkUploadedAndConfirmAsync(id, Contacts, CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Empty(emailMover.MoveCalls);
        Assert.Equal(RequestStatus.Pending, repository.FindById(id)!.Status);
    }

    [Fact]
    public async Task SendConfirmationAsync_MissingFechaUltimaCarpeta_RefusesWithoutSending()
    {
        sut.ProcessIncomingRequest(NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7"), Contacts);
        var id = repository.GetAll().Single().Id; // no SetFechaUltimaCarpeta — deliberately missing
        sut.ProcessUploadedCase(NewEmail("msg-1", "irrelevante"));

        var result = await sut.SendConfirmationAsync(id, Contacts, CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Empty(mailSender.SentMessages);
        Assert.Equal(RequestStatus.Uploaded, repository.FindById(id)!.Status);
    }

    [Fact]
    public async Task SendConfirmationAsync_UploadedCase_SendsAndMarksConfirmed()
    {
        var id = InsertPending();
        sut.ProcessUploadedCase(NewEmail("msg-1", "irrelevante"));

        var result = await sut.SendConfirmationAsync(id, Contacts, CancellationToken.None);

        Assert.True(result.Sent);
        Assert.Single(mailSender.SentMessages);
        Assert.Equal("rfloresc@municatemu.cl", mailSender.SentMessages[0].To);
        Assert.Equal(RequestStatus.Confirmed, repository.FindById(id)!.Status);
        Assert.Single(notificationChannel.Notified);
    }

    [Fact]
    public async Task SendConfirmationAsync_ViaF8_SendsF8SpecificWording()
    {
        var id = InsertPending();
        sut.ProcessUploadedCase(NewEmail("msg-1", "irrelevante"));

        var result = await sut.SendConfirmationAsync(id, Contacts, CancellationToken.None, viaF8: true);

        Assert.True(result.Sent);
        var sent = Assert.Single(mailSender.SentMessages);
        var (expectedSubject, expectedBody) = CambioDeDomicilio.Notifications.EmailTemplates.UploadConfirmationF8("GUSTAVO ANDRÉS PEÑA CASTRO", "18.785.387-7");
        Assert.Equal(expectedSubject, sent.Subject);
        Assert.Equal(expectedBody, sent.Body);
        Assert.Contains("proceso F8", sent.Body);
    }

    [Fact]
    public async Task SendConfirmationAsync_ComunaCasingDiffersFromDirectory_StillMatchesAndSends()
    {
        // A case tracked before the directory's casing was normalized (e.g. "Catemu" vs the
        // current "CATEMU") must still resolve to the right contact — comuna names are not
        // meant to be case-sensitive identifiers.
        var id = InsertPending();
        sut.ProcessUploadedCase(NewEmail("msg-1", "irrelevante"));
        Assert.Equal("Catemu", repository.FindById(id)!.Comuna); // sanity check on the fixture's own casing

        var mixedCaseContacts = new List<ComunaContact> { new("CATEMU", "rfloresc@municatemu.cl", "municatemu.cl") };
        var result = await sut.SendConfirmationAsync(id, mixedCaseContacts, CancellationToken.None);

        Assert.True(result.Sent);
        Assert.Single(mailSender.SentMessages);
    }

    [Fact]
    public async Task SendConfirmationAsync_StillPending_RefusesToSend()
    {
        var id = InsertPending(); // never moved to CARP. YA SUBIDAS

        var result = await sut.SendConfirmationAsync(id, Contacts, CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Empty(mailSender.SentMessages);
        Assert.Equal(RequestStatus.Pending, repository.FindById(id)!.Status);
    }

    [Fact]
    public async Task SendConfirmationAsync_AlreadyConfirmed_DoesNotSendTwice()
    {
        var id = InsertPending();
        sut.ProcessUploadedCase(NewEmail("msg-1", "irrelevante"));
        await sut.SendConfirmationAsync(id, Contacts, CancellationToken.None);

        var second = await sut.SendConfirmationAsync(id, Contacts, CancellationToken.None);

        Assert.False(second.Sent);
        Assert.Single(mailSender.SentMessages); // only the first send happened
    }

    [Fact]
    public async Task SendConfirmationAsync_UnknownCase_ReturnsNotSent()
    {
        var result = await sut.SendConfirmationAsync(999, Contacts, CancellationToken.None);

        Assert.False(result.Sent);
    }

    [Fact]
    public async Task RectifyConfirmationAsync_ConfirmedCase_SendsRectificationAndRevertsToPending()
    {
        var id = InsertPending();
        sut.ProcessUploadedCase(NewEmail("msg-1", "irrelevante"));
        await sut.SendConfirmationAsync(id, Contacts, CancellationToken.None);

        var result = await sut.RectifyConfirmationAsync(id, Contacts, CancellationToken.None);

        Assert.True(result.Sent);
        Assert.Equal(2, mailSender.SentMessages.Count); // original confirmation + rectification
        Assert.Contains("Rectificación", mailSender.SentMessages[1].Subject);
        var stored = repository.FindById(id)!;
        Assert.Equal(RequestStatus.Pending, stored.Status);
        Assert.Null(stored.UploadedAt);
        Assert.Null(stored.ConfirmedAt);
        Assert.Null(stored.ConfirmedByUserId);
    }

    [Fact]
    public async Task RectifyConfirmationAsync_CaseWasFlaggedAsBounced_ClearsTheBounceFlag()
    {
        var id = InsertPending();
        sut.ProcessUploadedCase(NewEmail("msg-1", "irrelevante"));
        await sut.SendConfirmationAsync(id, Contacts, CancellationToken.None);
        repository.SetConfirmationBounced(id, DateTimeOffset.UtcNow);

        await sut.RectifyConfirmationAsync(id, Contacts, CancellationToken.None);

        // Reverted to a clean Pending state — a stale "REBOTÓ" badge from the confirmation
        // that's now being retracted would otherwise survive the next upload/confirm cycle.
        Assert.Null(repository.FindById(id)!.ConfirmationBouncedAt);
    }

    [Fact]
    public async Task RectifyConfirmationAsync_UploadedNotConfirmed_Refuses()
    {
        var id = InsertPending();
        sut.ProcessUploadedCase(NewEmail("msg-1", "irrelevante")); // Uploaded, never Confirmed

        var result = await sut.RectifyConfirmationAsync(id, Contacts, CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Empty(mailSender.SentMessages); // nothing was ever sent, nothing to retract
        Assert.Equal(RequestStatus.Uploaded, repository.FindById(id)!.Status);
    }

    [Fact]
    public async Task RectifyConfirmationAsync_StillPending_Refuses()
    {
        var id = InsertPending();

        var result = await sut.RectifyConfirmationAsync(id, Contacts, CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Equal(RequestStatus.Pending, repository.FindById(id)!.Status);
    }

    [Fact]
    public async Task RectifyConfirmationAsync_UnknownCase_ReturnsNotSent()
    {
        var result = await sut.RectifyConfirmationAsync(999, Contacts, CancellationToken.None);

        Assert.False(result.Sent);
    }

    private long InsertPending()
    {
        sut.ProcessIncomingRequest(NewEmail("msg-1", "GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7"), Contacts);
        var id = repository.GetAll().Single().Id;
        repository.SetFechaUltimaCarpeta(id, new DateOnly(2024, 3, 15)); // a ready-to-confirm case always has one
        return id;
    }

    private long InsertConfirmed(string messageId, string rut, string comuna = "Catemu")
    {
        var id = repository.Insert(new PersonRequest
        {
            FullName = "JUAN PEREZ SOTO",
            Rut = rut,
            Comuna = comuna,
            SourceMessageId = messageId,
            SourceSubject = "Solicitud de carpeta",
            SourceSender = "rfloresc@municatemu.cl",
            NeedsReview = false,
            Status = RequestStatus.Pending,
            ReceivedAt = DateTimeOffset.UtcNow
        });
        repository.UpdateStatusToConfirmed(id, DateTimeOffset.UtcNow);
        return id;
    }

    private static IncomingEmail Ndr(string messageId, string rut) => new(
        messageId, "conv-ndr",
        $"Undeliverable: Carpeta subida a Conaset - JUAN PEREZ SOTO, RUT {rut}",
        "postmaster@munivalpo.cl",
        $"Your message to rfloresc@municatemu.cl could not be delivered.\n" +
        $"550 The sending IP is listed on spamrl.com.\n\n" +
        $"Se informa que la carpeta del contribuyente JUAN PEREZ SOTO, RUT {rut}, ya fue subida al sistema de Conaset.",
        DateTimeOffset.UtcNow);

    [Fact]
    public void ProcessPotentialBounce_NdrMatchingConfirmedCase_FlagsItAndTombstones()
    {
        var id = InsertConfirmed("msg-1", "12.345.678-5");

        sut.ProcessPotentialBounce(Ndr("ndr-1", "12.345.678-5"), Contacts);

        Assert.NotNull(repository.FindById(id)!.ConfirmationBouncedAt);
        Assert.True(repository.IsBounceProcessed("ndr-1"));
    }

    [Fact]
    public void ProcessPotentialBounce_AlreadyProcessed_DoesNothing()
    {
        repository.RecordProcessedBounce("ndr-1");
        var id = InsertConfirmed("msg-1", "12.345.678-5");

        sut.ProcessPotentialBounce(Ndr("ndr-1", "12.345.678-5"), Contacts);

        Assert.Null(repository.FindById(id)!.ConfirmationBouncedAt);
    }

    [Fact]
    public void ProcessPotentialBounce_NotAnNdr_IgnoredAndNotTombstoned()
    {
        var id = InsertConfirmed("msg-1", "12.345.678-5");
        var reply = new IncomingEmail("reply-1", "c", "RE: Carpeta subida a Conaset",
            "rfloresc@municatemu.cl", "Gracias, recibido conforme. Conaset.", DateTimeOffset.UtcNow);

        sut.ProcessPotentialBounce(reply, Contacts);

        Assert.Null(repository.FindById(id)!.ConfirmationBouncedAt);
        Assert.False(repository.IsBounceProcessed("reply-1"));
    }

    [Fact]
    public void ProcessPotentialBounce_NdrWithNoMatchingConfirmedCase_TombstonesAnyway()
    {
        sut.ProcessPotentialBounce(Ndr("ndr-1", "12.345.678-5"), Contacts); // no case in the db at all

        Assert.True(repository.IsBounceProcessed("ndr-1"));
    }

    [Fact]
    public void ProcessPotentialBounce_SameRutDifferentComuna_OnlyFlagsTheBouncedComuna()
    {
        var catemuId = InsertConfirmed("msg-1", "12.345.678-5", comuna: "Catemu");
        var otherId = InsertConfirmed("msg-2", "12.345.678-5", comuna: "Viña del Mar");

        // The NDR body names only Catemu's contact address (rfloresc@municatemu.cl) — the
        // Viña del Mar case's confirmation was never sent in this scenario, so it must stay clean.
        sut.ProcessPotentialBounce(Ndr("ndr-1", "12.345.678-5"), Contacts);

        Assert.NotNull(repository.FindById(catemuId)!.ConfirmationBouncedAt);
        Assert.Null(repository.FindById(otherId)!.ConfirmationBouncedAt);
    }

    [Fact]
    public void ProcessPotentialBounce_CaseAlreadyFlagged_KeepsTheOriginalTimestamp()
    {
        var id = InsertConfirmed("msg-1", "12.345.678-5");
        var firstBounce = DateTimeOffset.UtcNow.AddDays(-3);
        repository.SetConfirmationBounced(id, firstBounce);

        sut.ProcessPotentialBounce(Ndr("ndr-2", "12.345.678-5"), Contacts);

        Assert.Equal(firstBounce, repository.FindById(id)!.ConfirmationBouncedAt);
    }

    private static IncomingEmail NewEmail(string messageId, string body, string sender = "rfloresc@municatemu.cl", string subject = "Solicitud de carpeta") =>
        new(messageId, "conv-1", subject, sender, body, DateTimeOffset.UtcNow);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
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

    private sealed class FakeEmailMover : IEmailMover
    {
        public bool NextResult { get; set; } = true;
        public List<(string MessageId, string SourceFolder, string DestinationFolder)> MoveCalls { get; } = [];

        public Task<bool> MoveAndMarkUnreadAsync(string messageId, string sourceFolderDisplayName, string destinationFolderDisplayName, CancellationToken cancellationToken)
        {
            MoveCalls.Add((messageId, sourceFolderDisplayName, destinationFolderDisplayName));
            return Task.FromResult(NextResult);
        }
    }

    private sealed class FakeNotificationChannel : INotificationChannel
    {
        public List<(string FullName, string Rut, string Comuna)> Notified { get; } = [];

        public void NotifyConfirmationSent(string fullName, string rut, string comuna) =>
            Notified.Add((fullName, rut, comuna));
    }
}
