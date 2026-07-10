namespace OutlookComunaRouter.Dashboard.Auth;

public sealed class DashboardUser
{
    public long Id { get; set; }
    public required string Username { get; set; }
    public string? Email { get; set; }

    /// <summary>Personal signature/footer this operator wants appended to emails sent to other
    /// comunas (confirmations, rectifications). Empty by default, self-service via ChangePassword.</summary>
    public string? EmailFooter { get; set; }
    public required string PasswordHash { get; set; }
    public required string PasswordSalt { get; set; }
    public int Iterations { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
