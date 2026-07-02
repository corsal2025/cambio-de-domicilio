using Microsoft.Extensions.Logging;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Mail;

namespace OutlookComunaRouter.Ews;

public sealed class EwsEmailReader(IEwsClient client, RouterOptions options, ILogger<EwsEmailReader> logger) : IEmailReader
{
    private EwsFolderRef? cachedFolder;

    public async Task<IReadOnlyList<IncomingEmail>> GetRecentMessagesAsync(CancellationToken cancellationToken)
    {
        var folder = await ResolveFolderAsync(cancellationToken);
        if (folder is null)
        {
            logger.LogWarning(
                "No se pudo resolver la carpeta '{FolderName}'; se omite este ciclo de lectura",
                options.SourceFolderName);
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

    private async Task<EwsFolderRef?> ResolveFolderAsync(CancellationToken cancellationToken)
    {
        if (cachedFolder is not null)
        {
            return cachedFolder;
        }

        var response = await client.SendAsync(EwsMessages.BuildFindFolderRequest(options.SourceFolderName), cancellationToken);
        cachedFolder = EwsResponseParser.ParseFindFolderResponse(response);
        return cachedFolder;
    }
}
