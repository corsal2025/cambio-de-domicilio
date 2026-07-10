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

public interface IEmailMover
{
    /// <summary>
    /// Finds the item by its Internet Message-Id within <paramref name="sourceFolderDisplayName"/>,
    /// moves it to <paramref name="destinationFolderDisplayName"/>, and marks it unread there.
    /// Returns false if no matching item was found (e.g. already moved, or deleted) or either
    /// folder couldn't be resolved — callers should still proceed with their own state change and
    /// just log the mismatch, since the case tracking must not be blocked by a mailbox hiccup.
    /// </summary>
    Task<bool> MoveAndMarkUnreadAsync(string messageId, string sourceFolderDisplayName, string destinationFolderDisplayName, CancellationToken cancellationToken);
}
