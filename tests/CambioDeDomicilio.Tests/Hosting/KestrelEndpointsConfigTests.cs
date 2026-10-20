using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CambioDeDomicilio.Tests.Hosting;

/// <summary>
/// The dashboard must listen on exactly one port: HTTPS 5001. Any plain-HTTP listener (the old
/// port 5000 redirector or the 5002 loopback tunnel endpoint) would reintroduce a second door.
/// </summary>
public class KestrelEndpointsConfigTests
{
    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Example.json")]
    public void AppSettings_DeclaresExactlyOneKestrelEndpoint_HttpsOnPort5001(string fileName)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(FindAppSettings(fileName), optional: false)
            .Build();

        var endpoints = configuration.GetSection("Kestrel:Endpoints").GetChildren().ToList();

        var endpoint = Assert.Single(endpoints);
        Assert.Equal("Https", endpoint.Key);
        Assert.Equal("https://0.0.0.0:5001", endpoint["Url"]);
    }

    private static string FindAppSettings(string fileName, [CallerFilePath] string thisFile = "")
    {
        // This file lives in tests/CambioDeDomicilio.Tests/Hosting, so the repository root is three levels up.
        // Anchoring on the source path keeps the test independent of where the build output goes.
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
        var path = Path.Combine(repositoryRoot, "src", "CambioDeDomicilio", fileName);
        return File.Exists(path) ? path : throw new FileNotFoundException($"Could not locate {path}.");
    }
}
