using Microsoft.Data.Sqlite;
using CambioDeDomicilio.Dashboard.Pages;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Pages;

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
        repository.SetMarked(archivoId, true);

        var oficinaId = repository.Insert(NewRequest("msg-2", "Persona Oficina"));
        repository.SetFechaUltimaCarpeta(oficinaId, new DateOnly(2024, 1, 1));
        repository.SetMarked(oficinaId, true);

        var withoutFechaId = repository.Insert(NewRequest("msg-3", "Persona Sin Fecha"));
        repository.SetMarked(withoutFechaId, true);

        var model = new SectorModel(repository);
        model.OnGet(FolderSector.Archivo);

        var result = Assert.Single(model.Cases);
        Assert.Equal("Persona Archivo", result.FullName);
    }

    [Fact]
    public void OnGet_ExcludesUnmarkedCases()
    {
        // Marcar (checked on Casos) is how the operator picks which contributors get
        // categorized into a sector document — an unmarked case with a valid sector never
        // shows up here, even though it would otherwise qualify by FechaUltimaCarpeta alone.
        var markedId = repository.Insert(NewRequest("msg-1", "Persona Marcada"));
        repository.SetFechaUltimaCarpeta(markedId, new DateOnly(2022, 1, 1));
        repository.SetMarked(markedId, true);

        var unmarkedId = repository.Insert(NewRequest("msg-2", "Persona Sin Marcar"));
        repository.SetFechaUltimaCarpeta(unmarkedId, new DateOnly(2022, 1, 1));

        var model = new SectorModel(repository);
        model.OnGet(FolderSector.Archivo);

        var result = Assert.Single(model.Cases);
        Assert.Equal("Persona Marcada", result.FullName);
    }

    [Fact]
    public void OnGet_ExcludesCasesFlaggedPendienteCarpetaAloneWithoutMarcar()
    {
        // Only the "Marcar" checkbox selects a case for the sector document — Pendiente Carpeta
        // alone is not enough, since it means something else on the Casos screen.
        var pendienteId = repository.Insert(NewRequest("msg-1", "Persona Pendiente Carpeta"));
        repository.SetFechaUltimaCarpeta(pendienteId, new DateOnly(2022, 1, 1));
        repository.SetPendienteCarpeta(pendienteId, true);

        var model = new SectorModel(repository);
        model.OnGet(FolderSector.Archivo);

        Assert.Empty(model.Cases);
    }

    [Fact]
    public void OnGet_ExcludesCasesAlreadyGeneratedInAPreviousPdf()
    {
        var printedId = repository.Insert(NewRequest("msg-1", "Ya Impreso"));
        repository.SetFechaUltimaCarpeta(printedId, new DateOnly(2022, 1, 1));
        repository.SetMarked(printedId, true);
        repository.SetSectorPdfGenerated(printedId, DateTimeOffset.UtcNow);

        var freshId = repository.Insert(NewRequest("msg-2", "Caso Nuevo"));
        repository.SetFechaUltimaCarpeta(freshId, new DateOnly(2022, 1, 1));
        repository.SetMarked(freshId, true);

        var model = new SectorModel(repository);
        model.OnGet(FolderSector.Archivo);

        var result = Assert.Single(model.Cases);
        Assert.Equal("Caso Nuevo", result.FullName);
    }

    [Fact]
    public void OnGet_ExcludesCasesAlreadyTransferredToF8()
    {
        // Once a case is transferred to F8 it belongs to the F8-specific sector document
        // (SectorF8Model) instead — it must not also show up in the Cambio de Domicilio one.
        var transferredId = repository.Insert(NewRequest("msg-1", "Persona Traspasada"));
        repository.SetFechaUltimaCarpeta(transferredId, new DateOnly(2022, 1, 1));
        repository.SetMarked(transferredId, true);
        repository.SetDestination(transferredId, CaseDestination.F8, DateTimeOffset.UtcNow);

        var normalId = repository.Insert(NewRequest("msg-2", "Persona Normal"));
        repository.SetFechaUltimaCarpeta(normalId, new DateOnly(2022, 1, 1));
        repository.SetMarked(normalId, true);

        var model = new SectorModel(repository);
        model.OnGet(FolderSector.Archivo);

        var result = Assert.Single(model.Cases);
        Assert.Equal("Persona Normal", result.FullName);
    }

    [Fact]
    public void OnGet_ExcludesCasesAlreadyTransferredToCertificado()
    {
        // A case transferred to Certificado belongs to that dedicated screen instead — it must
        // not also show up in the normal Cambio de Domicilio PDF, same as F8-transferred cases.
        var transferredId = repository.Insert(NewRequest("msg-1", "Persona Traspasada Certificado"));
        repository.SetFechaUltimaCarpeta(transferredId, new DateOnly(2022, 1, 1));
        repository.SetMarked(transferredId, true);
        repository.SetDestination(transferredId, CaseDestination.Certificado, DateTimeOffset.UtcNow);

        var normalId = repository.Insert(NewRequest("msg-2", "Persona Normal"));
        repository.SetFechaUltimaCarpeta(normalId, new DateOnly(2022, 1, 1));
        repository.SetMarked(normalId, true);

        var model = new SectorModel(repository);
        model.OnGet(FolderSector.Archivo);

        var result = Assert.Single(model.Cases);
        Assert.Equal("Persona Normal", result.FullName);
    }

    [Fact]
    public void OnPostMarkPrinted_MarksEveryCurrentlyVisibleCaseInThatSector()
    {
        var archivoId = repository.Insert(NewRequest("msg-1", "Persona Archivo"));
        repository.SetFechaUltimaCarpeta(archivoId, new DateOnly(2022, 1, 1));
        repository.SetMarked(archivoId, true);
        var oficinaId = repository.Insert(NewRequest("msg-2", "Persona Oficina"));
        repository.SetFechaUltimaCarpeta(oficinaId, new DateOnly(2024, 1, 1));
        repository.SetMarked(oficinaId, true);

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
        repository.SetMarked(toRemoveId, true);
        var toKeepId = repository.Insert(NewRequest("msg-2", "Persona B"));
        repository.SetFechaUltimaCarpeta(toKeepId, new DateOnly(2022, 1, 1));
        repository.SetMarked(toKeepId, true);

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
        repository.SetMarked(archivoId1, true);
        var archivoId2 = repository.Insert(NewRequest("msg-2", "Persona B"));
        repository.SetFechaUltimaCarpeta(archivoId2, new DateOnly(2022, 1, 1));
        repository.SetMarked(archivoId2, true);
        var oficinaId = repository.Insert(NewRequest("msg-3", "Persona Oficina"));
        repository.SetFechaUltimaCarpeta(oficinaId, new DateOnly(2024, 1, 1));
        repository.SetMarked(oficinaId, true);

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
