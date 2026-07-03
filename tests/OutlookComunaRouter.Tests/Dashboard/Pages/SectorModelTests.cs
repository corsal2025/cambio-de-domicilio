using Microsoft.Data.Sqlite;
using OutlookComunaRouter.Dashboard.Pages;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Persistence;
using Xunit;

namespace OutlookComunaRouter.Tests.Dashboard.Pages;

public class SectorModelTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"sector-page-test-{Guid.NewGuid():N}.db");
    private readonly IPersonRequestRepository repository;

    public SectorModelTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        repository.EnsureSchema();
    }

    [Fact]
    public void OnGet_FiltersToRequestedSectorOnly()
    {
        var archivoId = repository.Insert(NewRequest("msg-1", "Persona Archivo"));
        repository.SetFechaUltimaCarpeta(archivoId, new DateOnly(2022, 1, 1));

        var oficinaId = repository.Insert(NewRequest("msg-2", "Persona Oficina"));
        repository.SetFechaUltimaCarpeta(oficinaId, new DateOnly(2024, 1, 1));

        var withoutFechaId = repository.Insert(NewRequest("msg-3", "Persona Sin Fecha"));

        var model = new SectorModel(repository);
        model.OnGet(FolderSector.Archivo);

        var result = Assert.Single(model.Cases);
        Assert.Equal("Persona Archivo", result.FullName);
    }

    private static PersonRequest NewRequest(string sourceMessageId, string fullName) => new()
    {
        FullName = fullName,
        Rut = "18.785.387-7",
        Comuna = "Catemu",
        SourceMessageId = sourceMessageId,
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
