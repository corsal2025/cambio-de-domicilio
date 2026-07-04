using OutlookComunaRouter.Domain;
using Xunit;

namespace OutlookComunaRouter.Tests.Domain;

public class DeadlineCalculatorTests
{
    [Fact]
    public void AddBusinessDays_SkipsWeekends()
    {
        // Friday 2026-07-03 + 1 business day = Monday 2026-07-06
        var result = DeadlineCalculator.AddBusinessDays(new DateOnly(2026, 7, 3), 1);

        Assert.Equal(new DateOnly(2026, 7, 6), result);
    }

    [Fact]
    public void AddBusinessDays_FifteenDays_LandsThreeWeeksLater()
    {
        // Wednesday 2026-07-01 + 15 business days = Wednesday 2026-07-22 (3 full weeks)
        var result = DeadlineCalculator.AddBusinessDays(new DateOnly(2026, 7, 1), 15);

        Assert.Equal(new DateOnly(2026, 7, 22), result);
    }

    [Fact]
    public void BusinessDaysRemaining_SameDay_IsZero()
    {
        var day = new DateOnly(2026, 7, 3);

        Assert.Equal(0, DeadlineCalculator.BusinessDaysRemaining(day, day));
    }

    [Fact]
    public void BusinessDaysRemaining_AcrossWeekend_CountsOnlyWeekdays()
    {
        // Friday to next Monday = 1 business day
        var result = DeadlineCalculator.BusinessDaysRemaining(new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 6));

        Assert.Equal(1, result);
    }

    [Fact]
    public void BusinessDaysRemaining_PastDeadline_IsNegative()
    {
        // Deadline was Monday, today is Thursday → -3 business days (overdue)
        var result = DeadlineCalculator.BusinessDaysRemaining(new DateOnly(2026, 7, 9), new DateOnly(2026, 7, 6));

        Assert.Equal(-3, result);
    }
}
