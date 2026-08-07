using CambioDeDomicilio.Mail;

namespace CambioDeDomicilio.Ews;

public sealed class EwsMailSender(IEwsClient client) : IMailSender
{
    public async Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
    {
        var response = await client.SendAsync(
            EwsMessages.BuildSendMailRequest(toAddress, subject, body),
            cancellationToken);

        EwsResponseParser.EnsureSuccess(response, "CreateItem");
    }
}
