using OutlookComunaRouter.Domain;
using OutlookComunaRouter.Mail;

namespace OutlookComunaRouter.Ews;

public sealed class EwsEmailReader(IEwsClient client) : IEmailReader
{
    public async Task<IReadOnlyList<IncomingEmail>> GetRecentMessagesAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        var findResponse = await client.SendAsync(EwsMessages.BuildFindItemRequest(since), cancellationToken);
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
}
