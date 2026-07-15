using Microsoft.Data.Sqlite;
using OutlookComunaRouter.Dashboard.Pages;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Persistence;
using Xunit;

namespace OutlookComunaRouter.Tests.Dashboard.Pages;

public class SectorF8ModelTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"sector-f8-page-test-{Guid.NewGuid():N}.db");
    private readonly IPersonRequestRepository repository;

    public SectorF8ModelTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        repository.EnsureSchema();
    }

    [Fact]
    public void OnGet_ExcludesCasesNotYetTransferredToF8()
    {
        var transferredId = repository.Insert(NewRequest("msg-1", "Persona Traspasada"));
        repository.SetFechaUltimaCarpeta(transferredId, new DateOnly(2022, 1, 1));
        repository.SetMarked(transferredId, true);
        repository.SetDestination(transferredId, CaseDestination.F8, DateTimeOffset.UtcNow);

        var notTransferredId = repository.Insert(NewRequest("msg-2", "Persona Sin Traspasar"));
        repository.SetFechaUltimaCarpeta(notTransferredId, new DateOnly(2022, 1, 1));
        repository.SetMarked(notTransferredId, true);

        var model = new SectorF8Model(repository);
        model.OnGet(FolderSector.Archivo);

        var result = Assert.Single(model.Cases);
        Assert.Equal("Persona Traspasada", result.FullName);
    }

    [Fact]
    public void OnGet_FiltersToRequestedSectorOnly()
    {
        var archivoId = repository.Insert(NewRequest("msg-1", "Persona Archivo"));
        repository.SetFechaUltimaCarpeta(archivoId, new DateOnly(2022, 1, 1));
        repository.SetMarked(archivoId, true);
        repository.SetDestination(archivoId, CaseDestination.F8, DateTimeOffset.UtcNow);

        var oficinaId = repository.Insert(NewRequest("msg-2", "Persona Oficina"));
        repository.SetFechaUltimaCarpeta(oficinaId, new DateOnly(2024, 1, 1));
        repository.SetMarked(oficinaId, true);
        repository.SetDestination(oficinaId, CaseDestination.F8, DateTimeOffset.UtcNow);

        var model = new SectorF8Model(repository);
        model.OnGet(FolderSector.Archivo);

        var result = Assert.Single(model.Cases);
        Assert.Equal("Persona Archivo", result.FullName);
    }

    [Fact]
    public void OnGet_ExcludesUnmarkedCases()
    {
        var markedId = repository.Insert(NewRequest("msg-1", "Persona Marcada"));
        repository.SetFechaUltimaCarpeta(markedId, new DateOnly(2022, 1, 1));
        repository.SetMarked(markedId, true);
        repository.SetDestination(markedId, CaseDestination.F8, DateTimeOffset.UtcNow);

        var unmarkedId = repository.Insert(NewRequest("msg-2", "Persona Sin Marcar"));
        repository.SetFechaUltimaCarpeta(unmarkedId, new DateOnly(2022, 1, 1));
        repository.SetDestination(unmarkedId, CaseDestination.F8, DateTimeOffset.UtcNow);

        var model = new SectorF8Model(repository);
        model.OnGet(FolderSector.Archivo);

        var result = Assert.Single(model.Cases);
        Assert.Equal("Persona Marcada", result.FullName);
    }

    [Fact]
    public void OnPostMarkPrinted_MarksEveryCurrentlyVisibleCaseInThatSector()
    {
        var archivoId = repository.Insert(NewRequest("msg-1", "Persona Archivo"));
        repository.SetFechaUltimaCarpeta(archivoId, new DateOnly(2022, 1, 1));
        repository.SetMarked(archivoId, true);
        repository.SetDestination(archivoId, CaseDestination.F8, DateTimeOffset.UtcNow);

        var model = new SectorF8Model(repository);
        model.OnPostMarkPrinted(FolderSector.Archivo);

        Assert.NotNull(repository.FindById(archivoId)!.SectorPdfGeneratedAt);

        model.OnGet(FolderSector.Archivo);
        Assert.Empty(model.Cases);
    }

    [Fact]
    public void OnPostRemoveOne_ExcludesOnlyThatCaseFromSectorView()
    {
        var toRemoveId = repository.Insert(NewRequest("msg-1", "Persona A"));
        repository.SetFechaUltimaCarpeta(toRemoveId, new DateOnly(2022, 1, 1));
        repository.SetMarked(toRemoveId, true);
        repository.SetDestination(toRemoveId, CaseDestination.F8, DateTimeOffset.UtcNow);

        var toKeepId = repository.Insert(NewRequest("msg-2", "Persona B"));
        repository.SetFechaUltimaCarpeta(toKeepId, new DateOnly(2022, 1, 1));
        repository.SetMarked(toKeepId, true);
        repository.SetDestination(toKeepId, CaseDestination.F8, DateTimeOffset.UtcNow);

        var model = new SectorF8Model(repository);
        model.OnPostRemoveOne(toRemoveId, FolderSector.Archivo);

        Assert.NotNull(repository.FindById(toRemoveId)!.SectorPdfGeneratedAt);
        Assert.Null(repository.FindById(toKeepId)!.SectorPdfGeneratedAt);

        model.OnGet(FolderSector.Archivo);
        var result = Assert.Single(model.Cases);
        Assert.Equal("Persona B", result.FullName);
    }

    [Fact]
    public void OnPostClearAll_MarksEveryVisibleCaseInThatSectorOnly()
    {
        var archivoId = repository.Insert(NewRequest("msg-1", "Persona A"));
        repository.SetFechaUltimaCarpeta(archivoId, new DateOnly(2022, 1, 1));
        repository.SetMarked(archivoId, true);
        repository.SetDestination(archivoId, CaseDestination.F8, DateTimeOffset.UtcNow);

        var oficinaId = repository.Insert(NewRequest("msg-2", "Persona Oficina"));
        repository.SetFechaUltimaCarpeta(oficinaId, new DateOnly(2024, 1, 1));
        repository.SetMarked(oficinaId, true);
        repository.SetDestination(oficinaId, CaseDestination.F8, DateTimeOffset.UtcNow);

        var model = new SectorF8Model(repository);
        model.OnPostClearAll(FolderSector.Archivo);

        Assert.NotNull(repository.FindById(archivoId)!.SectorPdfGeneratedAt);
        Assert.Null(repository.FindById(oficinaId)!.SectorPdfGeneratedAt);

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
