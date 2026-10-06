using CambioDeDomicilio.Dashboard.Pages;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Pages;

public class SinCarpetasModelTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"sin-carpetas-page-test-{Guid.NewGuid():N}.db");
    private readonly IPersonRequestRepository repository;
    private readonly SinCarpetasModel model;

    public SinCarpetasModelTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        repository.EnsureSchema();
        model = new SinCarpetasModel(repository);
    }

    [Fact]
    public void OnGet_ListsEveryCaseClosedWithoutFolder()
    {
        var closedIds = new[] { "msg-1", "msg-2", "msg-3" }
            .Select(messageId => repository.Insert(NewRequest(messageId)))
            .ToList();
        var untouchedId = repository.Insert(NewRequest("msg-untouched"));
        closedIds.ForEach(id => repository.CloseWithoutFolder(id, DateTimeOffset.UtcNow));

        model.OnGet();

        Assert.Equal(closedIds.Count, model.TotalCount);
        Assert.Equal(closedIds.OrderBy(id => id), model.Cases.Select(c => c.Id).OrderBy(id => id));
        Assert.DoesNotContain(model.Cases, c => c.Id == untouchedId);
    }

    [Fact]
    public void Page_ExposesNoHandlerThatMovesACaseOutOfSinCarpetas()
    {
        var postHandlers = typeof(SinCarpetasModel).GetMethods()
            .Where(m => m.Name.StartsWith("OnPost", StringComparison.Ordinal))
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(postHandlers);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(dbPath)) File.Delete(dbPath);
    }

    private static PersonRequest NewRequest(string sourceMessageId) => new()
    {
        FullName = "PERSONA DE PRUEBA",
        Rut = "18.785.387-7",
        Comuna = "Catemu",
        SourceMessageId = sourceMessageId,
        SourceSubject = "Cambio de domicilio",
        SourceSender = "comuna@example.cl",
    };
}
