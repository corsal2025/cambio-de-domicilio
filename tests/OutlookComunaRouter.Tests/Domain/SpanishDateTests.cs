using OutlookComunaRouter.Domain;
using Xunit;

namespace OutlookComunaRouter.Tests.Domain;

public class SpanishDateTests
{
    [Theory]
    [InlineData("15 marzo 2024", 2024, 3, 15)]
    [InlineData("15 de marzo de 2024", 2024, 3, 15)]
    [InlineData("15 de marzo del 2024", 2024, 3, 15)]
    [InlineData("1 ENERO 2023", 2023, 1, 1)]
    [InlineData("30 Septiembre 2022", 2022, 9, 30)]
    [InlineData("30 setiembre 2022", 2022, 9, 30)]
    public void TryParse_ValidSpanishDates_Parses(string input, int year, int month, int day)
    {
        Assert.True(SpanishDate.TryParse(input, out var date));
        Assert.Equal(new DateOnly(year, month, day), date);
    }

    [Theory]
    [InlineData("15 marzzo 2024")]  // typo in month
    [InlineData("32 enero 2024")]   // day out of range
    [InlineData("30 febrero 2024")] // invalid for month
    [InlineData("15 marzo")]        // missing year
    [InlineData("2024-03-15")]      // ISO, not the agreed format
    [InlineData("")]
    public void TryParse_InvalidInput_ReturnsFalse(string input)
    {
        Assert.False(SpanishDate.TryParse(input, out _));
    }

    [Fact]
    public void Format_RendersDayMonthNameFullYear()
    {
        Assert.Equal("15 marzo 2024", SpanishDate.Format(new DateOnly(2024, 3, 15)));
        Assert.Equal("1 enero 2023", SpanishDate.Format(new DateOnly(2023, 1, 1)));
    }
}
