namespace CambioDeDomicilio.Domain;

/// <summary>
/// Cheap structural (not RFC-5322) validation shared by every place that accepts a
/// user-entered email address, so the shape rules stay consistent across the app.
/// </summary>
public static class EmailShapeValidator
{
    public static bool IsValidEmailShape(string email)
    {
        var at = email.IndexOf('@');
        return at > 0 && at < email.Length - 3 && email.IndexOf('@', at + 1) < 0
            && email[(at + 1)..].Contains('.') && !email.Contains(',') && !email.Contains(' ');
    }
}
