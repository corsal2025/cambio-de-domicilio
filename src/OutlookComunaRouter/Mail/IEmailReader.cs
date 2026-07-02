using OutlookComunaRouter.Domain;

namespace OutlookComunaRouter.Mail;

public interface IEmailReader
{
    /// <summary>
    /// Lists items currently in the configured source folder. No time filtering: the trigger
    /// is the operator moving an item into the folder, not when it was originally received.
    /// </summary>
    Task<IReadOnlyList<IncomingEmail>> GetRecentMessagesAsync(CancellationToken cancellationToken);
}
