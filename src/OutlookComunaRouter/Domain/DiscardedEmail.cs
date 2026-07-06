namespace OutlookComunaRouter.Domain;

/// <summary>
/// An email found in CARP. PARA PEDIR that was not turned into a <see cref="PersonRequest"/> because
/// its sender domain isn't in the comuna directory. Recorded so a missing comuna in the CSV is visible
/// to the operator instead of silently dropping the request.
/// </summary>
public sealed class DiscardedEmail
{
    public long Id { get; set; }
    public required string SourceMessageId { get; set; }
    public required string SourceSubject { get; set; }
    public required string SourceSender { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset DiscardedAt { get; set; } = DateTimeOffset.UtcNow;
}
