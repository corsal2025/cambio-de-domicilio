# OutlookComunaRouter

Servicio en segundo plano (.NET 10, `BackgroundService`) que ayuda a tramitar las solicitudes de carpeta de contribuyentes que otras comunas le hacen a Valparaíso, ligadas a Conaset. El flujo de negocio completo está diagramado en [`docs/flujo-proceso.md`](docs/flujo-proceso.md) — acá el resumen técnico:

1. Cada 30 minutos (configurable) revisa la carpeta **"CARP. PARA PEDIR"** del buzón `cambiodedomicilio@munivalpo.cl` en Exchange on-premise (vía EWS). El operador clasifica manualmente los correos entrantes moviéndolos a esa carpeta — el sistema no escanea la bandeja de entrada completa.
2. Por cada correo nuevo de una comuna conocida (dominio comparado contra el directorio, no un patrón adivinado): extrae **nombre del contribuyente, RUT** (valida el dígito verificador, acepta mayúscula/minúscula y con/sin puntos) **y la comuna solicitante**. Lo registra como **Pendiente** — no envía ningún correo en este paso.
3. El operador digita manualmente la **fecha de última carpeta** por caso; el sistema deriva el **sector** (Archivo si es anterior a julio 2023, Oficina 43 si es igual o posterior) y puede generar un PDF con los casos de un sector para ir a buscar las carpetas físicas.
4. Cuando el operador sube la carpeta a Conaset y mueve el correo a **"CARP. YA SUBIDAS"**, el sistema lo detecta y marca el caso como **Subida** — sin enviar nada todavía.
5. El operador decide cuándo confirmar: con un botón ("Enviar confirmación", hoy expuesto vía `SendConfirmationAsync`, próximamente en el dashboard web) envía el correo estándar a la comuna avisando que la carpeta ya se subió, y el caso pasa a **Confirmado**. Nada se envía automáticamente por el solo hecho de mover el correo.
6. Mantiene un reporte CSV siempre actualizado en `data/reporte.csv` (nombre, RUT, comuna, estado, fecha de última carpeta, sector, fecha de confirmación, y una columna "Requiere revisión" para los casos con datos incompletos) — sin filas duplicadas por persona+comuna.

Diseño y decisiones documentadas en `openspec/specs/routing/` (spec vigente) y en los cambios: [`openspec/changes/archive/2026-07-02-add-address-change-routing/`](openspec/changes/archive/2026-07-02-add-address-change-routing/) (diseño original, superado), [`openspec/changes/add-folder-based-triggering/`](openspec/changes/add-folder-based-triggering/) (disparo por carpeta), [`openspec/changes/add-upload-confirmation-flow/`](openspec/changes/add-upload-confirmation-flow/) (flujo vigente: subida + confirmación por botón), [`openspec/changes/add-web-dashboard/`](openspec/changes/add-web-dashboard/) (próximo: interfaz web con el botón de confirmación y generación de PDF). Reporte técnico consolidado en [`docs/reporte-tecnico.md`](docs/reporte-tecnico.md).

## Requisitos

- .NET 10 SDK
- Credenciales de Active Directory del propio buzón (`servervalpo\cambiodedomicilio` o equivalente) para autenticarse contra el endpoint EWS on-premise (`https://mail.munivalpo.cl/EWS/Exchange.asmx`). **No se necesita Azure AD ni aprobación de TI** — el buzón vive en Exchange Server 2016 on-premise, no en Exchange Online (ver `docs/reporte-tecnico.md`, sección 3).
- Dos carpetas deben existir en el buzón: **"CARP. PARA PEDIR"** y **"CARP. YA SUBIDAS"** (nombres configurables vía `Router:SourceFolderName` y `Router:ConfirmationFolderName`).

## Configuración local

### Credenciales (User Secrets)

**Opción recomendada** — las credenciales nunca quedan en texto plano:
```powershell
dotnet user-secrets set "Router:Ews:Username" "servervalpo\cambiodedomicilio"
dotnet user-secrets set "Router:Ews:Password" "tu-contraseña"
dotnet user-secrets set "Router:SqliteDbPath" "data/router.db"
```

