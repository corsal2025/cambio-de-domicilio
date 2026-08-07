using Microsoft.Extensions.Logging;
using CambioDeDomicilio.Domain;
using CambioDeDomicilio.Mail;

namespace CambioDeDomicilio.Ews;

public sealed class EwsEmailReader(IEwsClient client, ILogger<EwsEmailReader> logger) : IEmailReader, IEmailMover
{
    /// <summary>Exchange throttling rejects large GetItem batches; 50 stays well under the default policy.</summary>
    private const int GetItemBatchSize = 50;

    private readonly Dictionary<string, EwsFolderRef> folderCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<IncomingEmail>> GetMessagesInFolderAsync(string folderDisplayName, CancellationToken cancellationToken)
    {
        try
        {
            return await ListFolderAsync(folderDisplayName, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            // A cached folder id can go stale if the operator renames or recreates the folder in
            // Outlook. Drop the cache entry, re-resolve, and retry once so the poller self-heals
            // instead of failing every cycle until the process restarts.
            logger.LogWarning(ex,
                "Fallo EWS con la carpeta '{FolderName}' (posible referencia obsoleta); se re-resuelve y reintenta",
                folderDisplayName);
            folderCache.Remove(folderDisplayName);
            return await ListFolderAsync(folderDisplayName, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<IncomingEmail>> ListFolderAsync(string folderDisplayName, CancellationToken cancellationToken)
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

        var results = new List<IncomingEmail>(itemRefs.Count);
        foreach (var chunk in itemRefs.Chunk(GetItemBatchSize))
        {
            var getResponse = await client.SendAsync(
                EwsMessages.BuildGetItemRequest(chunk.Select(r => (r.Id, r.ChangeKey))),
                cancellationToken);
            results.AddRange(EwsResponseParser.ParseGetItemResponse(getResponse));
        }

        return results;
    }

    public async Task<bool> MoveAndMarkUnreadAsync(string messageId, string sourceFolderDisplayName, string destinationFolderDisplayName, CancellationToken cancellationToken)
    {
        var sourceFolder = await ResolveFolderAsync(sourceFolderDisplayName, cancellationToken);
        var destinationFolder = await ResolveFolderAsync(destinationFolderDisplayName, cancellationToken);
        if (sourceFolder is null || destinationFolder is null)
        {
            logger.LogWarning(
                "No se pudo resolver '{Source}' o '{Destination}' para mover el correo; se omite el movimiento",
                sourceFolderDisplayName, destinationFolderDisplayName);
            return false;
        }

        var findResponse = await client.SendAsync(
            EwsMessages.BuildFindItemByMessageIdRequest(sourceFolder, messageId), cancellationToken);
        var match = EwsResponseParser.ParseFindItemResponse(findResponse).FirstOrDefault();
        if (match is null)
        {
            logger.LogWarning(
                "No se encontró el correo original en '{Folder}' para moverlo (puede ya haber sido movido)",
                sourceFolderDisplayName);
            return false;
        }

        var moveResponse = await client.SendAsync(EwsMessages.BuildMoveItemRequest(match, destinationFolder), cancellationToken);
        var movedItem = EwsResponseParser.ParseMoveItemResponse(moveResponse);
        if (movedItem is not null)
        {
            var updateResponse = await client.SendAsync(
                EwsMessages.BuildUpdateItemSetReadFlagRequest(movedItem, isRead: false), cancellationToken);
            EwsResponseParser.EnsureSuccess(updateResponse, "UpdateItem");
        }

        return true;
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
