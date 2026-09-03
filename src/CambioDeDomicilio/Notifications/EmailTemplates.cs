namespace CambioDeDomicilio.Notifications;

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

    /// <summary>Sent instead of <see cref="UploadConfirmation"/> when the case was resolved through
    /// the F8 process (physical folder never located in storage) rather than the normal upload —
    /// same "carpeta subida" outcome for the comuna, but the wording discloses how it was resolved.</summary>
    public static (string Subject, string Body) UploadConfirmationF8(string fullName, string rut) => (
        Subject: $"Carpeta subida a Conaset – {fullName}, RUT {rut}",
        Body: $"""
            Junto con saludar,

            Se informa que la información del contribuyente {fullName}, RUT {rut},
            solicitada por su comuna, ya fue subida al sistema de Conaset.

            Cabe hacer presente que, dado que no fue posible ubicar la carpeta física en nuestro
            sistema de almacenamiento, la gestión se realizó a través del proceso F8.

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

    public static (string Subject, string Body) ConfirmationSentNotification(string fullName, string rut, string comuna) => (
        Subject: $"[CambioDeDomicilio] Confirmación enviada – {fullName}, RUT {rut} ({comuna})",
        Body: $"""
            Se envió el correo de confirmación de subida a Conaset a la comuna de {comuna}
            para el contribuyente {fullName}, RUT {rut}.
            """);
}
