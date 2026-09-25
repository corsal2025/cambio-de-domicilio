using System.Linq;
using CambioDeDomicilio.Dashboard.Pages;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Pages;

public class CajaModelTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"caja-page-test-{Guid.NewGuid():N}.db");
    private readonly IPersonRequestRepository repository;
    private readonly CajaModel model;

    public CajaModelTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        repository.EnsureSchema();
        model = new CajaModel(repository);
    }

    [Fact]
    public void OnPostCerrarCaja_SameCodeAsExistingBox_IsAllowedForSamePhysicalBox()
    {
        QueueCase("msg-1", "12.345.678-5");
        repository.CloseBox("A1-CD", DateTimeOffset.UtcNow);
        var queuedId = QueueCase("msg-2", "9.868.019-K");

        model.OnPostCerrarCaja(boxNumber: "1", boxCode: null);

        Assert.Equal(2, repository.GetBoxes().Count(b => b.Code == "A1-CD"));
        Assert.DoesNotContain(repository.GetCajaQueue(), c => c.Id == queuedId);
    }

    [Fact]
    public void OnPostCerrarCaja_NewCode_ClosesBox()
    {
        QueueCase("msg-1", "12.345.678-5");
        repository.CloseBox("A1-CD", DateTimeOffset.UtcNow);
        var queuedId = QueueCase("msg-2", "9.868.019-K");

        model.OnPostCerrarCaja(boxNumber: "2", boxCode: null);

        var newBox = repository.GetBoxes().Single(b => b.Code == "A2-CD");
        Assert.Equal(queuedId, Assert.Single(repository.GetCasesByBoxId(newBox.Id)).Id);
    }

    private long QueueCase(string sourceMessageId, string rut)
    {
        var id = repository.Insert(new PersonRequest
        {
            FullName = "GUSTAVO ANDRÉS PEÑA CASTRO",
            Rut = rut,
            Comuna = "Catemu",
            SourceMessageId = sourceMessageId,
            SourceSubject = "Solicitud de carpeta",
            SourceSender = "rfloresc@municatemu.cl",
            Status = RequestStatus.Pending
        });
        repository.SetDestination(id, CaseDestination.Caja, DateTimeOffset.UtcNow);
        return id;
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(dbPath)) File.Delete(dbPath);
    }
}
