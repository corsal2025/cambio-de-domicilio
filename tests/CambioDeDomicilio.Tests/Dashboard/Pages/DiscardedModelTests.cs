using System.Linq;
using CambioDeDomicilio.Dashboard.Pages;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Pages;

public class DiscardedModelTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"discarded-page-test-{Guid.NewGuid():N}.db");
    private readonly IDiscardedEmailRepository repository;
    private readonly DiscardedModel model;

    public DiscardedModelTests()
    {
        repository = new DiscardedEmailRepository($"Data Source={dbPath}");
        repository.EnsureSchema();
        model = new DiscardedModel(repository);
    }

    [Fact]
    public void OnGet_WithDiscardedEmails_ListsThem()
    {
        repository.Insert(new DiscardedEmail
        {
            SourceMessageId = "msg-1",
            SourceSubject = "Solicitud de carpeta",
            SourceSender = "alguien@dominiodesconocido.cl",
            Reason = "Dominio no reconocido en el directorio de comunas: dominiodesconocido.cl"
        });

        model.OnGet();

        var item = Assert.Single(model.Items);
        Assert.Equal("msg-1", item.SourceMessageId);
    }

    [Fact]
    public void OnPostDelete_RemovesOnlyThatRecord()
    {
        repository.Insert(new DiscardedEmail
        {
            SourceMessageId = "msg-1",
            SourceSubject = "Solicitud de carpeta",
            SourceSender = "alguien@dominiodesconocido.cl",
            Reason = "Dominio no reconocido"
        });
        repository.Insert(new DiscardedEmail
        {
            SourceMessageId = "msg-2",
            SourceSubject = "Solicitud de carpeta",
            SourceSender = "otro@dominiodesconocido.cl",
            Reason = "Dominio no reconocido"
        });
        var toDelete = repository.GetAll().Single(d => d.SourceMessageId == "msg-1");

        model.OnPostDelete(toDelete.Id);

        model.OnGet();
        var remaining = Assert.Single(model.Items);
        Assert.Equal("msg-2", remaining.SourceMessageId);
    }

    [Fact]
    public void OnPostDeleteAll_RemovesEveryRecord()
    {
        repository.Insert(new DiscardedEmail
        {
            SourceMessageId = "msg-1",
            SourceSubject = "Solicitud de carpeta",
            SourceSender = "alguien@dominiodesconocido.cl",
            Reason = "Dominio no reconocido"
        });
        repository.Insert(new DiscardedEmail
        {
            SourceMessageId = "msg-2",
            SourceSubject = "Solicitud de carpeta",
            SourceSender = "otro@dominiodesconocido.cl",
            Reason = "Dominio no reconocido"
        });

        model.OnPostDeleteAll();

        model.OnGet();
        Assert.Empty(model.Items);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(dbPath);
    }
}
