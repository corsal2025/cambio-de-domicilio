namespace CambioDeDomicilio.Configuration;

public sealed record RouterOptionsValidationResult(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);

/// <summary>Fails fast at startup on configuration that can only produce confusing errors later
/// (e.g. a malformed EWS URL surfacing as an opaque HTTP exception on the first sync). Missing
/// credentials are a warning, not an error: Development runs without a mailbox on purpose.</summary>
public static class RouterOptionsValidator
{
    public static RouterOptionsValidationResult Validate(RouterOptions options)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!Uri.TryCreate(options.Ews.Url, UriKind.Absolute, out var ewsUri)
            || (ewsUri.Scheme != Uri.UriSchemeHttps && ewsUri.Scheme != Uri.UriSchemeHttp))
        {
            errors.Add("Router:Ews:Url must be an absolute http(s) URL.");
        }

        if (string.IsNullOrWhiteSpace(options.Ews.Username))
        {
            warnings.Add("Router:Ews:Username is empty: synchronization with the mailbox will fail until it is configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Ews.Password))
        {
            warnings.Add("Router:Ews:Password is empty: synchronization with the mailbox will fail until it is configured.");
        }

        if (string.IsNullOrWhiteSpace(options.SourceFolderName))
        {
            errors.Add("Router:SourceFolderName must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.ConfirmationFolderName))
        {
            errors.Add("Router:ConfirmationFolderName must not be empty.");
        }

        if (string.Equals(options.SourceFolderName, options.ConfirmationFolderName, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Router:ConfirmationFolderName must differ from Router:SourceFolderName, otherwise every request would look already uploaded.");
        }

        if (options.PlazoDiasHabiles <= 0)
        {
            errors.Add("Router:PlazoDiasHabiles must be greater than zero.");
        }

        if (options.BounceLookbackDays <= 0)
        {
            errors.Add("Router:BounceLookbackDays must be greater than zero.");
        }

        AddIfBlank(errors, options.SqliteDbPath, "Router:SqliteDbPath");
        AddIfBlank(errors, options.ComunaDirectoryCsvPath, "Router:ComunaDirectoryCsvPath");
        AddIfBlank(errors, options.ReportCsvPath, "Router:ReportCsvPath");

        return new RouterOptionsValidationResult(errors, warnings);
    }

    private static void AddIfBlank(List<string> errors, string value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{key} must not be empty.");
        }
    }
}
