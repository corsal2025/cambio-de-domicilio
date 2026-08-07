using CambioDeDomicilio.Extraction;
using Xunit;

namespace CambioDeDomicilio.Tests.Extraction;

public class RutValidatorTests
{
    [Theory]
    [InlineData("18.785.387-7", "18.785.387-7")]
    [InlineData("18785387-7", "18.785.387-7")]
    [InlineData("10000013-K", "10.000.013-K")]
    [InlineData("10000013-k", "10.000.013-K")]
    // 7-digit body (RUT under 10 million) gets left-padded with a 0 to the standard 8-digit
    // grouping, both dotted and dash-only input.
    [InlineData("9876543-3", "09.876.543-3")]
    [InlineData("9.876.543-3", "09.876.543-3")]
    public void NormalizeAndValidate_ValidRut_ReturnsCanonicalForm(string input, string expected)
    {
        var result = RutValidator.NormalizeAndValidate(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("18.785.387-6")] // wrong check digit
    [InlineData("no-es-un-rut")]
    [InlineData("123-4")] // too short
    public void NormalizeAndValidate_InvalidRut_ReturnsNull(string input)
    {
        var result = RutValidator.NormalizeAndValidate(input);

        Assert.Null(result);
    }
}
