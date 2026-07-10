using System.Security.Cryptography;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Mail;
using OutlookComunaRouter.Notifications;

namespace OutlookComunaRouter.Dashboard.Auth;

public enum PasswordResetRequestOutcome
{
    Requested,
    UserNotFound,
    NoEmailOnFile
}

public enum PasswordResetCompletionOutcome
{
    Success,
    InvalidOrExpiredToken,
    TokenAlreadyUsed,
    PasswordTooShort
}

public interface IPasswordResetService
{
    Task<PasswordResetRequestOutcome> RequestResetAsync(string username, CancellationToken cancellationToken);
    PasswordResetCompletionOutcome CompleteReset(string token, string newPassword);
}

/// <summary>
/// "Olvidé mi contraseña" flow: emails a single-use, time-limited link to the user's registered
/// recovery email (set via the Cambiar contraseña page). Never reveals whether a username exists
/// or has an email on file — the ForgotPassword page shows the same message regardless of outcome.
/// </summary>
public sealed class PasswordResetService(
    IUserRepository users,
    IPasswordResetTokenRepository tokens,
    IMailSender mailSender,
    RouterOptions options) : IPasswordResetService
{
    private const int MinPasswordLength = 8;
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    public async Task<PasswordResetRequestOutcome> RequestResetAsync(string username, CancellationToken cancellationToken)
    {
        var user = users.FindByUsername(username);
        if (user is null)
        {
            return PasswordResetRequestOutcome.UserNotFound;
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return PasswordResetRequestOutcome.NoEmailOnFile;
        }

        // Only the most recent request should ever be usable.
        tokens.InvalidateAllForUser(user.Id);

        var token = RandomNumberGenerator.GetHexString(40);
        tokens.Insert(new PasswordResetToken
        {
            UserId = user.Id,
            Token = token,
            ExpiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime)
        });

        var resetLink = $"{options.PublicBaseUrl.TrimEnd('/')}/ResetPassword?token={token}";
        var (subject, body) = EmailTemplates.PasswordReset(user.Username, resetLink);
        await mailSender.SendAsync(user.Email, subject, body, cancellationToken);

        return PasswordResetRequestOutcome.Requested;
    }

    public PasswordResetCompletionOutcome CompleteReset(string token, string newPassword)
    {
        if (newPassword.Length < MinPasswordLength)
        {
            return PasswordResetCompletionOutcome.PasswordTooShort;
        }

        var resetToken = tokens.FindByToken(token);
        if (resetToken is null || resetToken.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return PasswordResetCompletionOutcome.InvalidOrExpiredToken;
        }

        if (resetToken.UsedAt is not null)
        {
            return PasswordResetCompletionOutcome.TokenAlreadyUsed;
        }

        var (hash, salt, iterations) = PasswordHasher.Hash(newPassword);
        users.UpdatePassword(resetToken.UserId, hash, salt, iterations);
        tokens.MarkUsed(token);

        return PasswordResetCompletionOutcome.Success;
    }
}