**Alternativa** — copiar `src/OutlookComunaRouter/appsettings.Example.json` a `src/OutlookComunaRouter/appsettings.Development.json` (ignorado por git) y completar `Router:Ews:Username`/`Password`, y ajustar `MailboxAddress` a un buzón de prueba si aún no hay acceso al buzón real.

### Datos

1. Crear `data/comunas.csv` (ignorado por git) con el formato de `data/comunas.example.csv`:
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

## Dashboard web

El mismo proceso sirve un dashboard en `https://localhost:5001` (el puerto HTTP 5000 solo redirige, nunca entrega datos). Requiere login por usuario:

```powershell
dotnet run --project src/OutlookComunaRouter -- --add-user operador
```

Desde el dashboard (`/Index`) el operador puede: ver los casos con su estado (Pendiente/Subido/Confirmado), filtrar por estado o por "Requiere revisión", editar la fecha de última carpeta por caso (recalcula el sector al instante), y presionar "Enviar confirmación" en los casos Subidos — que llama a `SendConfirmationAsync` y queda registrado con el usuario y la hora exacta que lo confirmó. Desde `/Sector/Archivo` o `/Sector/Oficina43` se genera el documento imprimible (imprimir del navegador → PDF) con los casos de ese sector.

Ver `deploy/README.md` para el detalle de certificado HTTPS y creación de usuarios en producción.

## Pruebas

```bash
dotnet test
```

### CI automático (GitHub Actions)

Cada push o PR a `main` ejecuta `build + test` automáticamente.
El workflow está en [`.github/workflows/ci.yml`](.github/workflows/ci.yml).

## Docker (desarrollo / build)

Usa contenedores Linux si no tienes .NET SDK local:

```bash
docker compose run --rm build       # compila
docker compose run --rm test        # ejecuta 105 tests
docker compose run --rm publish     # genera single-file exe en ./publish/
```

## Despliegue

### Windows (actual)

Runbook completo, scripts de instalación/desinstalación de la Tarea Programada
y checklist paso a paso en [`deploy/README.md`](deploy/README.md).

**Publicación unificada** (un solo comando):
```powershell
.\deploy\publish.ps1 -DevCert -InstallTask -Shortcut -AddUser operador
```

Publica, copia datos, instala tarea, crea acceso directo y usuario del dashboard
en un solo paso. Ver `deploy/README.md` para parámetros detallados.

### VPS (futuro)

- Correrá como servicio `systemd` en vez de tarea de Programador de Tareas.
- El canal de notificación en pantalla (Windows toast) es un no-op automático en Linux; el aviso seguirá funcionando por correo sin cambios de código.
- El alcance de red del endpoint EWS desde un VPS externo aún no está verificado — podría requerir VPN/túnel hacia la red municipal.
- Revisar `docs/backend-standards.md` y los `design.md` de cada cambio en `openspec/` para el detalle de decisiones y riesgos documentados (incluye una nota sobre PII en reposo — depende de cifrado de disco del equipo, no resuelto en código).

## Estructura

```
src/OutlookComunaRouter/
  Domain/            # PersonRequest (Pending/Uploaded/Confirmed), ComunaContact, IncomingEmail
  Configuration/      # RouterOptions (bind de appsettings)
  Extraction/         # Regex de nombre/RUT + validación de dígito verificador
  Directories/         # Import de directorio de comunas (CSV) + resolución por dominio
  Ews/                # Cliente EWS (SOAP crudo), lectura/envío de correo, resolución de carpeta
  Mail/               # Interfaces de transporte de correo (IEmailReader, IMailSender)
  Notifications/      # Plantillas de correo, canal de toast (Windows) y canal de correo
  Persistence/        # Repositorio SQLite (sin ORM)
  Reporting/           # Escritor del reporte CSV (incluye sector derivado)
  Routing/             # Servicio central: detección, extracción, dedup, marcado de subida, confirmación por botón
  RouterWorker.cs      # BackgroundService: orquesta el ciclo de sondeo de ambas carpetas
tests/OutlookComunaRouter.Tests/
```
