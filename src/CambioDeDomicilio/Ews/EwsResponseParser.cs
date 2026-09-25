using System.Xml.Linq;
using CambioDeDomicilio.Domain;

namespace CambioDeDomicilio.Ews;

public sealed record EwsItemRef(string Id, string ChangeKey);

public static class EwsResponseParser
{
    private static readonly XNamespace T = EwsMessages.T;
    private static readonly XNamespace M = EwsMessages.M;

    /// <summary>Throws if the SOAP response reports an error response class.</summary>
    public static void EnsureSuccess(XDocument document, string operation)
    {
        var responseMessage = document.Descendants()
            .FirstOrDefault(e => e.Name.Namespace == M && e.Name.LocalName.EndsWith("ResponseMessage"));

        var responseClass = responseMessage?.Attribute("ResponseClass")?.Value;
        if (responseClass == "Error")
        {
            var code = responseMessage?.Element(M + "ResponseCode")?.Value ?? "unknown";
            throw new InvalidOperationException($"EWS {operation} failed with response code {code}");
        }
    }

    public static (IReadOnlyList<EwsItemRef> Items, bool IncludesLastItemInRange) ParseFindItemPagedResponse(XDocument document)
    {
        EnsureSuccess(document, "FindItem");

        var rootFolder = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "RootFolder");
        var includesLastAttr = rootFolder?.Attribute("IncludesLastItemInRange")?.Value;
        var includesLast = includesLastAttr is null || string.Equals(includesLastAttr, "true", StringComparison.OrdinalIgnoreCase);

        var items = document.Descendants(T + "ItemId")
            .Select(e => new EwsItemRef(
                e.Attribute("Id")?.Value ?? string.Empty,
                e.Attribute("ChangeKey")?.Value ?? string.Empty))
            .Where(r => r.Id.Length > 0)
            .ToList();

        return (items, includesLast);
    }

    public static IReadOnlyList<EwsItemRef> ParseFindItemResponse(XDocument document) =>
        ParseFindItemPagedResponse(document).Items;

    /// <summary>Returns the first matching folder's reference, or null when FindFolder found no match.</summary>
    public static EwsFolderRef? ParseFindFolderResponse(XDocument document)
    {
        EnsureSuccess(document, "FindFolder");

        var folderId = document.Descendants(T + "FolderId").FirstOrDefault();
        if (folderId is null)
        {
            return null;
        }

        var id = folderId.Attribute("Id")?.Value;
        var changeKey = folderId.Attribute("ChangeKey")?.Value;

        return string.IsNullOrEmpty(id) || string.IsNullOrEmpty(changeKey)
            ? null
            : EwsFolderRef.ByFolderId(id, changeKey);
    }

    /// <summary>The moved item's new ItemId/ChangeKey at its destination folder, or null if the
    /// response reported success but included no item (shouldn't happen in practice).</summary>
    public static EwsItemRef? ParseMoveItemResponse(XDocument document)
    {
        EnsureSuccess(document, "MoveItem");

        var itemId = document.Descendants(T + "ItemId").FirstOrDefault();
        if (itemId is null)
        {
            return null;
        }

        var id = itemId.Attribute("Id")?.Value;
        var changeKey = itemId.Attribute("ChangeKey")?.Value;
        return string.IsNullOrEmpty(id) || string.IsNullOrEmpty(changeKey) ? null : new EwsItemRef(id, changeKey);
    }

    public static IReadOnlyList<IncomingEmail> ParseGetItemResponse(XDocument document)
    {
        EnsureSuccess(document, "GetItem");

        var results = new List<IncomingEmail>();
        foreach (var message in document.Descendants(T + "Message"))
        {
            var internetMessageId = message.Element(T + "InternetMessageId")?.Value;
            var sender = message.Element(T + "From")?
                .Element(T + "Mailbox")?
                .Element(T + "EmailAddress")?.Value;

            if (string.IsNullOrWhiteSpace(internetMessageId) || string.IsNullOrWhiteSpace(sender))
            {
                continue;
            }

            var received = DateTimeOffset.TryParse(
                message.Element(T + "DateTimeReceived")?.Value, out var parsed)
                ? parsed
                : DateTimeOffset.UtcNow;

            results.Add(new IncomingEmail(
                MessageId: internetMessageId,
                ConversationId: message.Element(T + "ConversationId")?.Attribute("Id")?.Value ?? string.Empty,
                Subject: message.Element(T + "Subject")?.Value ?? string.Empty,
                SenderAddress: sender,
                BodyText: message.Element(T + "Body")?.Value ?? string.Empty,
                ReceivedAt: received));
        }

        return results;
    }
}
