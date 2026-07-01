# Email Templates

Templates used by OutlookComunaRouter. Not hardcoded in source — loaded from configuration/resources so they can be edited without a code change.

## Folder request (sent to comuna contact)

**Subject:**
```
Solicitud de última carpeta – Cambio de Domicilio – {{FullName}}, RUT {{Rut}}
```

**Body:**
```
Junto con saludar,

Por medio del presente correo, se solicita a Uds. tengan a bien remitir la última
carpeta tributaria/municipal correspondiente al contribuyente {{FullName}},
RUT {{Rut}}, quien registra un cambio de domicilio hacia la comuna de Valparaíso.

Agradecemos remitir la documentación a la brevedad a este mismo correo, indicando
la fecha de la última carpeta emitida.

Saluda atentamente,
Municipalidad de Valparaíso
```

Placeholders: `{{FullName}}`, `{{Rut}}` — substituted at send time from the extracted `PersonRequest`.

## Reply notification (sent to the operator's configured address)

**Subject:**
```
[OutlookComunaRouter] Respuesta recibida – {{FullName}}, RUT {{Rut}} ({{Comuna}})
```

**Body:**
```
Se recibió respuesta de la comuna de {{Comuna}} para el contribuyente
{{FullName}}, RUT {{Rut}}.

Verificar la carpeta recibida antes de continuar con la tramitación.
```
