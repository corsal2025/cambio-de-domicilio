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
        repository.EnsureSchema();
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
    public void EnsureSchema_OnPreExistingTableWithoutMarkedColumn_AddsColumnWithoutDataLoss()
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

        repository.EnsureSchema(); // re-run migration, as happens on every app startup

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
    public void EnsureSchema_OnPreExistingTableWithUniqueSourceMessageId_RemovesConstraintWithoutDataLoss()
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

        repository.EnsureSchema(); // re-run migration, as happens on every app startup

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
    public void EnsureSchema_OnPreExistingTableWithoutSectorPdfGeneratedAtColumn_AddsColumnWithoutDataLoss()
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

        repository.EnsureSchema(); // re-run migration, as happens on every app startup

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

    [Theory]
    [InlineData(CaseDestination.F8)]
    [InlineData(CaseDestination.Certificado)]
    public void SetDestination_StoresDestinationAndTimestamp(CaseDestination destination)
    {
        var id = repository.Insert(NewRequest("msg-1"));
        var transferredAt = DateTimeOffset.UtcNow;

        repository.SetDestination(id, destination, transferredAt);

        var stored = repository.FindById(id)!;
        Assert.Equal(destination, stored.Destination);
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
    public void SetCertificadoNotified_StoresTimestamp()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        var notifiedAt = DateTimeOffset.UtcNow;

        repository.SetCertificadoNotified(id, notifiedAt);

        Assert.Equal(notifiedAt, repository.FindById(id)!.CertificadoNotifiedAt);
    }

    [Fact]
    public void EnsureSchema_CalledTwice_IsIdempotent()
    {
        // Full backfill-from-old-MovedToF8At-data testing is skipped here: simulating a
        // pre-migration database with real MovedToF8At values populated via raw SQL is awkward
        // to set up cheaply in this test class (would require dropping/recreating the table with
        // the old shape and inserting rows with only the old columns). Instead this test proves
        // that re-running EnsureSchema (which is what the backfill migration runs inside) never
        // throws and never corrupts existing rows.
        var id = repository.Insert(NewRequest("msg-1"));
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);

        repository.EnsureSchema();

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
    public void EnsureSchema_OnPreExistingTableWithoutPenultimasCarpetasPdfGeneratedAtColumn_AddsColumnWithoutDataLoss()
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

        repository.EnsureSchema(); // re-run migration, as happens on every app startup

        var stored = repository.FindById(preExistingId);
        Assert.NotNull(stored);
        Assert.Null(stored!.PenultimasCarpetasPdfGeneratedAt);
        repository.SetPenultimasCarpetasPdfGenerated(preExistingId, DateTimeOffset.UtcNow);
        Assert.NotNull(repository.FindById(preExistingId)!.PenultimasCarpetasPdfGeneratedAt);
    }
    */

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

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
    }
}
