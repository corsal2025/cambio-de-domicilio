using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using OutlookComunaRouter.Configuration;
using OutlookComunaRouter.Domain;

namespace OutlookComunaRouter.Graph;

public interface IEmailReader
{
    Task<IReadOnlyList<IncomingEmail>> GetRecentMessagesAsync(DateTimeOffset since, CancellationToken cancellationToken);
}

public sealed class EmailReader(IGraphClientFactory clientFactory, RouterOptions options, ILogger<EmailReader> logger) : IEmailReader
{
    public async Task<IReadOnlyList<IncomingEmail>> GetRecentMessagesAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        var client = clientFactory.Create();

        var response = await GraphRetryPolicy.ExecuteAsync(
            () => client.Users[options.MailboxAddress].Messages.GetAsync(config =>
            {
                config.QueryParameters.Filter = $"receivedDateTime ge {since:yyyy-MM-ddTHH:mm:ssZ}";
                config.QueryParameters.Select = ["id", "conversationId", "subject", "from", "body", "receivedDateTime"];
                config.QueryParameters.Top = 100;
                config.QueryParameters.Orderby = ["receivedDateTime asc"];
            }, cancellationToken),
            logger,
            cancellationToken);

        var messages = response?.Value ?? [];
        var results = new List<IncomingEmail>();

        foreach (var message in messages)
        {
            var sender = message.From?.EmailAddress?.Address;
            if (string.IsNullOrWhiteSpace(sender) || message.Id is null)
            {
                continue;
            }

            results.Add(new IncomingEmail(
                MessageId: message.Id,
                ConversationId: message.ConversationId ?? string.Empty,
                Subject: message.Subject ?? string.Empty,
                SenderAddress: sender,
                BodyText: StripHtml(message.Body?.Content ?? string.Empty),
                ReceivedAt: message.ReceivedDateTime ?? DateTimeOffset.UtcNow));
        }

        return results;
    }

    private static string StripHtml(string content) =>
        System.Text.RegularExpressions.Regex.Replace(content, "<[^>]+>", " ");
}
