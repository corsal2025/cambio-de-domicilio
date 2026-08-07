using System.Xml.Linq;

namespace CambioDeDomicilio.Ews;

/// <summary>
/// Builds EWS SOAP request envelopes. XML is constructed with XElement so all
/// user-supplied values (subjects, bodies, addresses) are escaped structurally —
/// no string interpolation into markup.
/// </summary>
public static class EwsMessages
{
    public static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    public static readonly XNamespace T = "http://schemas.microsoft.com/exchange/services/2006/types";
    public static readonly XNamespace M = "http://schemas.microsoft.com/exchange/services/2006/messages";

    /// <summary>
    /// Lists items in <paramref name="folder"/> with no time-window restriction: the trigger
    /// for this router is "the operator moved this item into the folder," and DateTimeReceived
    /// does not change on move, so time-filtering it would silently drop manually-triaged mail.
    /// Idempotency (see AddressChangeRoutingService) is what prevents reprocessing, not this filter.
    /// </summary>
    public static string BuildFindItemRequest(EwsFolderRef folder, int maxEntries = 200) =>
        Envelope(
            new XElement(M + "FindItem",
                new XAttribute("Traversal", "Shallow"),
                new XElement(M + "ItemShape",
                    new XElement(T + "BaseShape", "IdOnly")),
                new XElement(M + "IndexedPageItemView",
                    new XAttribute("MaxEntriesReturned", maxEntries),
                    new XAttribute("Offset", 0),
                    new XAttribute("BasePoint", "Beginning")),
                new XElement(M + "SortOrder",
                    new XElement(T + "FieldOrder",
                        new XAttribute("Order", "Ascending"),
                        new XElement(T + "FieldURI", new XAttribute("FieldURI", "item:DateTimeReceived")))),
                new XElement(M + "ParentFolderIds", ParentFolderElement(folder))));

    /// <summary>Resolves a folder by display name (custom folders are not exposed as distinguished IDs).</summary>
    public static string BuildFindFolderRequest(string displayName) =>
        Envelope(
            new XElement(M + "FindFolder",
                new XAttribute("Traversal", "Deep"),
                new XElement(M + "FolderShape",
                    new XElement(T + "BaseShape", "IdOnly")),
                new XElement(M + "Restriction",
                    new XElement(T + "IsEqualTo",
                        new XElement(T + "FieldURI", new XAttribute("FieldURI", "folder:DisplayName")),
                        new XElement(T + "FieldURIOrConstant",
                            new XElement(T + "Constant", new XAttribute("Value", displayName))))),
                new XElement(M + "ParentFolderIds",
                    new XElement(T + "DistinguishedFolderId", new XAttribute("Id", "msgfolderroot")))));

    private static XElement ParentFolderElement(EwsFolderRef folder) =>
        folder.DistinguishedId is not null
            ? new XElement(T + "DistinguishedFolderId", new XAttribute("Id", folder.DistinguishedId))
            : new XElement(T + "FolderId",
                new XAttribute("Id", folder.Id!),
                new XAttribute("ChangeKey", folder.ChangeKey!));

    public static string BuildGetItemRequest(IEnumerable<(string Id, string ChangeKey)> itemIds) =>
        Envelope(
            new XElement(M + "GetItem",
                new XElement(M + "ItemShape",
                    new XElement(T + "BaseShape", "IdOnly"),
                    new XElement(T + "BodyType", "Text"),
                    new XElement(T + "AdditionalProperties",
                        Field("item:Subject"),
                        Field("item:DateTimeReceived"),
                        Field("item:ConversationId"),
                        Field("item:Body"),
                        Field("message:From"),
                        Field("message:InternetMessageId"))),
                new XElement(M + "ItemIds",
                    itemIds.Select(i => new XElement(T + "ItemId",
                        new XAttribute("Id", i.Id),
                        new XAttribute("ChangeKey", i.ChangeKey))))));

    /// <summary>Locates the (at most one expected) item with this Internet Message-Id inside
    /// <paramref name="folder"/> — used to get a live ItemId/ChangeKey right before a move, since a
    /// ChangeKey stored earlier can go stale if Exchange touched the item since.</summary>
    public static string BuildFindItemByMessageIdRequest(EwsFolderRef folder, string internetMessageId) =>
        Envelope(
            new XElement(M + "FindItem",
                new XAttribute("Traversal", "Shallow"),
                new XElement(M + "ItemShape",
                    new XElement(T + "BaseShape", "IdOnly")),
                new XElement(M + "Restriction",
                    new XElement(T + "IsEqualTo",
                        new XElement(T + "FieldURI", new XAttribute("FieldURI", "message:InternetMessageId")),
                        new XElement(T + "FieldURIOrConstant",
                            new XElement(T + "Constant", new XAttribute("Value", internetMessageId))))),
                new XElement(M + "ParentFolderIds", ParentFolderElement(folder))));

    public static string BuildMoveItemRequest(EwsItemRef item, EwsFolderRef destinationFolder) =>
        Envelope(
            new XElement(M + "MoveItem",
                new XElement(M + "ToFolderId", ParentFolderElement(destinationFolder)),
                new XElement(M + "ItemIds",
                    new XElement(T + "ItemId",
                        new XAttribute("Id", item.Id),
                        new XAttribute("ChangeKey", item.ChangeKey)))));

    /// <summary>Sets the read/unread flag on an item (e.g. mark unread after an automatic move, so
    /// it stands out to anyone browsing the folder in Outlook).</summary>
    public static string BuildUpdateItemSetReadFlagRequest(EwsItemRef item, bool isRead) =>
        Envelope(
            new XElement(M + "UpdateItem",
                new XAttribute("MessageDisposition", "SaveOnly"),
                new XAttribute("ConflictResolution", "AlwaysOverwrite"),
                new XElement(M + "ItemChanges",
                    new XElement(T + "ItemChange",
                        new XElement(T + "ItemId",
                            new XAttribute("Id", item.Id),
                            new XAttribute("ChangeKey", item.ChangeKey)),
                        new XElement(T + "Updates",
                            new XElement(T + "SetItemField",
                                Field("message:IsRead"),
                                new XElement(T + "Message",
                                    new XElement(T + "IsRead", isRead ? "true" : "false"))))))));

    public static string BuildSendMailRequest(string toAddress, string subject, string body) =>
        Envelope(
            new XElement(M + "CreateItem",
                new XAttribute("MessageDisposition", "SendAndSaveCopy"),
                new XElement(M + "SavedItemFolderId",
                    new XElement(T + "DistinguishedFolderId", new XAttribute("Id", "sentitems"))),
                new XElement(M + "Items",
                    new XElement(T + "Message",
                        new XElement(T + "Subject", subject),
                        new XElement(T + "Body", new XAttribute("BodyType", "Text"), body),
                        new XElement(T + "ToRecipients",
                            new XElement(T + "Mailbox",
                                new XElement(T + "EmailAddress", toAddress)))))));

    private static XElement Field(string uri) =>
        new(T + "FieldURI", new XAttribute("FieldURI", uri));

    private static string Envelope(XElement body)
    {
        var envelope = new XElement(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", Soap),
            new XAttribute(XNamespace.Xmlns + "t", T),
            new XAttribute(XNamespace.Xmlns + "m", M),
            new XElement(Soap + "Header",
                new XElement(T + "RequestServerVersion", new XAttribute("Version", "Exchange2013_SP1"))),
            new XElement(Soap + "Body", body));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), envelope).ToString(SaveOptions.DisableFormatting);
    }
}
