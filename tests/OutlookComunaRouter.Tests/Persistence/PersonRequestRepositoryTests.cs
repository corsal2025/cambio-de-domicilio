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
        var request = NewRequest("msg-1");
        repository.Insert(request);

        Assert.True(repository.ExistsBySourceMessageId("msg-1"));
        Assert.False(repository.ExistsBySourceMessageId("msg-unknown"));
    }

    [Fact]
    public void FindActiveByRutAndComuna_AfterSent_ReturnsRecord()
    {
        var request = NewRequest("msg-1");
        var id = repository.Insert(request);
        repository.UpdateStatusToSent(id, "n/a", DateTimeOffset.UtcNow);

        var found = repository.FindActiveByRutAndComuna("18.785.387-7", "Catemu");

        Assert.NotNull(found);
        Assert.Equal(RequestStatus.Sent, found!.Status);
    }

    [Fact]
    public void FindActiveByRutAndComuna_OnlyPending_ReturnsNull()
    {
        repository.Insert(NewRequest("msg-1"));

        var found = repository.FindActiveByRutAndComuna("18.785.387-7", "Catemu");

        Assert.Null(found);
    }

    [Fact]
    public void UpdateStatusToResponded_SetsStatusAndTimestamps()
    {
        var id = repository.Insert(NewRequest("msg-1"));
        repository.UpdateStatusToSent(id, "n/a", DateTimeOffset.UtcNow);

        repository.UpdateStatusToResponded(id, "reply-msg", DateTimeOffset.UtcNow, "2026-06-01");

        var all = repository.GetAll();
        Assert.Equal(RequestStatus.Responded, all[0].Status);
        Assert.Equal("2026-06-01", all[0].LastFolderDate);
    }

    private static PersonRequest NewRequest(string sourceMessageId) => new()
    {
        FullName = "GUSTAVO ANDRÉS PEÑA CASTRO",
        Rut = "18.785.387-7",
        Comuna = "Catemu",
        SourceMessageId = sourceMessageId,
        SourceConversationId = "conv-1",
        SourceSubject = "Cambio de domicilio",
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
