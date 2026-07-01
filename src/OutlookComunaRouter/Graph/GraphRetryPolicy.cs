using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models.ODataErrors;

namespace OutlookComunaRouter.Graph;

/// <summary>
/// Retries transient Microsoft Graph failures (throttling / 429, transient 5xx, and token
/// expiry manifesting as 401) with exponential backoff, within a single polling cycle.
/// Non-transient errors (4xx other than 401/429) are not retried and propagate immediately.
/// </summary>
public static class GraphRetryPolicy
{
    private static readonly int[] TransientStatusCodes = [401, 429, 500, 502, 503, 504];

    public static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        ILogger logger,
        CancellationToken cancellationToken,
        int maxAttempts = 4)
    {
        var attempt = 0;
        var delay = TimeSpan.FromSeconds(2);

        while (true)
        {
            attempt++;
            try
            {
                return await operation();
            }
            catch (ODataError ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                var wait = RetryAfterOrDefault(ex, delay);
                logger.LogWarning(
                    "Fallo transitorio de Graph (intento {Attempt}/{MaxAttempts}, HTTP {Status}), reintentando en {Wait}s",
                    attempt, maxAttempts, ex.ResponseStatusCode, wait.TotalSeconds);
                await Task.Delay(wait, cancellationToken);
                delay = delay * 2;
            }
        }
    }

    public static Task ExecuteAsync(
        Func<Task> operation,
        ILogger logger,
        CancellationToken cancellationToken,
        int maxAttempts = 4) =>
        ExecuteAsync(async () => { await operation(); return true; }, logger, cancellationToken, maxAttempts);

    private static bool IsTransient(ODataError ex) =>
        TransientStatusCodes.Contains(ex.ResponseStatusCode);

    private static TimeSpan RetryAfterOrDefault(ODataError ex, TimeSpan fallback)
    {
        var retryAfterHeader = ex.ResponseHeaders?.TryGetValue("Retry-After", out var values) == true
            ? values.FirstOrDefault()
            : null;

        return retryAfterHeader is not null && int.TryParse(retryAfterHeader, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : fallback;
    }
}
