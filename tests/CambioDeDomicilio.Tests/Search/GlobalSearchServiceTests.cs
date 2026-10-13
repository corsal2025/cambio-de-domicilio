using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Search;
using Xunit;

namespace CambioDeDomicilio.Tests.Search;

public class GlobalSearchServiceTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"global-search-test-{Guid.NewGuid():N}.db");
    private readonly PersonRequestRepository repository;
    private readonly BoxRepository boxes;
    private readonly DiscardedEmailRepository discarded;
    private readonly GlobalSearchService sut;

    private static readonly IReadOnlyList<ComunaContact> NoContacts = [];

    public GlobalSearchServiceTests()
    {
        TestDatabase.Migrate(dbPath);
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        boxes = new BoxRepository($"Data Source={dbPath}");
        discarded = new DiscardedEmailRepository($"Data Source={dbPath}");
        sut = new GlobalSearchService(repository, boxes, discarded);
    }

    [Fact]
    public void Search_CaseStillInCasos_IsReportedUnderCasos()
    {
        Insert("m1", "PEDRO PEREZ SOTO", "11.111.111-1");

        var result = sut.Search("perez", NoContacts);

        var hit = Assert.Single(result.Hits);
        Assert.Equal(SearchModule.Casos, hit.Module);
    }

    [Fact]
    public void Search_SameQuery_FindsPeopleInEveryModule()
    {
        Insert("casos", "ANA RIVERA CASOS", "11.111.111-1");
        var f8 = Insert("f8", "ANA RIVERA F8", "22.222.222-2");
        repository.SetDestination(f8, CaseDestination.F8, DateTimeOffset.UtcNow);
        var subidas = Insert("subidas", "ANA RIVERA SUBIDAS", "33.333.333-3");
        repository.SetDestination(subidas, CaseDestination.Subidas, DateTimeOffset.UtcNow);
        var sin = Insert("sin", "ANA RIVERA SIN", "44.444.444-4");
        repository.CloseWithoutFolder(sin, DateTimeOffset.UtcNow);
        var caja = Insert("caja", "ANA RIVERA CAJA", "55.555.555-5");
        repository.UpdateStatusToConfirmed(caja, DateTimeOffset.UtcNow);
        repository.SendToCaja([caja], DateTimeOffset.UtcNow);

        var result = sut.Search("rivera", NoContacts);

        Assert.Equal(
            new[] { SearchModule.Casos, SearchModule.F8, SearchModule.Subidas, SearchModule.Caja, SearchModule.SinCarpetas },
            result.Hits.Select(h => h.Module).OrderBy(m => m).ToArray());
    }

    [Theory]
    [InlineData("187853877")]
    [InlineData("18.785.387-7")]
    [InlineData("18785387-7")]
    [InlineData("785.387")]
    public void Search_RutWithOrWithoutSeparators_FindsTheCase(string query)
    {
        Insert("m1", "JUAN TORRES", "18.785.387-7");

        Assert.Single(sut.Search(query, NoContacts).Hits);
    }

    [Fact]
    public void Search_NameWithoutAccents_FindsAccentedName()
    {
        Insert("m1", "FELIPE IGNACIO MELÉNDEZ ACEVEDO", "11.111.111-1");

        Assert.Single(sut.Search("melendez", NoContacts).Hits);
    }

    [Fact]
    public void Search_CodigoF8_FindsTheCase()
    {
        var id = Insert("m1", "LUIS GARCIA", "11.111.111-1");
        repository.SetDestination(id, CaseDestination.F8, DateTimeOffset.UtcNow);
        repository.SetCodigoF8(id, "20582927");

        var hit = Assert.Single(sut.Search("20582927", NoContacts).Hits);
        Assert.Equal(SearchModule.F8, hit.Module);
    }

    [Fact]
    public void Search_CaseInClosedBox_ShowsBoxCodeAndPosition()
    {
        var first = Insert("m1", "PRIMERO UNO", "11.111.111-1");
        var second = Insert("m2", "SEGUNDO DOS", "22.222.222-2");
        foreach (var id in new[] { first, second })
        {
            repository.UpdateStatusToConfirmed(id, DateTimeOffset.UtcNow);
            repository.SendToCaja([id], DateTimeOffset.UtcNow);
        }
        boxes.CloseBox("A1-CD", DateTimeOffset.UtcNow);

        var hit = Assert.Single(sut.Search("SEGUNDO", NoContacts).Hits);

        Assert.Equal(SearchModule.Caja, hit.Module);
        Assert.Contains("A1-CD", hit.Detail);
        Assert.Contains("N° 2", hit.Detail);
    }

    [Fact]
    public void Search_CaseInOpenCajaQueue_SaysQueue()
    {
        var id = Insert("m1", "EN COLA UNO", "11.111.111-1");
        repository.UpdateStatusToConfirmed(id, DateTimeOffset.UtcNow);
        repository.SendToCaja([id], DateTimeOffset.UtcNow);

        var hit = Assert.Single(sut.Search("EN COLA", NoContacts).Hits);

        Assert.Equal(SearchModule.Caja, hit.Module);
        Assert.Contains("cola", hit.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Search_DiscardedEmail_MatchesSenderSubjectAndReason()
    {
        discarded.Insert(new DiscardedEmail
        {
            SourceMessageId = "d1",
            SourceSender = "tramites@muniexample.cl",
            SourceSubject = "Solicitud carpeta JUAN SOTO",
            Reason = "Dominio no reconocido en el directorio de comunas: muniexample.cl"
        });

        var bySender = Assert.Single(sut.Search("muniexample", NoContacts).Hits);
        Assert.Equal(SearchModule.Descartados, bySender.Module);
        Assert.Single(sut.Search("juan soto", NoContacts).Hits);
    }

    [Fact]
    public void Search_ComunaDirectory_MatchesName()
    {
        var contacts = new List<ComunaContact> { new("Catemu", "rfloresc@municatemu.cl", "municatemu.cl") };

        var result = sut.Search("catemu", contacts);

        Assert.Contains(result.Hits, h => h.Module == SearchModule.Comunas);
    }

    [Fact]
    public void Search_NothingMatches_ReturnsEmptyAndListsEverySearchedModule()
    {
        Insert("m1", "PEDRO PEREZ", "11.111.111-1");

        var result = sut.Search("zzzzzz", NoContacts);

        Assert.Empty(result.Hits);
        Assert.Equal(7, result.SearchedModules.Count);
    }

    [Fact]
    public void Search_TooShortQuery_ReturnsNothing()
    {
        Insert("m1", "PEDRO PEREZ", "11.111.111-1");

        Assert.Empty(sut.Search("p", NoContacts).Hits);
    }

    private long Insert(string messageId, string name, string rut) => repository.Insert(new PersonRequest
    {
        FullName = name,
        Rut = rut,
        Comuna = "Catemu",
        SourceMessageId = messageId,
        SourceSubject = "Cambio de domicilio",
        SourceSender = "comuna@example.cl",
    });

    public void Dispose() => TestDatabase.Cleanup(dbPath);
}
