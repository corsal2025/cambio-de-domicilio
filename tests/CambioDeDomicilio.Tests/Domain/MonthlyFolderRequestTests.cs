using CambioDeDomicilio.Domain;
using Xunit;

namespace CambioDeDomicilio.Tests.Domain;

public class MonthlyFolderRequestTests
{
    private static readonly DateTimeOffset UploadedInSeptember = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(2026, 10, 5, "2026-09")]
    [InlineData(2026, 1, 20, "2025-12")]
    [InlineData(2026, 3, 31, "2026-02")]
    public void PreviousMonthKey_IsTheMonthBeforeTheGivenDay(int year, int month, int day, string expected)
    {
        var now = new DateTimeOffset(year, month, day, 12, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, month, day)));

        Assert.Equal(expected, MonthlyFolderRequest.PreviousMonthKey(now));
    }

    [Fact]
    public void BelongsToMonth_UploadedF8CaseWithLastFolderDate_IsListedOnlyForItsMonth()
    {
        var c = F8Case();

        Assert.True(MonthlyFolderRequest.BelongsToMonth(c, "2026-09"));
        Assert.False(MonthlyFolderRequest.BelongsToMonth(c, "2026-10"));
    }

    [Fact]
    public void BelongsToMonth_UltimaCarpetaCase_IsAlsoListed()
    {
        var c = F8Case();
        c.Destination = CaseDestination.UltimaCarpeta;

        Assert.True(MonthlyFolderRequest.BelongsToMonth(c, "2026-09"));
    }

    [Fact]
    public void BelongsToMonth_NoUploadDate_FallsBackToConfirmedAt()
    {
        var c = F8Case();
        c.UploadedAt = null;
        c.ConfirmedAt = UploadedInSeptember;

        Assert.True(MonthlyFolderRequest.BelongsToMonth(c, "2026-09"));
    }

    [Fact]
    public void BelongsToMonth_WithoutLastFolderDate_IsNeverListed()
    {
        var c = F8Case();
        c.FechaUltimaCarpeta = null;

        Assert.False(MonthlyFolderRequest.BelongsToMonth(c, "2026-09"));
    }

    [Fact]
    public void BelongsToMonth_PendingCase_IsNeverListed()
    {
        var c = F8Case();
        c.Status = RequestStatus.Pending;

        Assert.False(MonthlyFolderRequest.BelongsToMonth(c, "2026-09"));
    }

    [Fact]
    public void BelongsToMonth_ClosedWithoutFolder_IsNeverListed()
    {
        var c = F8Case();
        c.ClosedWithoutFolderAt = DateTimeOffset.UtcNow;

        Assert.False(MonthlyFolderRequest.BelongsToMonth(c, "2026-09"));
    }

    [Theory]
    [InlineData(CaseDestination.None)]
    [InlineData(CaseDestination.Caja)]
    [InlineData(CaseDestination.Subidas)]
    public void BelongsToMonth_CaseOutsideF8_IsNotListed(CaseDestination destination)
    {
        var c = F8Case();
        c.Destination = destination;

        Assert.False(MonthlyFolderRequest.BelongsToMonth(c, "2026-09"));
    }

    [Theory]
    [InlineData("2026-09", true)]
    [InlineData("2026-9", false)]
    [InlineData("2026-13", false)]
    [InlineData("septiembre", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidMonthKey_AcceptsOnlyYearDashMonth(string? value, bool expected)
    {
        Assert.Equal(expected, MonthlyFolderRequest.IsValidMonthKey(value));
    }

    [Fact]
    public void ShouldRemind_FirstDaysWithUnmarkedCasesOfPreviousMonth_IsTrue()
    {
        var now = LocalDate(2026, 10, 3);

        Assert.True(MonthlyFolderRequest.ShouldRemind([F8Case()], now));
    }

    [Fact]
    public void ShouldRemind_AfterTheTenthDay_IsFalse()
    {
        var now = LocalDate(2026, 10, 11);

        Assert.False(MonthlyFolderRequest.ShouldRemind([F8Case()], now));
    }

    [Fact]
    public void ShouldRemind_AllCasesAlreadyMarked_IsFalse()
    {
        var c = F8Case();
        c.MonthlyRequestPrintedAt = DateTimeOffset.UtcNow;

        Assert.False(MonthlyFolderRequest.ShouldRemind([c], LocalDate(2026, 10, 3)));
    }

    [Fact]
    public void ShouldRemind_OnlyOlderMonthsPending_IsFalse()
    {
        var c = F8Case();
        c.UploadedAt = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

        Assert.False(MonthlyFolderRequest.ShouldRemind([c], LocalDate(2026, 10, 3)));
    }

    private static DateTimeOffset LocalDate(int year, int month, int day) =>
        new(new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Local));

    private static PersonRequest F8Case() => new()
    {
        FullName = "PERSONA DE PRUEBA",
        Rut = "11.111.111-1",
        Comuna = "Catemu",
        SourceMessageId = "m-" + Guid.NewGuid().ToString("N"),
        SourceSubject = "Cambio de domicilio",
        SourceSender = "comuna@example.cl",
        Destination = CaseDestination.F8,
        Status = RequestStatus.Confirmed,
        FechaUltimaCarpeta = new DateOnly(2019, 5, 17),
        UploadedAt = UploadedInSeptember,
    };
}
