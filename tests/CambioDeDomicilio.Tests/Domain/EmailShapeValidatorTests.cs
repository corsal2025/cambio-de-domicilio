using CambioDeDomicilio.Domain;
using Xunit;

namespace CambioDeDomicilio.Tests.Domain;

public class EmailShapeValidatorTests
{
    [Theory]
    [InlineData("user@example.com", true)]
    [InlineData("user.name@sub.example.com", true)]
    [InlineData("", false)]
    [InlineData("@example.com", false)]
    [InlineData("user@", false)]
    [InlineData("userexample.com", false)]
    [InlineData("user@@example.com", false)]
    [InlineData("user@example,com", false)]
    [InlineData("user @example.com", false)]
    [InlineData("user@examplecom", false)]
    public void IsValidEmailShape_ReturnsExpectedResult(string email, bool expected)
    {
        Assert.Equal(expected, EmailShapeValidator.IsValidEmailShape(email));
    }
}
