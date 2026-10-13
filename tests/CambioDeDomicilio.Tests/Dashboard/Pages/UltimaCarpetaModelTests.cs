using Microsoft.AspNetCore.Mvc;
using CambioDeDomicilio.Dashboard.Pages;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Pages;

public class UltimaCarpetaModelTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"ultima-carpeta-page-test-{Guid.NewGuid():N}.db");
    private readonly IPersonRequestRepository repository;
    private readonly UltimaCarpetaModel model;

    public UltimaCarpetaModelTests()
    {
        TestDatabase.Migrate(dbPath);
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        model = new UltimaCarpetaModel(repository);
    }

    [Fact]
    public void OnGet_ListsOnlyUltimaCarpetaCasesOldestTransferFirst()
    {
        var first = InUltimaCarpeta("m1", "PRIMERO UNO", new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero));
        var second = InUltimaCarpeta("m2", "SEGUNDO DOS", new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero));
        var stillInF8 = repository.Insert(NewRequest("m3", "TERCERO F8"));
        repository.SetDestination(stillInF8, CaseDestination.F8, DateTimeOffset.UtcNow);
        repository.Insert(NewRequest("m4", "CUARTO CASOS"));

        model.OnGet();

        Assert.Equal(new[] { first, second }, model.Cases.Select(c => c.Id).ToArray());
    }

    [Fact]
    public void OnGet_Search_FiltersTheList()
    {
        InUltimaCarpeta("m1", "ANA RIVERA", DateTimeOffset.UtcNow);
        InUltimaCarpeta("m2", "LUIS TORRES", DateTimeOffset.UtcNow);

        model.OnGet(search: "rivera");

        Assert.Equal("ANA RIVERA", Assert.Single(model.Cases).FullName);
        Assert.Equal(2, model.TotalCount);
    }

    [Fact]
    public void OnPostMarkRequested_RecordsTheRequestDate()
    {
        var id = InUltimaCarpeta("m1", "ANA RIVERA", DateTimeOffset.UtcNow);

        model.OnPostMarkRequested(id);

        Assert.NotNull(repository.FindById(id)!.UltimaCarpetaRequestedAt);
    }

    [Fact]
    public void OnPostUndoRequested_ClearsTheRequestDate()
    {
        var id = InUltimaCarpeta("m1", "ANA RIVERA", DateTimeOffset.UtcNow);
        repository.SetUltimaCarpetaRequested(id, DateTimeOffset.UtcNow);

        model.OnPostUndoRequested(id);

        Assert.Null(repository.FindById(id)!.UltimaCarpetaRequestedAt);
    }

    [Fact]
    public void OnPostSendToCaja_MovesTheCaseToTheCajaQueue()
    {
        var id = InUltimaCarpeta("m1", "ANA RIVERA", DateTimeOffset.UtcNow);

        Assert.IsType<RedirectToPageResult>(model.OnPostSendToCaja(id));

        var stored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.Caja, stored.Destination);
        Assert.Contains(repository.GetCajaQueue(), c => c.Id == id);
    }

    [Fact]
    public void OnPostCloseWithoutFolder_MovesTheCaseToSinCarpetasAndEndsThere()
    {
        var id = InUltimaCarpeta("m1", "ANA RIVERA", DateTimeOffset.UtcNow);

        var result = Assert.IsType<RedirectToPageResult>(model.OnPostCloseWithoutFolder(id));

        Assert.Equal("/SinCarpetas", result.PageName);
        var stored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.SinCarpetas, stored.Destination);
        Assert.NotNull(stored.ClosedWithoutFolderAt);
        Assert.True(stored.SinCarpeta);
        model.OnGet();
        Assert.Empty(model.Cases);
    }

    [Fact]
    public void Page_ExposesNoHandlerThatSendsEmail()
    {
        var handlers = typeof(UltimaCarpetaModel).GetMethods()
            .Where(m => m.Name.StartsWith("OnPost", StringComparison.Ordinal))
            .Select(m => m.Name)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(new[] { "OnPostCloseWithoutFolder", "OnPostMarkRequested", "OnPostSendToCaja", "OnPostUndoRequested" }, handlers);
    }

    private long InUltimaCarpeta(string messageId, string name, DateTimeOffset transferredAt)
    {
        var id = repository.Insert(NewRequest(messageId, name));
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);
        repository.SendToUltimaCarpeta(id, transferredAt);
        return id;
    }

    private static PersonRequest NewRequest(string messageId, string name) => new()
    {
        FullName = name,
        Rut = "11.111.111-1",
        Comuna = "Catemu",
        SourceMessageId = messageId,
        SourceSubject = "Cambio de domicilio",
        SourceSender = "comuna@example.cl",
    };

    public void Dispose() => TestDatabase.Cleanup(dbPath);
}
