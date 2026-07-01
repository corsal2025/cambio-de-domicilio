namespace OutlookComunaRouter.Notifications;

public static class EmailTemplates
{
    public static (string Subject, string Body) FolderRequest(string fullName, string rut) => (
        Subject: $"Solicitud de última carpeta – Cambio de Domicilio – {fullName}, RUT {rut}",
        Body: $"""
            Junto con saludar,

            Por medio del presente correo, se solicita a Uds. tengan a bien remitir la última
            carpeta tributaria/municipal correspondiente al contribuyente {fullName},
            RUT {rut}, quien registra un cambio de domicilio hacia la comuna de Valparaíso.

            Agradecemos remitir la documentación a la brevedad a este mismo correo, indicando
            la fecha de la última carpeta emitida.

            Saluda atentamente,
            Municipalidad de Valparaíso
            """);

    public static (string Subject, string Body) ReplyNotification(string fullName, string rut, string comuna) => (
        Subject: $"[OutlookComunaRouter] Respuesta recibida – {fullName}, RUT {rut} ({comuna})",
        Body: $"""
            Se recibió respuesta de la comuna de {comuna} para el contribuyente
            {fullName}, RUT {rut}.

            Verificar la carpeta recibida antes de continuar con la tramitación.
            """);
}
