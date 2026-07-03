using Microsoft.Data.Sqlite;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Persistence;
using Xunit;

namespace OutlookComunaRouter.Tests.Persistence;

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
    public void Sector_WithoutFecha_IsNull()
    {
        var id = repository.Insert(NewRequest("msg-1"));

        Assert.Null(repository.FindById(id)!.Sector);
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
