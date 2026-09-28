# CambioDeDomicilio

> Modelo de dominio y sistema creados por **Raúl Salazar** (Municipalidad de Valparaíso, Dirección de Tránsito). Ver [Autoría](#autoría).

Servicio en segundo plano (.NET 10, `BackgroundService`) que ayuda a tramitar las solicitudes de carpeta de contribuyentes que otras comunas le hacen a Valparaíso, ligadas a Conaset. El flujo de negocio completo está diagramado en [`docs/flujo-proceso.md`](docs/flujo-proceso.md) — acá el resumen técnico:

1. Cada 30 minutos (configurable) revisa la carpeta **"CARP. PARA PEDIR"** del buzón `cambiodedomicilio@munivalpo.cl` en Exchange on-premise (vía EWS). El operador clasifica manualmente los correos entrantes moviéndolos a esa carpeta — el sistema no escanea la bandeja de entrada completa.
2. Por cada correo nuevo de una comuna conocida (dominio comparado contra el directorio, no un patrón adivinado — si el dominio es compartido por varias comunas, como gmail.com, exige coincidencia exacta de la dirección): extrae **nombre del contribuyente y RUT de TODOS los contribuyentes listados** (un correo puede pedir varias personas), validando el dígito verificador y usando el asunto como respaldo cuando el cuerpo no trae datos (el RUT del asunto se confía, el nombre no — siempre exige revisión manual). Registra un caso **Pendiente** por cada contribuyente — no envía ningún correo en este paso.
3. El operador digita manualmente la **fecha de última carpeta** por caso; el sistema deriva el **sector** (Archivo si es anterior a julio 2023, Oficina 43 si es igual o posterior) y puede generar un PDF con los casos de un sector para ir a buscar las carpetas físicas.
4. Para marcar un caso como subido, hay dos caminos: (a) el operador sube la carpeta a Conaset y mueve el correo a **"CARP. YA SUBIDAS"** en Outlook — el sistema lo detecta en el próximo ciclo y marca el caso como **Subida**, sin enviar nada todavía; o (b) desde el dashboard, un botón **"Marcar subida"** hace ambas cosas de inmediato: mueve el correo por EWS y marca el caso.
5. El operador decide cuándo confirmar: con el botón **"Enviar confirmación"** (para casos ya Subidos) o con **"Marcar subida"** (que además confirma en el mismo clic), el sistema envía el correo estándar a la comuna avisando que la carpeta ya se subió, y el caso pasa a **Confirmado**, con registro de quién y cuándo. Nada se envía por el solo hecho de mover el correo.
6. Mantiene un reporte CSV siempre actualizado en `data/reporte.csv` (nombre, RUT, comuna, estado, fecha de última carpeta, sector, fecha de confirmación, y una columna "Requiere revisión" para los casos con datos incompletos) — sin filas duplicadas por persona+comuna. Todos los nombres son editables desde el dashboard, no solo los que requieren revisión.

Diseño y decisiones documentadas en `openspec/specs/routing/`, `openspec/specs/extraction/` y `openspec/specs/dashboard-auth/` (specs vigentes), y en `openspec/changes/archive/2026-07-02-add-address-change-routing/` (diseño original, superado). Reporte técnico consolidado en [`docs/reporte-tecnico.md`](docs/reporte-tecnico.md).

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

**Alternativa** — copiar `src/CambioDeDomicilio/appsettings.Example.json` a `src/CambioDeDomicilio/appsettings.Development.json` (ignorado por git) y completar `Router:Ews:Username`/`Password`, y ajustar `MailboxAddress` a un buzón de prueba si aún no hay acceso al buzón real.

### Datos

1. Crear `data/comunas.csv` (ignorado por git) con el formato de `data/comunas.example.csv`:
   ```csv
   Comuna,ContactEmail,Domain
   Catemu,rfloresc@municatemu.cl,municatemu.cl
   ```
3. `dotnet build`
4. `dotnet run --project src/CambioDeDomicilio`

Modo de verificación sin efectos secundarios (solo lee y cuenta, no envía nada):
```bash
dotnet run --project src/CambioDeDomicilio -- --smoke-test
```

## Dashboard web

El mismo proceso sirve un dashboard en `https://localhost:5001` (el puerto HTTP 5000 solo redirige, nunca entrega datos). No tiene login: corre en la red municipal y el acceso se controla a nivel de red, no de la aplicación.

Desde el dashboard (`/Index`) el operador puede: ver los casos con su estado (Pendiente/Subido/Confirmado), filtrar por estado o por "Requiere revisión", marcar casos con un checkbox propio (organización personal, sin efecto en el flujo), editar el nombre/RUT de cualquier caso, editar la fecha de última carpeta (recalcula el sector al instante, se guarda solo al salir del campo), y confirmar con "Enviar confirmación" (casos Subidos) o "Marcar subida" (mueve el correo y confirma en un solo clic, con diálogo de confirmación porque es irreversible). Los casos Confirmados se resaltan en la tabla. Desde `/Sector/Archivo` o `/Sector/Oficina43` se genera el documento imprimible (imprimir del navegador → PDF) con los casos de ese sector.

Ver `deploy/README.md` para el detalle del certificado HTTPS en producción.

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
docker compose run --rm test        # ejecuta 199 tests
docker compose run --rm publish     # genera single-file exe en ./publish/
```

## Despliegue

### Windows (actual)

Runbook completo, scripts de instalación/desinstalación de la Tarea Programada
y checklist paso a paso en [`deploy/README.md`](deploy/README.md).

**Publicación unificada** (un solo comando):
```powershell
.\deploy\publish.ps1 -DevCert -InstallTask -Shortcut
```

Publica, copia datos, instala tarea y crea acceso directo en un solo paso.
Ver `deploy/README.md` para parámetros detallados.

### VPS (futuro)

- Correrá como servicio `systemd` en vez de tarea de Programador de Tareas.
- El canal de notificación en pantalla (Windows toast) es un no-op automático en Linux; el aviso seguirá funcionando por correo sin cambios de código.
- El alcance de red del endpoint EWS desde un VPS externo aún no está verificado — podría requerir VPN/túnel hacia la red municipal.
- Revisar `docs/backend-standards.md` y los `design.md` de cada cambio en `openspec/` para el detalle de decisiones y riesgos documentados (incluye una nota sobre PII en reposo — depende de cifrado de disco del equipo, no resuelto en código).

## Estructura

```
src/CambioDeDomicilio/
  Domain/              # PersonRequest (Pending/Uploaded/Confirmed), ComunaContact, IncomingEmail
  Configuration/       # RouterOptions (bind de appsettings)
  Extraction/          # PersonDataExtractor: RUT+nombre, multi-contribuyente, respaldo por asunto
  Directories/         # Import de directorio de comunas (CSV) + resolución por dominio/dirección exacta
  Ews/                 # Cliente EWS (SOAP crudo): lectura, envío, mover ítem, marcar no leído
  Mail/                # Interfaces de transporte de correo (IEmailReader, IEmailMover, IMailSender)
  Notifications/       # Plantillas de correo, canal de toast (Windows) y canal de correo
  Persistence/         # Repositorio SQLite (sin ORM)
  Reporting/           # Escritor del reporte CSV (incluye sector derivado)
  Routing/             # Servicio central: detección, extracción, dedup, marcado de subida, confirmación
  RouterWorker.cs      # BackgroundService: orquesta el ciclo de sondeo de ambas carpetas
tests/CambioDeDomicilio.Tests/
```

## Autoría

El modelo de dominio y el sistema Cambio de Domicilio fueron creados por **Raúl Salazar**.

- Diagrama interactivo del modelo: [`docs/modelo-dominio.html`](docs/modelo-dominio.html). Se abre en cualquier navegador, incluso sin internet.
- Presentación del modelo (fuentes de las diapositivas): [`docs/presentacion/`](docs/presentacion/).
- Detalle de autoría: [`AUTHORS.md`](AUTHORS.md).
