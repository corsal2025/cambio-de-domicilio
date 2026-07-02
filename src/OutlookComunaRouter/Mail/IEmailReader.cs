using OutlookComunaRouter.Domain;

namespace OutlookComunaRouter.Mail;

public interface IEmailReader
{
    Task<IReadOnlyList<IncomingEmail>> GetRecentMessagesAsync(DateTimeOffset since, CancellationToken cancellationToken);
}
