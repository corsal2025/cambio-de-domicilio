namespace OutlookComunaRouter.Notifications;

public static class EmailTemplates
{
    public static (string Subject, string Body) UploadConfirmation(string fullName, string rut) => (
        Subject: $"Carpeta subida a Conaset – {fullName}, RUT {rut}",
        Body: $"""
            Junto con saludar,

            Se informa que la carpeta del contribuyente {fullName}, RUT {rut},
            solicitada por su comuna, ya fue subida al sistema de Conaset.

            Saluda atentamente,
            Municipalidad de Valparaíso
            """);

    public static (string Subject, string Body) ConfirmationSentNotification(string fullName, string rut, string comuna) => (
        Subject: $"[OutlookComunaRouter] Confirmación enviada – {fullName}, RUT {rut} ({comuna})",
        Body: $"""
            Se envió el correo de confirmación de subida a Conaset a la comuna de {comuna}
            para el contribuyente {fullName}, RUT {rut}.
            """);
}
