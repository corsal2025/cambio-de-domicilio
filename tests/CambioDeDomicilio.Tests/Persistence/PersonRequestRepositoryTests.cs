using System.Linq;
using Microsoft.Data.Sqlite;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;
using Xunit;

namespace CambioDeDomicilio.Tests.Persistence;

public class PersonRequestRepositoryTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"router-test-{Guid.NewGuid():N}.db");
    private readonly IPersonRequestRepository repository;

    public PersonRequestRepositoryTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        TestDatabase.Migrate(dbPath);
    }

    [Fact]
    public void ExistsBySourceMessageId_AfterInsert_ReturnsTrue()
    {
        repository.Insert(NewRequest("msg-1"));

        Assert.True(repository.ExistsBySourceMessageId("msg-1"));
        Assert.False(repository.ExistsBySourceMessageId("msg-unknown"));
    }

    [Fact]
    public void FindByRutAndComuna_ExistingPending_ReturnsRecord()
    {
        repository.Insert(NewRequest("msg-1"));

        var found = repository.FindByRutAndComuna("18.785.387-7", "Catemu");

        Assert.NotNull(found);
        Assert.Equal(RequestStatus.Pending, found!.Status);
    }

    [Fact]
    public void FindByRutAndComuna_NoRecord_ReturnsNull()
    {
        Assert.Null(repository.FindByRutAndComuna("18.785.387-7", "Catemu"));
    }

    [Fact]
    public void MarkUploaded_PendingCase_TransitionsToUploaded()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        repository.MarkUploaded(id, DateTimeOffset.UtcNow);

        var stored = repository.FindById(id);
        Assert.Equal(RequestStatus.Uploaded, stored!.Status);
        Assert.NotNull(stored.UploadedAt);
    }

    [Fact]
    public void FindPendingBySourceMessageId_AfterUploaded_ReturnsNull()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);

        Assert.Null(repository.FindPendingBySourceMessageId("msg-1"));
    }

    [Fact]
    public void SetFechaUltimaCarpeta_StoresDateAndDerivesSector()
    {
        var idArchivo = repository.Insert(NewRequest("msg-1"));
        var idOficina = repository.Insert(NewRequest("msg-2", rut: "10.000.013-K"));

        repository.SetFechaUltimaCarpeta(idArchivo, new DateOnly(2022, 3, 15));
        repository.SetFechaUltimaCarpeta(idOficina, new DateOnly(2024, 1, 10));

        Assert.Equal(FolderSector.Archivo, repository.FindById(idArchivo)!.Sector);
        Assert.Equal(FolderSector.Oficina43, repository.FindById(idOficina)!.Sector);
    }

    [Fact]
    public void ClearFechaUltimaCarpeta_RemovesDateAndSector()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetFechaUltimaCarpeta(id, new DateOnly(2022, 3, 15));

        repository.ClearFechaUltimaCarpeta(id);

        var stored = repository.FindById(id)!;
        Assert.Null(stored.FechaUltimaCarpeta);
        Assert.Null(stored.Sector);
    }

    [Fact]
    public void Sector_WithoutFecha_IsNull()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        Assert.Null(repository.FindById(id)!.Sector);
    }

    [Fact]
    public void Insert_DefaultsMarkedToFalse()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        Assert.False(repository.FindById(id)!.Marked);
    }

    [Fact]
    public void SetMarked_TogglesFlag()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        repository.SetMarked(id, true);
        Assert.True(repository.FindById(id)!.Marked);

        repository.SetMarked(id, false);
        Assert.False(repository.FindById(id)!.Marked);
    }

    [Fact]
    public void SetPendienteCarpeta_TogglesFlag()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        repository.SetPendienteCarpeta(id, true);
        Assert.True(repository.FindById(id)!.PendienteCarpeta);

        repository.SetPendienteCarpeta(id, false);
        Assert.False(repository.FindById(id)!.PendienteCarpeta);
    }

    [Fact]
    public void Baseline_OnPreExistingTableWithoutMarkedColumn_AddsColumnWithoutDataLoss()
    {
        // Simulates a database created before the Marked column existed.
        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                DROP TABLE PersonRequest;
                CREATE TABLE PersonRequest (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NULL,
                    Rut TEXT NULL,
                    Comuna TEXT NULL,
                    SourceMessageId TEXT NOT NULL UNIQUE,
                    SourceConversationId TEXT NULL,
                    SourceSubject TEXT NOT NULL,
                    SourceSender TEXT NOT NULL,
                    NeedsReview INTEGER NOT NULL,
                    Status TEXT NOT NULL,
                    ReceivedAt TEXT NOT NULL,
                    FechaUltimaCarpeta TEXT NULL,
                    UploadedAt TEXT NULL,
                    ConfirmedAt TEXT NULL,
                    ConfirmedByUserId INTEGER NULL,
                    CreatedAt TEXT NOT NULL
                );
                """;
            command.ExecuteNonQuery();
        }
        var preExistingId = repository.Insert(NewRequest("msg-old"));

        TestDatabase.RerunBaseline(dbPath); // re-adopt the rewritten legacy table

        var stored = repository.FindById(preExistingId);
        Assert.NotNull(stored);
        Assert.False(stored!.Marked);
        repository.SetMarked(preExistingId, true);
        Assert.True(repository.FindById(preExistingId)!.Marked);
    }

    [Fact]
    public void Insert_TwoRequestsSameSourceMessageId_BothSucceed()
    {
        // A single source email can list more than one contributor — all their PersonRequest
        // rows share the same SourceMessageId, so it must not be a unique constraint.
        var request1 = NewRequest("msg-multi", rut: "18.552.843-K");
        request1.FullName = "EDGARD ORLANDO PACHECO CARRASCO";
        var request2 = NewRequest("msg-multi", rut: "15.409.979-4");
        request2.FullName = "JUAN CARLOS LORENZO PATIÑO GAMONAL";

        var id1 = repository.Insert(request1);
        var id2 = repository.Insert(request2);

        Assert.NotEqual(id1, id2);
        var stored = repository.GetAll().Where(r => r.SourceMessageId == "msg-multi").ToList();
        Assert.Equal(2, stored.Count);
        Assert.Contains(stored, r => r.Rut == "18.552.843-K" && r.FullName == "EDGARD ORLANDO PACHECO CARRASCO");
        Assert.Contains(stored, r => r.Rut == "15.409.979-4" && r.FullName == "JUAN CARLOS LORENZO PATIÑO GAMONAL");
    }

    [Fact]
    public void Baseline_OnPreExistingTableWithUniqueSourceMessageId_RemovesConstraintWithoutDataLoss()
    {
        // Simulates a database created before multi-contributor emails were supported, where
        // SourceMessageId was still a UNIQUE column.
        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                DROP TABLE PersonRequest;
                CREATE TABLE PersonRequest (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NULL,
                    Rut TEXT NULL,
                    Comuna TEXT NULL,
                    SourceMessageId TEXT NOT NULL UNIQUE,
                    SourceConversationId TEXT NULL,
                    SourceSubject TEXT NOT NULL,
                    SourceSender TEXT NOT NULL,
                    NeedsReview INTEGER NOT NULL,
                    Status TEXT NOT NULL,
                    ReceivedAt TEXT NOT NULL,
                    FechaUltimaCarpeta TEXT NULL,
                    UploadedAt TEXT NULL,
                    ConfirmedAt TEXT NULL,
                    ConfirmedByUserId INTEGER NULL,
                    CreatedAt TEXT NOT NULL,
                    Marked INTEGER NOT NULL DEFAULT 0
                );
                """;
            command.ExecuteNonQuery();
        }
        var preExistingId = repository.Insert(NewRequest("msg-old"));

        // Set the legacy Marked flag with raw SQL, not repository.SetMarked: that method now also
        // writes MarkedAt/SectorPdfGeneratedAt, columns this pre-migration schema doesn't have yet
        // (real app startup always runs EnsureSchema before any request reaches the repository).
        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE PersonRequest SET Marked = 1 WHERE Id = @id";
            command.Parameters.AddWithValue("@id", preExistingId);
            command.ExecuteNonQuery();
        }

        TestDatabase.RerunBaseline(dbPath); // re-adopt the rewritten legacy table

        // Old row preserved, including its Marked flag.
        var preExisting = repository.FindById(preExistingId);
        Assert.NotNull(preExisting);
        Assert.True(preExisting!.Marked);

        // The constraint is gone: a second row with the same SourceMessageId now succeeds.
        var secondId = repository.Insert(NewRequest("msg-old", rut: "15.409.979-4"));
        Assert.NotEqual(preExistingId, secondId);
        Assert.Equal(2, repository.GetAll().Count(r => r.SourceMessageId == "msg-old"));
    }

    [Fact]
    public void UpdateStatusToConfirmed_SetsStatusAndTimestamp()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        var confirmedAt = DateTimeOffset.UtcNow;

        repository.UpdateStatusToConfirmed(id, confirmedAt, confirmedByUserId: 1);

        var stored = repository.FindById(id);
        Assert.Equal(RequestStatus.Confirmed, stored!.Status);
        Assert.Equal(confirmedAt, stored.ConfirmedAt);
    }

    [Fact]
    public void Delete_ExistingCase_RemovesItFromGetAll()
    {
        var toDelete = repository.Insert(NewRequest("msg-1"));
        var toKeep = repository.Insert(NewRequest("msg-2", rut: "15.409.979-4"));

        repository.Delete(toDelete);

        Assert.Null(repository.FindById(toDelete));
        Assert.NotNull(repository.FindById(toKeep));
        Assert.Single(repository.GetAll());
    }

    [Fact]
    public void RevertUploadedBySourceMessageId_UploadedCase_RevertsToPendingAndClearsUploadedAt()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);

        var reverted = repository.RevertUploadedBySourceMessageId("msg-1");

        Assert.Equal(1, reverted);
        var stored = repository.FindById(id)!;
        Assert.Equal(RequestStatus.Pending, stored.Status);
        Assert.Null(stored.UploadedAt);
    }

    [Fact]
    public void RevertUploadedBySourceMessageId_ConfirmedCase_IsNotReverted()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);
        repository.UpdateStatusToConfirmed(id, DateTimeOffset.UtcNow, confirmedByUserId: 1);

        var reverted = repository.RevertUploadedBySourceMessageId("msg-1");

        Assert.Equal(0, reverted);
        Assert.Equal(RequestStatus.Confirmed, repository.FindById(id)!.Status);
    }

    [Fact]
    public void RevertUploadedBySourceMessageId_PendingCase_ReturnsZeroAndIsUnaffected()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        var reverted = repository.RevertUploadedBySourceMessageId("msg-1");

        Assert.Equal(0, reverted);
        Assert.Equal(RequestStatus.Pending, repository.FindById(id)!.Status);
    }

    [Fact]
    public void RevertUploadedBySourceMessageId_MultipleUploadedRowsSharingMessageId_RevertsAll()
    {
        var id1 = repository.Insert(NewRequest("msg-multi", rut: "18.552.843-K"));
        var id2 = repository.Insert(NewRequest("msg-multi", rut: "15.409.979-4"));
        repository.MarkUploaded(id1, DateTimeOffset.UtcNow);
        repository.MarkUploaded(id2, DateTimeOffset.UtcNow);

        var reverted = repository.RevertUploadedBySourceMessageId("msg-multi");

        Assert.Equal(2, reverted);
        Assert.Equal(RequestStatus.Pending, repository.FindById(id1)!.Status);
        Assert.Equal(RequestStatus.Pending, repository.FindById(id2)!.Status);
    }

    [Fact]
    public void SetSectorPdfGenerated_StoresTimestamp()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        var generatedAt = DateTimeOffset.UtcNow;

        repository.SetSectorPdfGenerated(id, generatedAt);

        Assert.Equal(generatedAt, repository.FindById(id)!.SectorPdfGeneratedAt);
    }

    [Fact]
    public void Baseline_OnPreExistingTableWithoutSectorPdfGeneratedAtColumn_AddsColumnWithoutDataLoss()
    {
        // Simulates a database created before the SectorPdfGeneratedAt column existed.
        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                DROP TABLE PersonRequest;
                CREATE TABLE PersonRequest (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NULL,
                    Rut TEXT NULL,
                    Comuna TEXT NULL,
                    SourceMessageId TEXT NOT NULL,
                    SourceConversationId TEXT NULL,
                    SourceSubject TEXT NOT NULL,
                    SourceSender TEXT NOT NULL,
                    NeedsReview INTEGER NOT NULL,
                    Status TEXT NOT NULL,
                    ReceivedAt TEXT NOT NULL,
                    FechaUltimaCarpeta TEXT NULL,
                    UploadedAt TEXT NULL,
                    ConfirmedAt TEXT NULL,
                    ConfirmedByUserId INTEGER NULL,
                    CreatedAt TEXT NOT NULL,
                    Marked INTEGER NOT NULL DEFAULT 0
                );
                """;
            command.ExecuteNonQuery();
        }
        var preExistingId = repository.Insert(NewRequest("msg-old"));

        TestDatabase.RerunBaseline(dbPath); // re-adopt the rewritten legacy table

        var stored = repository.FindById(preExistingId);
        Assert.NotNull(stored);
        Assert.Null(stored!.SectorPdfGeneratedAt);
        repository.SetSectorPdfGenerated(preExistingId, DateTimeOffset.UtcNow);
        Assert.NotNull(repository.FindById(preExistingId)!.SectorPdfGeneratedAt);
    }

    [Fact]
    public void SetCodigoF8_SetsAndClearsValue()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        repository.SetCodigoF8(id, "F8-1234");
        Assert.Equal("F8-1234", repository.FindById(id)!.CodigoF8);

        repository.SetCodigoF8(id, null);
        Assert.Null(repository.FindById(id)!.CodigoF8);
    }

    [Fact]
    public void SetDestination_StoresDestinationAndTimestamp()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        var transferredAt = DateTimeOffset.UtcNow;

        repository.SetDestination(id, CaseDestination.F8, transferredAt);

        var stored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.F8, stored.Destination);
        Assert.Equal(transferredAt, stored.TransferredAt);
    }

    [Fact]
    public void ClearDestination_ResetsToNone()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);

        repository.ClearDestination(id);

        var stored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.None, stored.Destination);
        Assert.Null(stored.TransferredAt);
    }

    [Fact]
    public void GetCajaQueue_OnlyReturnsUnboxedCajaDestinationCases_InInsertionOrder()
    {
        var sentAt = new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

        // Sent first, even though its última carpeta is the newest one.
        var first = repository.Insert(NewRequest("msg-first", rut: "12.345.678-5"));
        repository.SetFechaUltimaCarpeta(first, new DateOnly(2024, 5, 1));
        repository.SetDestination(first, CaseDestination.Caja, sentAt);

        var second = repository.Insert(NewRequest("msg-second", rut: "9.868.019-K"));
        repository.SetFechaUltimaCarpeta(second, new DateOnly(2018, 1, 1));
        repository.SetDestination(second, CaseDestination.Caja, sentAt.AddMinutes(1));

        // Not in the queue: still in Casos (Destination.None) and transferred to F8.
        repository.Insert(NewRequest("msg-none", rut: "5.126.663-2"));
        var f8Case = repository.Insert(NewRequest("msg-f8", rut: "7.036.145-6"));
        repository.SetDestination(f8Case, CaseDestination.F8, sentAt);

        var queue = repository.GetCajaQueue();

        Assert.Equal([first, second], queue.Select(c => c.Id));
    }

    [Fact]
    public void GetCasesByBoxId_KeepsInsertionOrderAfterClosing()
    {
        var sentAt = new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);
        var first = repository.Insert(NewRequest("msg-first", rut: "12.345.678-5"));
        repository.SetFechaUltimaCarpeta(first, new DateOnly(2024, 5, 1));
        repository.SetDestination(first, CaseDestination.Caja, sentAt);
        var second = repository.Insert(NewRequest("msg-second", rut: "9.868.019-K"));
        repository.SetFechaUltimaCarpeta(second, new DateOnly(2018, 1, 1));
        repository.SetDestination(second, CaseDestination.Caja, sentAt.AddMinutes(1));

        var box = repository.CloseBox("A1-CD", sentAt.AddHours(1));

        Assert.Equal([first, second], repository.GetCasesByBoxId(box.Id).Select(c => c.Id));
    }

    [Fact]
    public void CloseBox_AssignsQueuedCasesToNewSequentiallyNumberedBox()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetDestination(id, CaseDestination.Caja, DateTimeOffset.UtcNow);

        var firstBox = repository.CloseBox("A1-CD", DateTimeOffset.UtcNow);
        Assert.Equal(1, firstBox.Number);
        Assert.Equal("A1-CD", firstBox.Code);
        Assert.Empty(repository.GetCajaQueue());
        Assert.Equal(id, Assert.Single(repository.GetCasesByBoxId(firstBox.Id)).Id);

        var idTwo = repository.Insert(NewRequest("msg-2", rut: "12.345.678-5"));
        repository.SetDestination(idTwo, CaseDestination.Caja, DateTimeOffset.UtcNow);
        var secondBox = repository.CloseBox("A2-CD", DateTimeOffset.UtcNow);

        Assert.Equal(2, secondBox.Number);
        Assert.Equal("A2-CD", secondBox.Code);
        Assert.Equal(idTwo, Assert.Single(repository.GetCasesByBoxId(secondBox.Id)).Id);
        // Closing a second box must not reassign what's already settled in the first one.
        Assert.Equal(id, Assert.Single(repository.GetCasesByBoxId(firstBox.Id)).Id);
    }

    [Fact]
    public void CloseBox_EmptyQueue_StillCreatesABoxWithDefaultCode()
    {
        var box = repository.CloseBox("", DateTimeOffset.UtcNow);

        Assert.Equal(1, box.Number);
        Assert.Equal("A1-CD", box.Code);
        Assert.Empty(repository.GetCasesByBoxId(box.Id));
    }

    [Fact]
    public void GetBoxes_ReturnsMostRecentlyClosedFirst()
    {
        repository.CloseBox("A1-CD", DateTimeOffset.UtcNow);
        repository.CloseBox("A2-CD", DateTimeOffset.UtcNow);

        var boxes = repository.GetBoxes();

        Assert.Equal(2, boxes.Count);
        Assert.Equal(2, boxes[0].Number);
        Assert.Equal("A2-CD", boxes[0].Code);
        Assert.Equal(1, boxes[1].Number);
        Assert.Equal("A1-CD", boxes[1].Code);
    }

    [Fact]
    public void GetCajaQueue_ExcludesSinCarpeta()
    {
        var regular = repository.Insert(NewRequest("msg-1"));
        repository.SetDestination(regular, CaseDestination.Caja, DateTimeOffset.UtcNow);

        var sc = repository.Insert(NewRequest("msg-2", rut: "11.111.111-1"));
        repository.SetSinCarpeta(sc);
        repository.SetDestination(sc, CaseDestination.Caja, DateTimeOffset.UtcNow);

        var queue = repository.GetCajaQueue();
        Assert.Single(queue);
        Assert.Equal(regular, queue[0].Id);
    }

    [Fact]
    public void SendToCaja_SetsDestinationAndTransferredAt_OnlyForPhysicalCases()
    {
        var uploaded = repository.Insert(NewRequest("msg-1"));
        repository.MarkUploaded(uploaded, DateTimeOffset.UtcNow);
        repository.SetMarked(uploaded, true);

        var confirmed = repository.Insert(NewRequest("msg-2", rut: "12.345.678-5"));
        repository.MarkUploaded(confirmed, DateTimeOffset.UtcNow);
        repository.UpdateStatusToConfirmed(confirmed, DateTimeOffset.UtcNow);
        repository.SetMarked(confirmed, true);

        var sc = repository.Insert(NewRequest("msg-3", rut: "13.456.789-2"));
        repository.MarkUploaded(sc, DateTimeOffset.UtcNow);
        repository.SetSinCarpeta(sc);

        repository.SendToCaja([uploaded, confirmed, sc], DateTimeOffset.UtcNow);

        var storedUploaded = repository.FindById(uploaded)!;
        Assert.Equal(CaseDestination.Caja, storedUploaded.Destination);
        Assert.False(storedUploaded.Marked);
        Assert.NotNull(storedUploaded.TransferredAt);

        var storedConfirmed = repository.FindById(confirmed)!;
        Assert.Equal(CaseDestination.Caja, storedConfirmed.Destination);
        Assert.False(storedConfirmed.Marked);

        var storedSc = repository.FindById(sc)!;
        Assert.Equal(CaseDestination.None, storedSc.Destination);
    }

    [Fact]
    public void ClearDestination_RemovesFromCajaQueue_AndReturnsToCasos()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);
        repository.SendToCaja([id], DateTimeOffset.UtcNow);

        Assert.Single(repository.GetCajaQueue());

        repository.ClearDestination(id);

        Assert.Empty(repository.GetCajaQueue());
        var stored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.None, stored.Destination);
        Assert.Null(stored.TransferredAt);
    }

    [Fact]
    public void ReopenBox_UnpacksCasesToQueueAndDeletesBox()
    {
        var id1 = repository.Insert(NewRequest("msg-1"));
        var id2 = repository.Insert(NewRequest("msg-2"));
        repository.MarkUploaded(id1, DateTimeOffset.UtcNow);
        repository.MarkUploaded(id2, DateTimeOffset.UtcNow);
        repository.SendToCaja([id1, id2], DateTimeOffset.UtcNow);

        var box = repository.CloseBox("A3-CD", DateTimeOffset.UtcNow);
        Assert.Empty(repository.GetCajaQueue());
        Assert.Equal(2, repository.GetCasesByBoxId(box.Id).Count);

        repository.ReopenBox(box.Id);

        var queue = repository.GetCajaQueue();
        Assert.Equal(2, queue.Count);
        Assert.Null(repository.FindBoxById(box.Id));
        Assert.Null(repository.FindById(id1)!.BoxId);
        Assert.Equal(CaseDestination.Caja, repository.FindById(id1)!.Destination);
    }

    [Fact]
    public void RemoveCaseFromClosedBox_ReturnsCaseToCasos()
    {
        var id1 = repository.Insert(NewRequest("msg-1"));
        var id2 = repository.Insert(NewRequest("msg-2"));
        repository.MarkUploaded(id1, DateTimeOffset.UtcNow);
        repository.MarkUploaded(id2, DateTimeOffset.UtcNow);
        repository.SendToCaja([id1, id2], DateTimeOffset.UtcNow);

        var box = repository.CloseBox("A3-CD", DateTimeOffset.UtcNow);
        repository.RemoveCaseFromClosedBox(id1);

        var remainingInBox = repository.GetCasesByBoxId(box.Id);
        Assert.Single(remainingInBox);
        Assert.Equal(id2, remainingInBox[0].Id);

        var removed = repository.FindById(id1)!;
        Assert.Equal(CaseDestination.None, removed.Destination);
        Assert.Null(removed.BoxId);
        Assert.Null(removed.TransferredAt);
    }

    [Fact]
    public void RevertConfirmedToPending_UnboxedCajaCase_ReturnsToCasos()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetDestination(id, CaseDestination.Caja, DateTimeOffset.UtcNow);
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);
        repository.UpdateStatusToConfirmed(id, DateTimeOffset.UtcNow);

        repository.RevertConfirmedToPending(id);

        var stored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.None, stored.Destination);
        Assert.Null(stored.TransferredAt);
        Assert.Equal(RequestStatus.Pending, stored.Status);
    }

    [Fact]
    public void RevertConfirmedToPending_AlreadyBoxedCajaCase_StaysInItsBox()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetDestination(id, CaseDestination.Caja, DateTimeOffset.UtcNow);
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);
        repository.UpdateStatusToConfirmed(id, DateTimeOffset.UtcNow);
        var box = repository.CloseBox("A1-CD", DateTimeOffset.UtcNow);

        repository.RevertConfirmedToPending(id);

        var stored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.Caja, stored.Destination);
        Assert.Equal(box.Id, stored.BoxId);
    }

    [Fact]
    public void RevertUploadedBySourceMessageId_AlreadyBoxedCajaCase_StaysInItsBox()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetDestination(id, CaseDestination.Caja, DateTimeOffset.UtcNow);
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);
        var box = repository.CloseBox("A1-CD", DateTimeOffset.UtcNow);

        repository.RevertUploadedBySourceMessageId("msg-1");

        var stored = repository.FindById(id)!;
        // A boxed case's Status still reverts (the email really did reappear in the source
        // folder), but its physical box assignment is historical record and must survive.
        Assert.Equal(RequestStatus.Pending, stored.Status);
        Assert.Equal(CaseDestination.Caja, stored.Destination);
        Assert.Equal(box.Id, stored.BoxId);
    }

    [Fact]
    public void Baseline_LegacyDatabaseRebuild_PreservesBoxId()
    {
        // Simulates a database still on the pre-multi-contributor schema (SourceMessageId
        // UNIQUE), which EnsureSchema rebuilds via a hand-written column list — this test locks
        // in that BoxId, added well after that rebuild path was written, is actually carried
        // through it instead of silently dropped by an out-of-sync column list.
        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            using var create = connection.CreateCommand();
            create.CommandText = """
                DROP TABLE PersonRequest;
                CREATE TABLE PersonRequest (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NULL,
                    Rut TEXT NULL,
                    Comuna TEXT NULL,
                    SourceMessageId TEXT NOT NULL UNIQUE,
                    SourceConversationId TEXT NULL,
                    SourceSubject TEXT NOT NULL,
                    SourceSender TEXT NOT NULL,
                    NeedsReview INTEGER NOT NULL,
                    Status TEXT NOT NULL,
                    ReceivedAt TEXT NOT NULL,
                    FechaUltimaCarpeta TEXT NULL,
                    UploadedAt TEXT NULL,
                    ConfirmedAt TEXT NULL,
                    ConfirmedByUserId INTEGER NULL,
                    CreatedAt TEXT NOT NULL
                );
                INSERT INTO PersonRequest
                    (FullName, Rut, Comuna, SourceMessageId, SourceSubject, SourceSender, NeedsReview, Status, ReceivedAt, CreatedAt)
                VALUES
                    ('GUSTAVO ANDRÉS PEÑA CASTRO', '18.785.387-7', 'Catemu', 'msg-legacy', 'Solicitud', 'a@b.cl', 0, 'Uploaded', '2024-01-01T00:00:00+00:00', '2024-01-01T00:00:00+00:00');
                """;
            create.ExecuteNonQuery();
        }

        var legacyRepository = new PersonRequestRepository($"Data Source={dbPath}");
        TestDatabase.RerunBaseline(dbPath);

        // BoxId is added (as NULL) by EnsureColumnExists before the rebuild strips the UNIQUE
        // constraint, so this proves the rebuild's hand-written column list still carries it —
        // it round-trips as null here since there's no Box row to point it at yet.
        var stored = legacyRepository.GetAll().Single();
        Assert.Null(stored.BoxId);
        Assert.Equal("msg-legacy", stored.SourceMessageId);
    }

    [Fact]
    public void Baseline_CalledTwice_IsIdempotent()
    {
        // Full backfill-from-old-MovedToF8At-data testing is skipped here: simulating a
        // pre-migration database with real MovedToF8At values populated via raw SQL is awkward
        // to set up cheaply in this test class (would require dropping/recreating the table with
        // the old shape and inserting rows with only the old columns). Instead this test proves
        // that re-running EnsureSchema (which is what the backfill migration runs inside) never
        // throws and never corrupts existing rows.
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);

        TestDatabase.Migrate(dbPath);

        var stored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.F8, stored.Destination);
    }

    /* Pending feature: f8-pdf-penultimas-carpetas
    [Fact]
    public void SetPenultimasCarpetasPdfGenerated_StoresTimestampIndependentlyOfSectorPdfGeneratedAt()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        var generatedAt = DateTimeOffset.UtcNow;

        repository.SetPenultimasCarpetasPdfGenerated(id, generatedAt);

        var stored = repository.FindById(id)!;
        Assert.Equal(generatedAt, stored.PenultimasCarpetasPdfGeneratedAt);
        Assert.Null(stored.SectorPdfGeneratedAt);
    }

    [Fact]
    public void SetSectorPdfGenerated_DoesNotAffectPenultimasCarpetasPdfGeneratedAt()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        var generatedAt = DateTimeOffset.UtcNow;
        repository.SetPenultimasCarpetasPdfGenerated(id, generatedAt);

        repository.SetSectorPdfGenerated(id, DateTimeOffset.UtcNow);

        Assert.Equal(generatedAt, repository.FindById(id)!.PenultimasCarpetasPdfGeneratedAt);
    }

    [Fact]
    public void SetPenultimasCarpetasPdfGenerated_WithNull_ClearsValue()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetPenultimasCarpetasPdfGenerated(id, DateTimeOffset.UtcNow);

        repository.SetPenultimasCarpetasPdfGenerated(id, null);

        Assert.Null(repository.FindById(id)!.PenultimasCarpetasPdfGeneratedAt);
    }

    [Fact]
    public void SetMarked_ReMarkingCase_ClearsPenultimasCarpetasPdfGeneratedAt()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetMarked(id, true);
        repository.SetPenultimasCarpetasPdfGenerated(id, DateTimeOffset.UtcNow);

        repository.SetMarked(id, false);
        repository.SetMarked(id, true);

        Assert.Null(repository.FindById(id)!.PenultimasCarpetasPdfGeneratedAt);
    }

    [Fact]
    public void Baseline_OnPreExistingTableWithoutPenultimasCarpetasPdfGeneratedAtColumn_AddsColumnWithoutDataLoss()
    {
        // Simulates a database created before the PenultimasCarpetasPdfGeneratedAt column existed.
        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                DROP TABLE PersonRequest;
                CREATE TABLE PersonRequest (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NULL,
                    Rut TEXT NULL,
                    Comuna TEXT NULL,
                    SourceMessageId TEXT NOT NULL,
                    SourceConversationId TEXT NULL,
                    SourceSubject TEXT NOT NULL,
                    SourceSender TEXT NOT NULL,
                    NeedsReview INTEGER NOT NULL,
                    Status TEXT NOT NULL,
                    ReceivedAt TEXT NOT NULL,
                    FechaUltimaCarpeta TEXT NULL,
                    UploadedAt TEXT NULL,
                    ConfirmedAt TEXT NULL,
                    ConfirmedByUserId INTEGER NULL,
                    CreatedAt TEXT NOT NULL,
                    Marked INTEGER NOT NULL DEFAULT 0,
                    SectorPdfGeneratedAt TEXT NULL
                );
                """;
            command.ExecuteNonQuery();
        }
        var preExistingId = repository.Insert(NewRequest("msg-old"));

        TestDatabase.RerunBaseline(dbPath); // re-adopt the rewritten legacy table

        var stored = repository.FindById(preExistingId);
        Assert.NotNull(stored);
        Assert.Null(stored!.PenultimasCarpetasPdfGeneratedAt);
        repository.SetPenultimasCarpetasPdfGenerated(preExistingId, DateTimeOffset.UtcNow);
        Assert.NotNull(repository.FindById(preExistingId)!.PenultimasCarpetasPdfGeneratedAt);
    }
    */

    [Fact]
    public void SetConfirmationBounced_ThenClear_RoundTrips()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        var bouncedAt = DateTimeOffset.UtcNow;

        repository.SetConfirmationBounced(id, bouncedAt);
        Assert.Equal(bouncedAt, repository.FindById(id)!.ConfirmationBouncedAt);

        repository.ClearConfirmationBounced(id);
        Assert.Null(repository.FindById(id)!.ConfirmationBouncedAt);
    }

    [Fact]
    public void FindConfirmedByRut_ReturnsOnlyConfirmedRowsForThatRut()
    {
        var confirmed = repository.Insert(NewRequest("msg-1", rut: "12.345.678-5"));
        repository.UpdateStatusToConfirmed(confirmed, DateTimeOffset.UtcNow);
        repository.Insert(NewRequest("msg-2", rut: "12.345.678-5")); // same RUT, still Pending
        var otherConfirmed = repository.Insert(NewRequest("msg-3", rut: "9.868.019-K"));
        repository.UpdateStatusToConfirmed(otherConfirmed, DateTimeOffset.UtcNow);

        var found = repository.FindConfirmedByRut("12.345.678-5");

        Assert.Single(found);
        Assert.Equal(confirmed, found[0].Id);
    }

    [Fact]
    public void FindConfirmedByRut_SamePersonConfirmedForTwoComunas_ReturnsBoth()
    {
        var a = repository.Insert(NewRequest("msg-1", rut: "12.345.678-5"));
        var b = repository.Insert(NewRequest("msg-2", rut: "12.345.678-5"));
        repository.UpdateStatusToConfirmed(a, DateTimeOffset.UtcNow);
        repository.UpdateStatusToConfirmed(b, DateTimeOffset.UtcNow);

        Assert.Equal(2, repository.FindConfirmedByRut("12.345.678-5").Count);
    }

    [Fact]
    public void RevertF8AndReturnToCasos_ReincorporatesCaseAsSoloCaja()
    {
        var id = repository.Insert(NewRequest("msg-f8"));
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);
        repository.SetFolderNotFound(id, true);
        repository.UpdateStatusToConfirmed(id, DateTimeOffset.UtcNow);

        repository.RevertF8AndReturnToCasos(id);

        var stored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.None, stored.Destination);
        Assert.Equal(RequestStatus.Pending, stored.Status);
        Assert.True(stored.SoloCaja);
        Assert.False(stored.FolderNotFound);
        Assert.False(stored.SinCarpeta);
        Assert.Null(stored.TransferredAt);

        // When subsequently sent to Caja
        repository.SetFechaUltimaCarpeta(id, new DateOnly(2024, 3, 15));
        repository.SendToCaja([id], DateTimeOffset.UtcNow);

        var inCaja = repository.FindById(id)!;
        Assert.Equal(CaseDestination.Caja, inCaja.Destination);
        Assert.Equal(RequestStatus.Confirmed, inCaja.Status);
        Assert.False(inCaja.SoloCaja);

        var queue = repository.GetCajaQueue();
        Assert.Contains(queue, c => c.Id == id);
    }

    [Fact]
    public void Insert_NewCase_ClosedWithoutFolderAtIsNull()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        Assert.Null(repository.FindById(id)!.ClosedWithoutFolderAt);
    }

    [Fact]
    public void CloseWithoutFolder_F8Case_ClosesAndMovesToSinCarpetas()
    {
        var id = repository.Insert(NewRequest("msg-f8"));
        repository.SetFolderNotFound(id, true);
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);
        var closedAt = new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

        repository.CloseWithoutFolder(id, closedAt);

        var stored = repository.FindById(id)!;
        Assert.Equal(closedAt, stored.ClosedWithoutFolderAt);
        Assert.False(stored.FolderNotFound);
        Assert.Equal(CaseDestination.SinCarpetas, stored.Destination);
        Assert.Equal(closedAt, stored.TransferredAt);
    }

    [Fact]
    public void SendToCaja_F8Case_MovesToQueueAsConfirmed()
    {
        var id = repository.Insert(NewRequest("msg-f8"));
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);

        repository.SendToCaja([id], DateTimeOffset.UtcNow);

        var stored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.Caja, stored.Destination);
        Assert.Equal(RequestStatus.Confirmed, stored.Status);
        Assert.Null(stored.BoxId);
        Assert.Contains(repository.GetCajaQueue(), c => c.Id == id);
    }

    [Fact]
    public void SendToCaja_ClosedWithoutFolder_IsNotMoved()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);
        repository.CloseWithoutFolder(id, DateTimeOffset.UtcNow);

        repository.SendToCaja([id], DateTimeOffset.UtcNow);

        Assert.Equal(CaseDestination.SinCarpetas, repository.FindById(id)!.Destination);
    }

    [Fact]
    public void SendToCaja_PendingCaseWithoutSoloCaja_IsNotMoved()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        repository.SendToCaja([id], DateTimeOffset.UtcNow);

        Assert.Equal(CaseDestination.None, repository.FindById(id)!.Destination);
    }

    [Fact]
    public void RevertF8AndReturnToCasos_UploadedF8_KeepsTypedDataAndClearsStatus()
    {
        var id = repository.Insert(NewRequest("msg-f8"));
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);
        repository.SetFolderNotFound(id, true);
        repository.SetCodigoF8(id, "F8-123");
        repository.SetFechaUltimaCarpeta(id, new DateOnly(2024, 3, 15));
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);
        repository.UpdateStatusToConfirmed(id, DateTimeOffset.UtcNow);

        repository.RevertF8AndReturnToCasos(id);

        var stored = repository.FindById(id)!;
        Assert.Equal("F8-123", stored.CodigoF8);
        Assert.Equal(new DateOnly(2024, 3, 15), stored.FechaUltimaCarpeta);
        Assert.Equal("GUSTAVO ANDRÉS PEÑA CASTRO", stored.FullName);
        Assert.Equal("18.785.387-7", stored.Rut);
        Assert.Null(stored.ConfirmedAt);
        Assert.Null(stored.UploadedAt);
        Assert.Equal(RequestStatus.Pending, stored.Status);
        Assert.True(stored.SoloCaja);
        Assert.Equal(CaseDestination.None, stored.Destination);
    }

    [Fact]
    public void RevertF8AndReturnToCasos_NotUploadedF8_ReachesSameState()
    {
        var id = repository.Insert(NewRequest("msg-f8"));
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);
        repository.SetFolderNotFound(id, true);
        repository.SetSinCarpeta(id);

        repository.RevertF8AndReturnToCasos(id);

        var stored = repository.FindById(id)!;
        Assert.Null(stored.CodigoF8);
        Assert.True(stored.SinCarpeta);
        Assert.Equal(RequestStatus.Pending, stored.Status);
        Assert.True(stored.SoloCaja);
        Assert.False(stored.FolderNotFound);
        Assert.Equal(CaseDestination.None, stored.Destination);
    }

    [Fact]
    public void CloseWithoutFolder_And_ReopenSinCarpetaToF8_WorksCorrectly()
    {
        var id = repository.Insert(NewRequest("msg-sc"));
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);
        repository.SetFolderNotFound(id, true);

        var closedAt = DateTimeOffset.UtcNow;
        repository.CloseWithoutFolder(id, closedAt);

        var closed = repository.FindById(id)!;
        Assert.NotNull(closed.ClosedWithoutFolderAt);
        Assert.False(closed.FolderNotFound);
        Assert.Equal(CaseDestination.SinCarpetas, closed.Destination);

        var reopenedAt = DateTimeOffset.UtcNow;
        repository.ReopenSinCarpetaToF8(id, reopenedAt);

        var reopened = repository.FindById(id)!;
        Assert.Null(reopened.ClosedWithoutFolderAt);
        Assert.True(reopened.FolderNotFound);
        Assert.Equal(CaseDestination.F8, reopened.Destination);
        Assert.NotNull(reopened.TransferredAt);
    }

    private static PersonRequest NewRequest(string sourceMessageId, string rut = "18.785.387-7") => new()
    {
        FullName = "GUSTAVO ANDRÉS PEÑA CASTRO",
        Rut = rut,
        Comuna = "Catemu",
        SourceMessageId = sourceMessageId,
        SourceConversationId = "conv-1",
        SourceSubject = "Solicitud de carpeta",
        SourceSender = "rfloresc@municatemu.cl",
        NeedsReview = false,
        Status = RequestStatus.Pending
    };

    public void Dispose() => TestDatabase.Cleanup(dbPath);
}
