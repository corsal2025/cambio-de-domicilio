using Microsoft.AspNetCore.Mvc;
using CambioDeDomicilio.Dashboard.Pages;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Persistence;
using Xunit;

namespace CambioDeDomicilio.Tests.Dashboard.Pages;

public class PedidoMensualModelTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"pedido-mensual-test-{Guid.NewGuid():N}.db");
    private readonly IPersonRequestRepository repository;
    private readonly PedidoMensualModel model;

    public PedidoMensualModelTests()
    {
        TestDatabase.Migrate(dbPath);
        repository = new PersonRequestRepository($"Data Source={dbPath}");
        model = new PedidoMensualModel(repository, new FixedClock(new DateTimeOffset(new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Local))));
    }

    [Fact]
    public void OnGet_WithoutMonth_ShowsThePreviousMonth()
    {
        model.OnGet(month: null);

        Assert.Equal("2026-09", model.Month);
    }

    [Fact]
    public void OnGet_InvalidMonth_FallsBackToThePreviousMonth()
    {
        model.OnGet(month: "no-es-un-mes");

        Assert.Equal("2026-09", model.Month);
    }

    [Fact]
    public void OnGet_ListsOnlyTheCasesOfThatMonthOrderedByUploadDate()
    {
        var later = Seed("m-later", "SEGUNDO", new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        var earlier = Seed("m-earlier", "PRIMERO", new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero));
        Seed("m-august", "AGOSTO", new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero));

        model.OnGet(month: "2026-09");

        Assert.Equal(new[] { earlier, later }, model.Cases.Select(c => c.Id).ToArray());
        Assert.StartsWith("F8-Mensual-202609-", model.Folio);
    }

    [Fact]
    public void OnPostMarkPrinted_StampsTheListedCasesAndKeepsThemInTheListing()
    {
        var id = Seed("m1", "PRIMERO", new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero));
        var otherMonth = Seed("m2", "AGOSTO", new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));

        var result = Assert.IsType<RedirectToPageResult>(model.OnPostMarkPrinted("2026-09"));

        Assert.Equal("2026-09", result.RouteValues!["month"]);
        Assert.NotNull(repository.FindById(id)!.MonthlyRequestPrintedAt);
        Assert.Null(repository.FindById(otherMonth)!.MonthlyRequestPrintedAt);

        model.OnGet(month: "2026-09");
        Assert.Single(model.Cases);
        Assert.Equal(1, model.PrintedCount);
    }

    [Fact]
    public void Page_ExposesNoHandlerThatSendsEmail()
    {
        var handlers = typeof(PedidoMensualModel).GetMethods()
            .Where(m => m.Name.StartsWith("OnPost", StringComparison.Ordinal))
            .Select(m => m.Name)
            .ToArray();

        Assert.Equal(new[] { "OnPostMarkPrinted" }, handlers);
    }

    private long Seed(string messageId, string name, DateTimeOffset uploadedAt)
    {
        var id = repository.Insert(new PersonRequest
        {
            FullName = name,
            Rut = "11.111.111-1",
            Comuna = "Catemu",
            SourceMessageId = messageId,
            SourceSubject = "Cambio de domicilio",
            SourceSender = "comuna@example.cl",
        });
        repository.SetFechaUltimaCarpeta(id, new DateOnly(2019, 5, 17));
        repository.MarkUploaded(id, uploadedAt);
        repository.UpdateStatusToConfirmed(id, uploadedAt);
        repository.SetDestination(id, CaseDestination.F8, uploadedAt);
        return id;
    }

    public void Dispose() => TestDatabase.Cleanup(dbPath);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }
}
