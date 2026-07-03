using Microsoft.Extensions.Logging;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Mail;

namespace OutlookComunaRouter.Ews;

public sealed class EwsEmailReader(IEwsClient client, ILogger<EwsEmailReader> logger) : IEmailReader
{
    private readonly Dictionary<string, EwsFolderRef> folderCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<IncomingEmail>> GetMessagesInFolderAsync(string folderDisplayName, CancellationToken cancellationToken)
    {
        var folder = await ResolveFolderAsync(folderDisplayName, cancellationToken);
        if (folder is null)
        {
            logger.LogWarning(
                "No se pudo resolver la carpeta '{FolderName}'; se omite este ciclo de lectura",
                folderDisplayName);
            return [];
        }

        var findResponse = await client.SendAsync(EwsMessages.BuildFindItemRequest(folder), cancellationToken);
        var itemRefs = EwsResponseParser.ParseFindItemResponse(findResponse);

        if (itemRefs.Count == 0)
        {
            return [];
        }

        var getResponse = await client.SendAsync(
            EwsMessages.BuildGetItemRequest(itemRefs.Select(r => (r.Id, r.ChangeKey))),
            cancellationToken);

        return EwsResponseParser.ParseGetItemResponse(getResponse);
    }

    private async Task<EwsFolderRef?> ResolveFolderAsync(string folderDisplayName, CancellationToken cancellationToken)
    {
        if (folderCache.TryGetValue(folderDisplayName, out var cached))
        {
            return cached;
        }

        var response = await client.SendAsync(EwsMessages.BuildFindFolderRequest(folderDisplayName), cancellationToken);
        var resolved = EwsResponseParser.ParseFindFolderResponse(response);
        if (resolved is not null)
        {
            folderCache[folderDisplayName] = resolved;
        }

        return resolved;
    }
}
