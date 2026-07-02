using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using OutlookComunaRouter.Configuration;

namespace OutlookComunaRouter.Ews;

public interface IEwsClient
{
    Task<XDocument> SendAsync(string soapRequest, CancellationToken cancellationToken);
}

/// <summary>
/// Thin SOAP transport for the on-premises Exchange EWS endpoint. Uses Basic
/// authentication over TLS (deterministic cross-platform, unlike NTLM which needs
/// native GSSAPI libraries on Linux) and retries transient failures (5xx/408/429,
/// network errors) with exponential backoff within the polling cycle.
/// </summary>
public sealed class EwsClient : IEwsClient, IDisposable
{
    private static readonly int[] TransientStatusCodes = [408, 429, 500, 502, 503, 504];

    private readonly HttpClient httpClient;
    private readonly RouterOptions options;
    private readonly ILogger<EwsClient> logger;

    public EwsClient(RouterOptions options, ILogger<EwsClient> logger)
    {
        this.options = options;
        this.logger = logger;

        httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(100) };
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{options.Ews.Username}:{options.Ews.Password}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    public async Task<XDocument> SendAsync(string soapRequest, CancellationToken cancellationToken)
    {
        var attempt = 0;
        var delay = TimeSpan.FromSeconds(2);
        const int maxAttempts = 4;

        while (true)
        {
            attempt++;
            try
            {
                using var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
                using var response = await httpClient.PostAsync(options.Ews.Url, content, cancellationToken);

                if (TransientStatusCodes.Contains((int)response.StatusCode) && attempt < maxAttempts)
                {
                    logger.LogWarning(
                        "Transient EWS failure (attempt {Attempt}/{MaxAttempts}, HTTP {Status}), retrying in {Delay}s",
                        attempt, maxAttempts, (int)response.StatusCode, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                    delay *= 2;
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var xml = await response.Content.ReadAsStringAsync(cancellationToken);
                return XDocument.Parse(xml);
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex,
                    "EWS network error (attempt {Attempt}/{MaxAttempts}), retrying in {Delay}s",
                    attempt, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
                delay *= 2;
            }
        }
    }

    public void Dispose() => httpClient.Dispose();
}
