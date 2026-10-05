using CambioDeDomicilio.Dashboard.Pages;
using CambioDeDomicilio.Persistence;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Pages;

public class ManualModelTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"manual-test-{Guid.NewGuid():N}.db");
    private readonly IPersonRequestRepository repository;

    public ManualModelTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        repository.EnsureSchema();
    }

    [Fact]
    public void OnGet_ExecutesWithoutError()
    {
        var model = new ManualModel(repository);
        model.OnGet();
        Assert.Equal(0, model.TotalCasos);
        Assert.Equal(0, model.CasosEnCaja);
        Assert.Equal(0, model.CasosSinCarpeta);
    }

    public void Dispose()
    {
        try { if (File.Exists(dbPath)) File.Delete(dbPath); } catch { }
    }
}
