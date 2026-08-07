namespace CambioDeDomicilio.Dashboard.Auth;

public enum LoginOutcome
{
    Success,
    InvalidCredentials,
    LockedOut
}

public interface ILoginService
{
    Task<LoginOutcome> TryLoginAsync(string username, string password);
}

/// <summary>
/// Fixed small delay on every failed attempt plus a lockout after repeated failures — cheap
/// brute-force friction appropriate for a small internal user base, not a full rate-limiter.
/// </summary>
public sealed class LoginService(IUserRepository users) : ILoginService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FailedAttemptDelay = TimeSpan.FromSeconds(1);

    public async Task<LoginOutcome> TryLoginAsync(string username, string password)
    {
        var user = users.FindByUsername(username);
        if (user is null)
        {
            await Task.Delay(FailedAttemptDelay); // don't let username existence be timing-distinguishable
            return LoginOutcome.InvalidCredentials;
        }

        if (user.LockedUntil is { } lockedUntil && lockedUntil > DateTimeOffset.UtcNow)
        {
            return LoginOutcome.LockedOut;
        }

        if (!PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt, user.Iterations))
        {
            await Task.Delay(FailedAttemptDelay);
            var attempts = user.FailedLoginAttempts + 1;
            var lockUntil = attempts >= MaxFailedAttempts ? DateTimeOffset.UtcNow.Add(LockoutDuration) : (DateTimeOffset?)null;
            users.RecordFailedLogin(user.Id, attempts, lockUntil);
            return lockUntil is not null ? LoginOutcome.LockedOut : LoginOutcome.InvalidCredentials;
        }

        users.ResetFailedLogins(user.Id);
        return LoginOutcome.Success;
    }
}
