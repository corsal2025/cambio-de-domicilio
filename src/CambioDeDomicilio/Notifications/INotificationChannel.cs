namespace CambioDeDomicilio.Notifications;

public interface INotificationChannel
{
    void NotifyConfirmationSent(string fullName, string rut, string comuna);
}
