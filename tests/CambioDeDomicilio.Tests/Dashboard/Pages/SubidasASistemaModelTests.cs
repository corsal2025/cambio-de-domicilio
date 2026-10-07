using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using CambioDeDomicilio.Configuration;
using CambioDeDomicilio.Dashboard.Pages;
using CambioDeDomicilio.Directories;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Mail;
using CambioDeDomicilio.Persistence;
using CambioDeDomicilio.Routing;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Pages;

public class SubidasASistemaModelTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"subidas-test-{Guid.NewGuid():N}.db");
    private readonly string csvPath = Path.Combine(Path.GetTempPath(), $"subidas-test-{Guid.NewGuid():N}.csv");
    private readonly IPersonRequestRepository repository;
    private readonly AddressChangeRoutingService routingService;
    private readonly SubidasASistemaModel model;

    public SubidasASistemaModelTests()
    {
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        TestDatabase.Migrate(dbPath);
        var discardedRepository = new DiscardedEmailRepository($"Data Source={dbPath}");
        TestDatabase.Migrate(dbPath);
        File.WriteAllText(csvPath, "Comuna,ContactEmail,Domain\nCatemu,rfloresc@municatemu.cl,municatemu.cl\n");

        var options = new RouterOptions
        {
            Ews = new EwsOptions { Url = "https://mail.munivalpo.cl/EWS/Exchange.asmx", Username = "u", Password = "p" },
            MailboxAddress = "cambiodedomicilio@munivalpo.cl",
            OwnDomain = "munivalpo.cl",
            SqliteDbPath = dbPath,
            ComunaDirectoryCsvPath = csvPath,
            ReportCsvPath = "unused-report.csv",
            NotificationEmailAddress = "raul.salazar1984@gmail.com"
        };

        routingService = new AddressChangeRoutingService(
            repository,
            discardedRepository,
            new MessageTombstoneRepository($"Data Source={dbPath}"),
            new ComunaDirectory(),
            new NoOpMailSender(),
            new NoOpEmailMover(),
            [],
            options,
            NullLogger<AddressChangeRoutingService>.Instance);

        model = new SubidasASistemaModel(repository, routingService)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public void OnGet_LoadsCasesWithSubidasDestination()
    {
        var id1 = InsertCase("JUAN PEREZ", "12.345.678-5", RequestStatus.Confirmed, CaseDestination.Subidas);
        var id2 = InsertCase("PEDRO GOMEZ", "9.876.543-2", RequestStatus.Pending, CaseDestination.None);

        model.OnGet();

        Assert.Single(model.Cases);
        Assert.Equal(id1, model.Cases[0].Id);
        Assert.Equal(1, model.TotalCount);
    }

    [Fact]
    public void OnGet_WithSearch_FiltersCorrectly()
    {
        InsertCase("JUAN PEREZ", "12.345.678-5", RequestStatus.Confirmed, CaseDestination.Subidas);
        InsertCase("MARIA SOTO", "9.876.543-2", RequestStatus.Confirmed, CaseDestination.Subidas);

        model.OnGet(search: "JUAN");

        Assert.Single(model.Cases);
        Assert.Equal("JUAN PEREZ", model.Cases[0].FullName);
    }

    [Fact]
    public void OnPostSendToCaja_MovesCaseToCajaDestination()
    {
        var id = InsertCase("JUAN PEREZ", "12.345.678-5", RequestStatus.Confirmed, CaseDestination.Subidas);

        var result = Assert.IsType<RedirectToPageResult>(model.OnPostSendToCaja(id));

        var stored = repository.FindById(id)!;
        Assert.Equal(CaseDestination.Caja, stored.Destination);
        Assert.Contains(repository.GetCajaQueue(), c => c.Id == id);
    }

    [Fact]
    public void OnPostCloseWithoutFolder_ClosesWithoutFolder()
    {
        var id = InsertCase("JUAN PEREZ", "12.345.678-5", RequestStatus.Confirmed, CaseDestination.Subidas);

        var result = Assert.IsType<RedirectToPageResult>(model.OnPostCloseWithoutFolder(id));
        Assert.Equal("/SinCarpetas", result.PageName);

        var stored = repository.FindById(id)!;
        Assert.NotNull(stored.ClosedWithoutFolderAt);

        model.OnGet();
        Assert.Empty(model.Cases);
    }

    [Fact]
    public async Task OnPostRectifyConfirmationAsync_RevertsCaseToPending()
    {
        var id = InsertCase("JUAN PEREZ", "12.345.678-5", RequestStatus.Confirmed, CaseDestination.Subidas);

        var result = Assert.IsType<RedirectToPageResult>(await model.OnPostRectifyConfirmationAsync(id));

        var stored = repository.FindById(id)!;
        Assert.Equal(RequestStatus.Pending, stored.Status);
        Assert.Equal(CaseDestination.None, stored.Destination);
        Assert.Null(stored.TransferredAt);
    }

    private long InsertCase(string name, string rut, RequestStatus status, CaseDestination destination)
    {
        var id = repository.Insert(new PersonRequest
        {
            FullName = name,
            Rut = rut,
            Comuna = "Catemu",
            SourceMessageId = $"msg-{Guid.NewGuid():N}",
            SourceSubject = "Solicitud de carpeta",
            SourceSender = "rfloresc@municatemu.cl",
            Status = status,
            FechaUltimaCarpeta = new DateOnly(2024, 3, 15)
        });

        if (destination != CaseDestination.None)
        {
            repository.SetDestination(id, destination, DateTimeOffset.UtcNow);
        }

        return id;
    }

    public void Dispose()
    {
        try { if (File.Exists(dbPath)) File.Delete(dbPath); } catch { }
        try { if (File.Exists(csvPath)) File.Delete(csvPath); } catch { }
    }

    private sealed class NoOpMailSender : IMailSender
    {
        public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class NoOpEmailMover : IEmailMover
    {
        public Task<bool> MoveAndMarkUnreadAsync(string messageId, string sourceFolderDisplayName, string destinationFolderDisplayName, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }
}
