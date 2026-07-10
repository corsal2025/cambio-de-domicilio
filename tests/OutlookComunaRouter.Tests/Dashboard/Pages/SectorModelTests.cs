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

    [Fact]
    public void OnGet_ExcludesCasesAlreadyGeneratedInAPreviousPdf()
    {
        var printedId = repository.Insert(NewRequest("msg-1", "Ya Impreso"));
        repository.SetFechaUltimaCarpeta(printedId, new DateOnly(2022, 1, 1));
        repository.SetSectorPdfGenerated(printedId, DateTimeOffset.UtcNow);

        var freshId = repository.Insert(NewRequest("msg-2", "Caso Nuevo"));
        repository.SetFechaUltimaCarpeta(freshId, new DateOnly(2022, 1, 1));

        var model = new SectorModel(repository);
        model.OnGet(FolderSector.Archivo);

        var result = Assert.Single(model.Cases);
        Assert.Equal("Caso Nuevo", result.FullName);
    }

    [Fact]
    public void OnPostMarkPrinted_MarksAllCurrentlyVisibleCasesInThatSector()
    {
        var archivoId = repository.Insert(NewRequest("msg-1", "Persona Archivo"));
        repository.SetFechaUltimaCarpeta(archivoId, new DateOnly(2022, 1, 1));
        var oficinaId = repository.Insert(NewRequest("msg-2", "Persona Oficina"));
        repository.SetFechaUltimaCarpeta(oficinaId, new DateOnly(2024, 1, 1));

        var model = new SectorModel(repository);
        model.OnPostMarkPrinted(FolderSector.Archivo);

        Assert.NotNull(repository.FindById(archivoId)!.SectorPdfGeneratedAt);
        Assert.Null(repository.FindById(oficinaId)!.SectorPdfGeneratedAt); // other sector untouched

        model.OnGet(FolderSector.Archivo);
        Assert.Empty(model.Cases); // the sector view is now blank until a new case arrives
    }

    [Fact]
    public void OnPostRemoveOne_ExcludesOnlyThatCaseFromSectorView()
    {
        var toRemoveId = repository.Insert(NewRequest("msg-1", "Persona A"));
        repository.SetFechaUltimaCarpeta(toRemoveId, new DateOnly(2022, 1, 1));
        var toKeepId = repository.Insert(NewRequest("msg-2", "Persona B"));
        repository.SetFechaUltimaCarpeta(toKeepId, new DateOnly(2022, 1, 1));

        var model = new SectorModel(repository);
        model.OnPostRemoveOne(toRemoveId, FolderSector.Archivo);

        Assert.NotNull(repository.FindById(toRemoveId)!.SectorPdfGeneratedAt); // excluded from Sector view
        Assert.Null(repository.FindById(toKeepId)!.SectorPdfGeneratedAt); // untouched

        model.OnGet(FolderSector.Archivo);
        var result = Assert.Single(model.Cases);
        Assert.Equal("Persona B", result.FullName);
    }

    [Fact]
    public void OnPostClearAll_MarksEveryVisibleCaseInThatSectorOnly()
    {
        var archivoId1 = repository.Insert(NewRequest("msg-1", "Persona A"));
        repository.SetFechaUltimaCarpeta(archivoId1, new DateOnly(2022, 1, 1));
        var archivoId2 = repository.Insert(NewRequest("msg-2", "Persona B"));
        repository.SetFechaUltimaCarpeta(archivoId2, new DateOnly(2022, 1, 1));
        var oficinaId = repository.Insert(NewRequest("msg-3", "Persona Oficina"));
        repository.SetFechaUltimaCarpeta(oficinaId, new DateOnly(2024, 1, 1));

        var model = new SectorModel(repository);
        model.OnPostClearAll(FolderSector.Archivo);

        Assert.NotNull(repository.FindById(archivoId1)!.SectorPdfGeneratedAt);
        Assert.NotNull(repository.FindById(archivoId2)!.SectorPdfGeneratedAt);
        Assert.Null(repository.FindById(oficinaId)!.SectorPdfGeneratedAt); // other sector untouched

        model.OnGet(FolderSector.Archivo);
        Assert.Empty(model.Cases);
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
