using OutlookComunaRouter.Dashboard.Pages;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Persistence;
using Xunit;

namespace OutlookComunaRouter.Tests.Dashboard.Pages;

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

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(dbPath);
    }
}
