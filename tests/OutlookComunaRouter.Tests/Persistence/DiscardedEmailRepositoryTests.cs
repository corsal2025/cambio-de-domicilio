using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Persistence;
using Xunit;

namespace OutlookComunaRouter.Tests.Persistence;

public class DiscardedEmailRepositoryTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"discarded-test-{Guid.NewGuid():N}.db");
    private readonly IDiscardedEmailRepository repository;

    public DiscardedEmailRepositoryTests()
    {
        repository = new DiscardedEmailRepository($"Data Source={dbPath}");
        repository.EnsureSchema();
    }

    [Fact]
    public void ExistsBySourceMessageId_AfterInsert_ReturnsTrue()
    {
        repository.Insert(NewDiscarded("msg-1"));

        Assert.True(repository.ExistsBySourceMessageId("msg-1"));
        Assert.False(repository.ExistsBySourceMessageId("msg-unknown"));
    }

    [Fact]
    public void GetAll_MultipleInserts_ReturnsNewestFirst()
    {
        repository.Insert(NewDiscarded("msg-1"));
        repository.Insert(NewDiscarded("msg-2"));

        var all = repository.GetAll();

        Assert.Equal(2, all.Count);
        Assert.Equal("msg-2", all[0].SourceMessageId);
        Assert.Equal("msg-1", all[1].SourceMessageId);
    }

    private static DiscardedEmail NewDiscarded(string messageId) => new()
    {
        SourceMessageId = messageId,
        SourceSubject = "Solicitud de carpeta",
        SourceSender = "alguien@dominiodesconocido.cl",
        Reason = "Dominio no reconocido en el directorio de comunas: dominiodesconocido.cl"
    };

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(dbPath);
    }
}
