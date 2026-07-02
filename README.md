# OutlookComunaRouter

Servicio en segundo plano (.NET 10, `BackgroundService`) que:

1. Revisa cada 30 minutos (configurable) la carpeta **"Para pedir"** del buzón `cambiodedomicilio@munivalpo.cl` en Exchange on-premise (vía EWS). No lee la bandeja de entrada completa: el disparador es que el operador mueve manualmente los correos a esa carpeta. La carpeta **"Carpetas subidas a Conaset"** (archivo de casos ya cerrados) queda fuera de alcance a propósito — el sistema nunca la lee ni la modifica.
2. Detecta notificaciones de "cambio de domicilio" según el dominio del remitente (comparado contra tu directorio de comunas, no una suposición de patrón).
3. Extrae nombre completo y RUT (mayúscula/minúscula, con o sin puntos, valida el dígito verificador).
4. Envía una solicitud formal de "última carpeta" a la comuna correspondiente, evitando duplicados por RUT+comuna.
5. Detecta la respuesta de la comuna (mismo hilo, o correo nuevo con el mismo RUT) y avisa por notificación en pantalla (Windows) + correo.
6. Mantiene un reporte CSV siempre actualizado en `data/reporte.csv`, incluyendo los casos que requieren revisión manual.

Ver el diseño completo en `openspec/specs/routing/` (spec vigente) y en los cambios documentados: [`openspec/changes/archive/2026-07-02-add-address-change-routing/`](openspec/changes/archive/2026-07-02-add-address-change-routing/) (diseño original), [`openspec/changes/add-folder-based-triggering/`](openspec/changes/add-folder-based-triggering/) (disparo por carpeta), [`openspec/changes/add-web-dashboard/`](openspec/changes/add-web-dashboard/) (próximo: interfaz web). Reporte técnico consolidado en [`docs/reporte-tecnico.md`](docs/reporte-tecnico.md).

## Requisitos

- .NET 10 SDK
- Credenciales de Active Directory del propio buzón (`servervalpo\cambiodedomicilio` o equivalente) para autenticarse contra el endpoint EWS on-premise (`https://mail.munivalpo.cl/EWS/Exchange.asmx`). **No se necesita Azure AD ni aprobación de TI** — el buzón vive en Exchange Server 2016 on-premise, no en Exchange Online (ver `docs/reporte-tecnico.md`, sección 3, para el detalle de esta verificación).
- Una carpeta llamada **"Para pedir"** debe existir en el buzón (el nombre es configurable vía `Router:SourceFolderName`).

## Configuración local

1. Copiar `src/OutlookComunaRouter/appsettings.Example.json` a `src/OutlookComunaRouter/appsettings.Development.json` (ignorado por git) y completar `Router:Ews:Username`/`Password`, y ajustar `MailboxAddress` a un buzón de prueba si aún no hay acceso al buzón real.
2. Crear `data/comunas.csv` (ignorado por git) con el formato de `data/comunas.example.csv`:
   ```csv
   Comuna,ContactEmail,Domain
   Catemu,rfloresc@municatemu.cl,municatemu.cl
   ```
3. `dotnet build`
4. `dotnet run --project src/OutlookComunaRouter`

Modo de verificación sin efectos secundarios (solo lee y cuenta, no envía nada):
```bash
dotnet run --project src/OutlookComunaRouter -- --smoke-test
```

## Pruebas

```bash
dotnet test
```

## Despliegue

### Windows (actual)

Runbook completo, scripts de instalación/desinstalación de la Tarea Programada
y checklist paso a paso en [`deploy/README.md`](deploy/README.md).

### VPS (futuro)

- Correrá como servicio `systemd` en vez de tarea de Programador de Tareas.
- El canal de notificación en pantalla (Windows toast) es un no-op automático en Linux; el aviso seguirá funcionando por correo sin cambios de código.
- El alcance de red del endpoint EWS desde un VPS externo aún no está verificado — podría requerir VPN/túnel hacia la red municipal.
- Revisar `docs/backend-standards.md` y los `design.md` de cada cambio en `openspec/` para el detalle de decisiones y riesgos documentados (incluye una nota sobre PII en reposo — depende de cifrado de disco del equipo, no resuelto en código).

## Estructura

```
src/OutlookComunaRouter/
  Domain/            # PersonRequest, ComunaContact, IncomingEmail
  Configuration/      # RouterOptions (bind de appsettings)
  Extraction/         # Regex de nombre/RUT + validación de dígito verificador
  Directories/         # Import de directorio de comunas (CSV) + resolución por dominio
  Ews/                # Cliente EWS (SOAP crudo), lectura/envío de correo, resolución de carpeta
  Mail/               # Interfaces de transporte de correo (IEmailReader, IMailSender)
  Notifications/      # Plantillas de correo, canal de toast (Windows) y canal de correo
  Persistence/        # Repositorio SQLite (sin ORM)
  Reporting/           # Escritor del reporte CSV
  Routing/             # Servicio central: detección, extracción, dedup, ruteo, matching de respuestas
  RouterWorker.cs      # BackgroundService: orquesta el ciclo de sondeo
tests/OutlookComunaRouter.Tests/
```
