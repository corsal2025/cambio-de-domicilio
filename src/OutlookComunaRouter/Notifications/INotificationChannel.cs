namespace OutlookComunaRouter.Notifications;

public interface INotificationChannel
{
    void NotifyResponded(string fullName, string rut, string comuna);
}
