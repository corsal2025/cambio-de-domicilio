using CambioDeDomicilio.Configuration;
using Xunit;

namespace CambioDeDomicilio.Tests.Hosting;

public class TunnelPortPolicyTests
{
    [Theory]
    [InlineData("http://127.0.0.1:5102", 5102)]
    [InlineData("http://127.0.0.1:5002", 5002)]
    public void ResolveTunnelPort_ConfiguredUrl_ReturnsItsPort(string configuredUrl, int expectedPort)
    {
        Assert.Equal(expectedPort, TunnelPortPolicy.ResolveTunnelPort(configuredUrl));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    public void ResolveTunnelPort_MissingOrMalformedUrl_FallsBackToProductionDefault(string? configuredUrl)
    {
        Assert.Equal(5002, TunnelPortPolicy.ResolveTunnelPort(configuredUrl));
    }

    [Fact]
    public void ShouldRedirectToHttps_RequestOnTunnelPort_IsFalse()
    {
        Assert.False(TunnelPortPolicy.ShouldRedirectToHttps(localPort: 5102, tunnelPort: 5102));
    }

    [Theory]
    [InlineData(5000)]
    [InlineData(5101)]
    public void ShouldRedirectToHttps_RequestOnPublicOrHttpsPort_IsTrue(int localPort)
    {
        Assert.True(TunnelPortPolicy.ShouldRedirectToHttps(localPort, tunnelPort: 5102));
    }

    [Fact]
    public void ShouldRedirectToHttps_RequestOnOldHardCodedPortWhenTunnelIsElsewhere_IsTrue()
    {
        Assert.True(TunnelPortPolicy.ShouldRedirectToHttps(localPort: 5002, tunnelPort: 5102));
    }
}
