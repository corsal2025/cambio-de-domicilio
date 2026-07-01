# OutlookComunaRouter

Servicio en segundo plano (.NET 10, `BackgroundService`) que:

1. Revisa cada 30 minutos (configurable) el buzón `cambiodedomicilio@munivalpo.cl` vía Microsoft Graph.
2. Detecta notificaciones de "cambio de domicilio" según el dominio del remitente (comparado contra tu directorio de comunas, no una suposición de patrón).
3. Extrae nombre completo y RUT (mayúscula/minúscula, con o sin puntos, valida el dígito verificador).
4. Envía una solicitud formal de "última carpeta" a la comuna correspondiente, evitando duplicados por RUT+comuna.
5. Detecta la respuesta de la comuna (mismo hilo, o correo nuevo con el mismo RUT) y avisa por notificación en pantalla (Windows) + correo.
6. Mantiene un reporte CSV siempre actualizado en `data/reporte.csv`, incluyendo los casos que requieren revisión manual.

Ver el diseño completo en [`openspec/changes/add-address-change-routing/`](openspec/changes/add-address-change-routing/) (propuesta, spec, decisiones de diseño, tareas).

## Requisitos

- .NET 10 SDK
- Un registro de aplicación en **Azure AD** con permisos de aplicación `Mail.Read` y `Mail.Send` (con consentimiento de administrador), acotado al buzón `cambiodedomicilio@munivalpo.cl` mediante una política de acceso de aplicación de Exchange. **Esto requiere coordinación con el equipo de TI del municipio** — ver tarea `0.1` en `tasks.md`.

### Registro de la app en Azure AD (resumen para TI)

1. Azure Portal → Azure Active Directory → App registrations → New registration.
2. Anotar `Application (client) ID` y `Directory (tenant) ID`.
3. Certificates & secrets → crear un client secret, guardarlo (no se puede recuperar después).
4. API permissions → Microsoft Graph → Application permissions → agregar `Mail.Read` y `Mail.Send` → Grant admin consent.
5. **Restringir el acceso solo al buzón objetivo** con una política de acceso de aplicación de Exchange (PowerShell de Exchange Online):
   ```powershell
   New-ApplicationAccessPolicy -AppId <client-id> `
     -PolicyScopeGroupId cambiodedomicilio@munivalpo.cl `
     -AccessRight RestrictAccess `
     -Description "OutlookComunaRouter - solo este buzón"
   ```

## Configuración local

1. Copiar `src/OutlookComunaRouter/appsettings.Example.json` a `src/OutlookComunaRouter/appsettings.Development.json` (ignorado por git) y completar `TenantId`, `ClientId`, `ClientSecret`, y ajustar `MailboxAddress` a un buzón de prueba si aún no hay acceso al buzón real.
2. Crear `data/comunas.csv` (ignorado por git) con el formato de `data/comunas.example.csv`:
   ```csv
   Comuna,ContactEmail,Domain
   Catemu,rfloresc@municatemu.cl,municatemu.cl
   ```
3. `dotnet build`
4. `dotnet run --project src/OutlookComunaRouter`

## Pruebas

```bash
dotnet test
```

## Despliegue

### Windows (actual)

Ejecutar como tarea de Programador de Tareas de Windows configurada para "Ejecutar sea que el usuario haya iniciado sesión o no", con reinicio automático si falla, apuntando al ejecutable publicado (`dotnet publish -c Release`). El proceso queda corriendo continuamente (no es una tarea de un solo disparo).

### VPS (futuro)

- Correrá como servicio `systemd` en vez de tarea de Programador de Tareas.
- El canal de notificación en pantalla (Windows toast) es un no-op automático en Linux; el aviso seguirá funcionando por correo sin cambios de código.
- Revisar `docs/backend-standards.md` y `openspec/changes/add-address-change-routing/design.md` para el detalle de decisiones y riesgos documentados (incluye una nota sobre PII en reposo — depende de cifrado de disco del equipo, no resuelto en código).

## Estructura

```
src/OutlookComunaRouter/
  Domain/            # PersonRequest, ComunaContact, IncomingEmail
  Configuration/      # RouterOptions (bind de appsettings)
  Extraction/         # Regex de nombre/RUT + validación de dígito verificador
  Directories/         # Import de directorio de comunas (CSV) + resolución por dominio
  Graph/              # Cliente de Microsoft Graph, lectura y envío de correo
  Notifications/      # Plantillas de correo, canal de toast (Windows) y canal de correo
  Persistence/        # Repositorio SQLite (sin ORM)
  Reporting/           # Escritor del reporte CSV
  Routing/             # Servicio central: detección, extracción, dedup, ruteo, matching de respuestas
  RouterWorker.cs      # BackgroundService: orquesta el ciclo de sondeo
tests/OutlookComunaRouter.Tests/
```
