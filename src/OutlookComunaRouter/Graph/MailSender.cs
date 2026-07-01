using Microsoft.Graph.Models;
using OutlookComunaRouter.Configuration;

namespace OutlookComunaRouter.Graph;

public interface IMailSender
{
    Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken);
}

public sealed class MailSender(IGraphClientFactory clientFactory, RouterOptions options) : IMailSender
{
    public async Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
    {
        var client = clientFactory.Create();

        var message = new Message
        {
            Subject = subject,
            Body = new ItemBody { ContentType = BodyType.Text, Content = body },
            ToRecipients = [new Recipient { EmailAddress = new EmailAddress { Address = toAddress } }]
        };

        await client.Users[options.MailboxAddress].SendMail.PostAsync(new()
        {
            Message = message,
            SaveToSentItems = true
        }, cancellationToken: cancellationToken);
    }
}
