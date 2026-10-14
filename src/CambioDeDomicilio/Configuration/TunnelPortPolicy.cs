namespace CambioDeDomicilio.Configuration;

/// <summary>
/// Decides which local listener is exempt from the HTTPS redirect. The exempt listener is the
/// loopback tunnel port (<c>Kestrel:Endpoints:TunnelHttp:Url</c>), so port-forwarding tools can
/// proxy plain HTTP to it. Its port comes from configuration, never from a hard-coded constant.
/// </summary>
public static class TunnelPortPolicy
{
    /// <summary>Production tunnel port, used when the configured URL is missing or malformed.</summary>
    public const int DefaultTunnelPort = 5002;

    public static int ResolveTunnelPort(string? tunnelHttpUrl)
    {
        if (!Uri.TryCreate(tunnelHttpUrl, UriKind.Absolute, out var tunnelUri))
        {
            return DefaultTunnelPort;
        }

        return tunnelUri.Port;
    }

    public static bool ShouldRedirectToHttps(int localPort, int tunnelPort) => localPort != tunnelPort;
}
