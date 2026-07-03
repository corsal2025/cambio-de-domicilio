using OutlookComunaRouter.Domain;

namespace OutlookComunaRouter.Mail;

public interface IEmailReader
{
    /// <summary>
    /// Lists items currently in the named folder. No time filtering: the trigger is folder
    /// membership (the operator moving an item in), not when it was originally received.
    /// </summary>
    Task<IReadOnlyList<IncomingEmail>> GetMessagesInFolderAsync(string folderDisplayName, CancellationToken cancellationToken);
}
