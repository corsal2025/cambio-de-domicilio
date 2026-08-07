# Email Templates

Templates used by CambioDeDomicilio. Not hardcoded in source — mirrored here (and in `Notifications/EmailTemplates.cs`) so they can be reviewed/edited without digging through code.

## Upload confirmation (sent to the requesting comuna's contact)

Sent automatically once the operator moves the source email from "CARP. PARA PEDIR" to "CARP. YA SUBIDAS" and the configured grace period (default 30 min) has elapsed.

**Subject:**
```
Carpeta subida a Conaset – {{FullName}}, RUT {{Rut}}
```

**Body:**
```
Junto con saludar,

Se informa que la carpeta del contribuyente {{FullName}}, RUT {{Rut}},
solicitada por su comuna, ya fue subida al sistema de Conaset.

Saluda atentamente,
Municipalidad de Valparaíso
```

Placeholders: `{{FullName}}`, `{{Rut}}` — substituted at send time from the tracked `PersonRequest`.

## Confirmation-sent notification (sent to the operator's configured address)

**Subject:**
```
[CambioDeDomicilio] Confirmación enviada – {{FullName}}, RUT {{Rut}} ({{Comuna}})
```

**Body:**
```
Se envió el correo de confirmación de subida a Conaset a la comuna de {{Comuna}}
para el contribuyente {{FullName}}, RUT {{Rut}}.
```
