using System.Text.RegularExpressions;

namespace CambioDeDomicilio.Domain;

/// <summary>
/// Recognizes a non-delivery report (bounce) for one of our "carpeta subida a Conaset"
/// confirmation emails, sitting in the mailbox inbox. Deliberately conservative: it requires
/// both an NDR-shaped sender or subject AND a mention of Conaset in the message, so a bounce for
/// some unrelated email — or a normal reply from a comuna — is never mistaken for one of ours.
/// </summary>
public static partial class BounceDetector
{
    private static readonly string[] NdrSenderLocalParts = ["postmaster", "mailer-daemon", "mail delivery subsystem"];

    [GeneratedRegex(
        @"^\s*(undeliverable|undelivered mail|delivery status notification|mail delivery (failed|subsystem)|returned mail|"
        + @"correo no entregado|no se puede entregar|mensaje no entregado|no entregado|devoluci[oó]n de correo|fallo en la entrega)",
        RegexOptions.IgnoreCase)]
    private static partial Regex NdrSubjectPattern();

    // Exchange also sends a "still trying" notice ("Se retrasó la entrega" / "Delivery is
    // delayed") while a message is merely queued or being retried — not a failure, and often sent
    // from a postmaster-looking address too. Treating this as a bounce would be wrong: the message
    // can still arrive, and flagging the case would send the operator chasing a non-problem.
    [GeneratedRegex(
        @"(se retras[oó]|retraso en la entrega|a[uú]n no se entreg[oó]|delivery is delayed|delayed|still trying|will keep trying to deliver)",
        RegexOptions.IgnoreCase)]
    private static partial Regex DelayNotificationPattern();

    public static bool LooksLikeConfirmationBounce(IncomingEmail email)
    {
        if (!MentionsConaset(email))
        {
            return false;
        }

        if (IsDelayNotification(email))
        {
            return false;
        }

        return HasNdrSender(email.SenderAddress) || NdrSubjectPattern().IsMatch(email.Subject ?? string.Empty);
    }

    private static bool IsDelayNotification(IncomingEmail email) =>
        DelayNotificationPattern().IsMatch(email.Subject ?? string.Empty)
        || DelayNotificationPattern().IsMatch(email.BodyText ?? string.Empty);

    private static bool MentionsConaset(IncomingEmail email) =>
        (email.Subject?.Contains("conaset", StringComparison.OrdinalIgnoreCase) ?? false)
        || (email.BodyText?.Contains("conaset", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool HasNdrSender(string? sender)
    {
        var trimmed = (sender ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed == "<>")
        {
            return true; // empty envelope-from is the classic bounce signature
        }

        var at = trimmed.IndexOf('@');
        var localPart = (at > 0 ? trimmed[..at] : trimmed).Trim('<', '>', '"', ' ');
        return Array.Exists(NdrSenderLocalParts, p => string.Equals(p, localPart, StringComparison.OrdinalIgnoreCase));
    }
}
