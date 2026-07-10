namespace OutlookComunaRouter.Dashboard.Auth;

/// <summary>A single-use, time-limited token emailed to a user who requested a password reset.</summary>
public sealed class PasswordResetToken
{
    public long Id { get; set; }
    public required long UserId { get; set; }
    public required string Token { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
