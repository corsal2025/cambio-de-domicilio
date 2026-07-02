namespace OutlookComunaRouter.Mail;

public interface IMailSender
{
    Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken);
}
