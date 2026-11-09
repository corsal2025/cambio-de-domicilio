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
        TestDatabase.Migrate(dbPath);
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
    public void FoundFolder_ReturnsTheSameCaseToCasosAndPreservesItsData()
    {
        var id = repository.Insert(NewRequest("found-folder"));
        repository.SetCodigoF8(id, "F8-123");
        repository.SetFechaUltimaCarpeta(id, new DateOnly(2020, 6, 15));
        repository.CloseWithoutFolder(id, DateTimeOffset.UtcNow);

        model.OnPostRestoreFolder(id);

        var restored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.None, restored.Destination);
        Assert.False(restored.SinCarpeta);
        Assert.Null(restored.ClosedWithoutFolderAt);
        Assert.Null(restored.TransferredAt);
        Assert.False(restored.FolderNotFound);
        Assert.Equal("PERSONA DE PRUEBA", restored.FullName);
        Assert.Equal("18.785.387-7", restored.Rut);
        Assert.Equal("F8-123", restored.CodigoF8);
        Assert.Equal(new DateOnly(2020, 6, 15), restored.FechaUltimaCarpeta);
        Assert.Equal(RequestStatus.Pending, restored.Status);
        Assert.False(restored.SoloCaja);
        Assert.Single(repository.GetAll());
        model.OnGet();
        Assert.Empty(model.Cases);
    }

    [Fact]
    public void FoundFolder_PreservesConfirmationAndAllowsCajaWithoutResendingMail()
    {
        var id = repository.Insert(NewRequest("confirmed-folder"));
        var confirmedAt = DateTimeOffset.UtcNow;
        repository.MarkUploaded(id, confirmedAt);
        repository.UpdateStatusToConfirmed(id, confirmedAt);
        repository.CloseWithoutFolder(id, confirmedAt);

        model.OnPostRestoreFolder(id);

        var restored = repository.FindById(id)!;
        Assert.Equal(RequestStatus.Confirmed, restored.Status);
        Assert.Equal(confirmedAt, restored.ConfirmedAt);
        Assert.Equal(confirmedAt, restored.UploadedAt);
        Assert.True(restored.SoloCaja);
        repository.SendToCaja([id], DateTimeOffset.UtcNow);
        Assert.Equal(id, Assert.Single(repository.GetCajaQueue()).Id);
    }

    [Fact]
    public void FoundFolder_RejectsUnrelatedMissingAndAlreadyRestoredCases()
    {
        var id = repository.Insert(NewRequest("untouched"));
        Assert.False(repository.RestoreFoundFolder(id));
        Assert.False(repository.RestoreFoundFolder(long.MaxValue));
        Assert.False(repository.FindById(id)!.SoloCaja);
        repository.CloseWithoutFolder(id, DateTimeOffset.UtcNow);
        Assert.True(repository.RestoreFoundFolder(id));
        Assert.False(repository.RestoreFoundFolder(id));
    }

    [Fact]
    public void FoundFolder_DoesNotChangeClosedBoxMembership()
    {
        var id = repository.Insert(NewRequest("boxed"));
        repository.MarkUploaded(id, DateTimeOffset.UtcNow);
        repository.SendToCaja([id], DateTimeOffset.UtcNow);
        var boxes = new BoxRepository($"Data Source={dbPath}");
        var box = boxes.CloseBox("TEST-BOX", DateTimeOffset.UtcNow);
        repository.CloseWithoutFolder(id, DateTimeOffset.UtcNow);

        Assert.False(repository.RestoreFoundFolder(id));
        Assert.Equal(box.Id, repository.FindById(id)!.BoxId);
    }

    public void Dispose()
    {
        TestDatabase.Cleanup(dbPath);
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
