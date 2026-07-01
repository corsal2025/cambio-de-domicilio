using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using OutlookComunaRouter.Configuration;

namespace OutlookComunaRouter.Graph;

public interface IMailSender
{
    Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken);
}

public sealed class MailSender(IGraphClientFactory clientFactory, RouterOptions options, ILogger<MailSender> logger) : IMailSender
{
    public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
    {
        var client = clientFactory.Create();

        var message = new Message
        {
            Subject = subject,
            Body = new ItemBody { ContentType = BodyType.Text, Content = body },
            ToRecipients = [new Recipient { EmailAddress = new EmailAddress { Address = toAddress } }]
        };

        return GraphRetryPolicy.ExecuteAsync(
            () => client.Users[options.MailboxAddress].SendMail.PostAsync(new()
            {
                Message = message,
                SaveToSentItems = true
            }, cancellationToken: cancellationToken),
            logger,
            cancellationToken);
    }
}
