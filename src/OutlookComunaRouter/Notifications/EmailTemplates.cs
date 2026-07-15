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

    /// <summary>Sent when the operator undoes a Confirmed case by mistake — the original
    /// UploadConfirmation email already reached the comuna, so this explicitly retracts it rather
    /// than silently reverting the case (which would leave the comuna believing it's done).</summary>
    public static (string Subject, string Body) ConfirmationRectification(string fullName, string rut) => (
        Subject: $"Rectificación – Carpeta subida a Conaset – {fullName}, RUT {rut}",
        Body: $"""
            Junto con saludar,

            Se informa que la confirmación de carpeta subida enviada anteriormente para el
            contribuyente {fullName}, RUT {rut}, fue enviada por error y debe considerarse sin
            efecto. La carpeta aún NO ha sido subida al sistema de Conaset.

            Disculpe las molestias.

            Saluda atentamente,
            Municipalidad de Valparaíso
            """);

    public static (string Subject, string Body) PasswordReset(string username, string resetLink) => (
        Subject: "Recuperación de contraseña – OutlookComunaRouter",
        Body: $"""
            Se solicitó restablecer la contraseña del usuario '{username}' del dashboard OutlookComunaRouter.

            Si fuiste tú, ingresa al siguiente enlace para elegir una nueva contraseña
            (válido por 30 minutos, un solo uso):
            {resetLink}

            Si no fuiste tú, ignora este correo — tu contraseña actual sigue funcionando sin cambios.
            """);

    public static (string Subject, string Body) ConfirmationSentNotification(string fullName, string rut, string comuna) => (
        Subject: $"[OutlookComunaRouter] Confirmación enviada – {fullName}, RUT {rut} ({comuna})",
        Body: $"""
            Se envió el correo de confirmación de subida a Conaset a la comuna de {comuna}
            para el contribuyente {fullName}, RUT {rut}.
            """);

}
