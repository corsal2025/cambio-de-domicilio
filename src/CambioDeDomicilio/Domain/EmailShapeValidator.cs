namespace CambioDeDomicilio.Domain;

/// <summary>
/// Cheap structural (not RFC-5322) validation shared by every place that accepts a
/// user-entered email address, so the shape rules stay consistent across the app.
/// </summary>
public static class EmailShapeValidator
{
    private static readonly char[] AddressSeparators = [',', ';'];

    public static bool IsValidEmailShape(string email)
    {
        var at = email.IndexOf('@');
        return at > 0 && at < email.Length - 3 && email.IndexOf('@', at + 1) < 0
            && email[(at + 1)..].Contains('.') && !email.Contains(',') && !email.Contains(' ');
    }

    public static IReadOnlyList<string> SplitAddresses(string? addresses) =>
        string.IsNullOrWhiteSpace(addresses)
            ? []
            : addresses.Split(AddressSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool AreAllValidEmailShapes(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var parts = raw.Split(AddressSeparators, StringSplitOptions.TrimEntries);
        return parts.Length > 0 && parts.All(IsValidEmailShape);
    }
}
