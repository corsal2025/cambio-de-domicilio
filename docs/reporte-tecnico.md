# Reporte Técnico — OutlookComunaRouter

**Última actualización:** 2026-07-07
**Fuente:** consolidado desde los artefactos OpenSpec del proyecto (`openspec/specs/`, `openspec/changes/`) y la sesión de trabajo del 2026-07-06/07 — la metodología del proyecto exige que toda decisión quede documentada ahí antes de implementarse.

---

## 1. Qué es el sistema

`OutlookComunaRouter` es un servicio .NET 10 con dashboard web integrado que automatiza el trámite de solicitudes de carpeta de contribuyentes que **otras comunas le piden a Valparaíso** (ligado a Conaset): detecta las solicitudes que llegan por correo, extrae los datos de la persona (incluso cuando un mismo correo pide varias personas), acompaña el trabajo manual del operador (buscar/escanear/subir la carpeta), y envía el aviso de "carpeta subida" a la comuna solicitante — ya sea en dos pasos manuales, o en un solo clic que mueve el correo y confirma de inmediato.

## 2. Estado de los cambios (OpenSpec)

| Cambio | Estado |
|---|---|
| `add-address-change-routing` | Archivado (diseño original, superado — asumía el flujo en dirección inversa) |
| `add-folder-based-triggering` | Implementado e integrado al spec vigente de `routing` |
| `add-upload-confirmation-flow` | Implementado e integrado al spec vigente de `routing` |
| `add-web-dashboard` | Implementado e integrado al spec vigente de `routing` |
| `extraction` (spec nuevo, 2026-07-07) | Algoritmo de extracción de nombre/RUT, multi-contribuyente, respaldo por asunto, reordenamiento Viña |
| `dashboard-auth` (spec nuevo, 2026-07-07) | Login, cambio de contraseña, recuperación por correo |

> Nota de higiene documental: el spec `routing/spec.md` había quedado congelado en el diseño original archivado (hablaba de "enviar solicitud automática" y estados `sent`/`responded`, que nunca se implementaron así). Se corrigió el 2026-07-07 para reflejar el comportamiento real: Pendiente → Subido → Confirmado, disparado por carpeta, sin envío automático.

## 3. Diagrama de flujo general del sistema

```
                        ┌─────────────────────────────┐
                        │   Otra municipalidad envía   │
                        │   correo pidiendo carpeta    │
                        │   de uno o más contribuyentes│
                        └──────────────┬──────────────┘
                                       │
                                       ▼
                        ┌─────────────────────────────┐
                        │  Buzón Exchange on-premise   │
                        │  cambiodedomicilio@          │
                        │  munivalpo.cl                │
                        └──────────────┬──────────────┘
                                       │  el operador clasifica
                                       │  manualmente (arrastra)
                                       ▼
                        ┌─────────────────────────────┐
                        │   Carpeta Outlook:           │
                        │   "CARP. PARA PEDIR"         │
                        └──────────────┬──────────────┘
                                       │  cada 30 min (EWS)
                                       ▼
        ┌──────────────────────────────────────────────────────────┐
        │              SERVICIO (RouterWorker)                       │
        │                                                            │
        │  ┌────────────┐   ┌─────────────┐   ┌──────────────┐      │
        │  │ ¿Dominio    │──►│ Extraer     │──►│ ¿Duplicado    │     │
        │  │ de comuna   │no │ TODOS los   │   │ (RUT+comuna)? │     │
        │  │ conocido?   │─┐ │ contribuy.  │   └──────┬───────┘     │
        │  └────────────┘ │ │ del cuerpo  │          │no            │
        │   (dominio       │ │  Y asunto   │          ▼             │
        │   compartido→    │ └─────┬──────┘   ┌──────────────┐     │
        │   exige dirección│       │¿faltó     │ Registrar    │     │
        │   exacta)        │       │ alguno?   │ un caso por  │     │
        │           (se     ▼           ▼      │ contribuyente│     │
        │            descarta)  ┌──────────────┐└──────────────┘     │
        │                       │ PENDIENTE +  │                     │
        │                       │ "Requiere    │                     │
        │                       │  revisión"   │                     │
        │                       └──────────────┘                     │
        └────────────────────────────┬───────────────────────────────┘
                                       │
                                       ▼
                        ┌─────────────────────────────┐
                        │   DASHBOARD WEB (HTTPS)      │
                        │   https://<pc>:5001          │
                        │   - login + recuperación      │
                        │   - lista de casos, editable  │
                        │   - fecha última carpeta ────┼──► deriva SECTOR:
                        │     (la digita el operador)   │    < jul 2023 → Archivo
                        │   - "Marcar subida" (1 clic:  │    ≥ jul 2023 → Oficina 43
                        │     mueve correo + confirma)   │
                        │   - PDF por sector             │
                        │   - directorio de comunas       │
                        └─────────────────────────────┘
```

## 4. Diagrama del ciclo de vida de un caso

```
   correo llega a          operador sube la           el operador tiene DOS
   CARP. PARA PEDIR         carpeta a Conaset          caminos para confirmar:
        │
        ▼
  ┌───────────┐
  │ PENDIENTE │
  └─────┬─────┘
        │
        ├── CAMINO A (manual, 2 pasos): mueve el correo a CARP. YA SUBIDAS
        │   en Outlook → el sistema lo detecta en el próximo ciclo →
        │   pasa a SUBIDO → operador aprieta "Enviar confirmación" → CONFIRMADO
        │
        └── CAMINO B (1 clic): botón "Marcar subida" en el dashboard →
            el sistema busca el correo por su Message-ID, lo MUEVE él mismo
            a CARP. YA SUBIDAS, lo marca no leído, pasa a SUBIDO, y de
            inmediato envía la confirmación → CONFIRMADO
            (con diálogo de confirmación de JS antes, porque es irreversible
             y envía un correo real a un municipio externo)

  REGLAS CLAVE (sin cambios de diseño, solo más caminos para llegar):
  • Mover el correo a CARP. YA SUBIDAS NO envía nada por sí solo.
  • El correo a la comuna sale ÚNICAMENTE con una acción explícita del operador
    (botón "Enviar confirmación", o botón "Marcar subida").
  • Un caso Pendiente o ya Confirmado no puede confirmarse otra vez.
  • Nunca hay dos filas para la misma persona+comuna (deduplicación).
  • Si el correo no se encuentra para moverlo (Camino B), el caso avanza
    igual — no se bloquea por un problema del buzón.
```

## 5. Diagrama del flujo de extracción de datos (calibrado con correos reales)

```
   Cuerpo del correo
        │
        ▼
   ¿Tiene el aviso "CORREO EXTERNO" de Exchange?
        │ sí → se elimina antes de extraer
        ▼
   Buscar TODOS los RUT del cuerpo (no solo el primero), en orden de prioridad:
     1. Con prefijo:  RUT: / R.U.T. / RUN / R.U.N.
     2. Pelado con puntos:  12.345.678-9
     3. Pelado sin puntos:  12345678-9
     4. Separado por espacios: 12345678        9   (formato Viña del Mar)
   Siempre validado con el dígito verificador chileno.
        │
        ├── CERO RUT válidos en el cuerpo
        │        └──► probar el ASUNTO (mismo algoritmo, pero el NOMBRE
        │             encontrado ahí NUNCA se usa — solo el RUT). Si ni
        │             siquiera hay RUT en el asunto: 1 caso "Requiere
        │             revisión" sin nombre ni RUT, para no perder el correo.
        ▼
   Por CADA RUT válido encontrado (uno o varios contribuyentes):
     Buscar el NOMBRE en la ventana de texto pegada a ESE RUT específico
     (antes del RUT; si no, después), acotada por los RUT vecinos para que
     el nombre de un contribuyente nunca se mezcle con el del siguiente.
     Quita don/doña/Sr./Sra./RUT/RUN de los bordes.
        │
        ▼
   ¿El RUT vino del formato Viña (separado por espacios) Y el nombre
   tiene exactamente 4 palabras?
        │ sí → reordenar de "APELLIDO APELLIDO NOMBRE NOMBRE"
        │      a "NOMBRE NOMBRE APELLIDO APELLIDO"
        ▼
   Un caso registrado por cada contribuyente encontrado,
   con nombre + RUT normalizado (12.345.678-9)
```

**Por qué el nombre del asunto nunca se confía**: el 2026-07-07 un correo real con asunto `Fwd: SUBIR CARPETA PLATAFORMA CONASET 14.148.466-4` (una instrucción, sin ningún nombre de persona) hizo que el sistema tomara "SUBIR CARPETA PLATAFORMA CONASET" como si fuera el nombre del contribuyente, y lo marcó como caso limpio (sin revisión). A diferencia del cuerpo del correo (calibrado contra 38 correos reales con una plantilla razonablemente consistente), el asunto varía muchísimo de comuna a comuna y no hay forma confiable de distinguir un nombre real de una frase instructiva. Por eso, desde ese incidente, **el RUT del asunto se acepta, pero el nombre del asunto jamás** — el caso siempre queda "Requiere revisión" para que el operador confirme el nombre a mano.

## 6. Arquitectura de acceso al correo (por qué no se usa Azure)

El buzón `cambiodedomicilio@munivalpo.cl` vive en un **Exchange Server 2016 on-premise** (`mail.munivalpo.cl`), no en Exchange Online. Microsoft Graph no puede alcanzar buzones on-premise, así que la integración es **EWS** (SOAP) con las credenciales de Active Directory del propio buzón. **No se necesita Azure AD, ni registro de aplicación, ni aprobación de TI.** El cliente EWS es propio (SOAP crudo sobre `HttpClient`), porque el paquete oficial de Microsoft es solo .NET Framework y está abandonado.

Operaciones EWS implementadas:
- `FindItem` / `GetItem`: listar y leer correos de una carpeta (con caché de ID de carpeta y reintento si queda obsoleto).
- `FindFolder`: resolver una carpeta por su nombre visible.
- `CreateItem` (`SendAndSaveCopy`): enviar el correo de confirmación.
- `FindItem` con `Restriction` por `message:InternetMessageId` (nuevo, 2026-07-07): ubicar el correo original justo antes de moverlo, sin depender de un `ChangeKey` guardado que podría estar obsoleto.
- `MoveItem` (nuevo): mover el correo de "CARP. PARA PEDIR" a "CARP. YA SUBIDAS".
- `UpdateItem` (nuevo): marcar el correo como no leído en su nueva carpeta, para que el equipo lo note en Outlook.

## 7. Componentes técnicos

| Componente | Tecnología | Rol |
|---|---|---|
| Servicio de sondeo | .NET 10 `BackgroundService` | Lee ambas carpetas Outlook cada 30 min vía EWS |
| Dashboard | ASP.NET Core Razor Pages (mismo proceso) | UI del operador, HTTPS-only en puerto 5001 |
| Autenticación | PBKDF2 + cookies (sin ASP.NET Identity) | Login por usuario, bloqueo tras 5 intentos, cambio propio de contraseña, recuperación por correo con token de un solo uso (30 min) |
| Base de datos | SQLite (sin ORM) | Casos + usuarios + tokens de recuperación, archivo único portable |
| Directorio de comunas | CSV editable (300+ filas) | Dominio → comuna → correo de contacto; resolución por dirección exacta cuando el dominio es compartido (ej. gmail.com) |
| Extracción de datos | Regex calibrado + reglas de seguridad | Multi-contribuyente por correo, respaldo por asunto (solo RUT), reordenamiento de nombre para el formato Viña |
| Mover/marcar correo | EWS `MoveItem`/`UpdateItem` (nuevo) | Soporta el botón "Marcar subida" de un clic |
| Reporte | CSV regenerado cada ciclo | nombre, rut, comuna, estado, fecha, sector, confirmado |
| Notificaciones | Toast Windows + correo al operador | Solo al confirmar (feedback del envío real) |
| Distribución | `dotnet publish` self-contained single-file | Un .exe (~100 MB) copiable a otro PC sin instalar .NET; tarea programada de Windows para auto-inicio |
| Estadísticas (2026-07-28) | Chart.js (vendorizado, un solo archivo `wwwroot/js/vendor/chart.umd.js`, sin CDN) | Pantalla `/Estadisticas` de solo lectura: agregaciones en memoria (`StatisticsService`) sobre los mismos datos de Casos/F8/Certificado/Descartados, sin cambio de schema |

## 8. Decisiones técnicas relevantes

- **Confirmación explícita, nunca automática por sí sola**: mover el correo solo marca "Subido"; el envío del aviso a la comuna requiere una acción explícita del operador (botón de 2 pasos, o botón de 1 clic), con registro de quién y cuándo. El botón de 1 clic (2026-07-07) fue una decisión consciente de eliminar el paso manual de arrastrar el correo en Outlook, pedida explícitamente por el usuario sabiendo que reduce el control de último minuto que el diseño original tenía a propósito.
- **Detección por directorio, no por patrón**: los dominios municipales chilenos no siguen ningún patrón (`litueche.cl`, `munisanfelipe.cl`, `maho.cl`...); la fuente de verdad es el CSV del directorio.
- **Resolución exacta para dominios compartidos**: cuando un dominio (típicamente gmail.com) es usado por más de una comuna, el sistema exige que la dirección exacta del remitente esté registrada — nunca adivina cuál de las comunas es, para no atribuir mal una confirmación oficial. Se corrigió tras encontrar 4 comunas reales compartiendo gmail.com en el directorio.
- **Extracción anclada al RUT, multi-contribuyente**: sin RUT válido no se inventa nombre. Un mismo correo puede listar varias personas — cada una se registra como su propio caso, con el nombre de cada quien acotado por sus vecinos para que no se mezclen.
- **El nombre del asunto nunca se confía automáticamente**: el RUT del asunto sí se usa como respaldo cuando el cuerpo no trae nada, pero el nombre encontrado ahí siempre exige revisión manual — ver incidente en sección 5.
- **Reordenamiento de nombre acotado a la fuente conocida**: el formato de Viña del Mar (exportación automatizada) siempre trae apellido-apellido-nombre-nombre; se reordena SOLO cuando se detecta ese patrón específico, nunca por adivinar en texto libre (rompería los nombres que ya vienen bien ordenados).
- **Todos los nombres son editables desde el dashboard**, no solo los marcados "Requiere revisión" — porque la extracción es heurística y a veces produce un nombre incompleto o levemente incorrecto sin activar la bandera de revisión.
- **Recuperación de contraseña self-service, no la opción completa por email al inicio**: se evaluaron 3 opciones (cambiar sabiendo la clave actual, recuperar por correo, resetear por CLI) y se implementó primero la más simple; se agregó recuperación por correo completa después, cuando el usuario tuvo fricción real para entrar.
- **Sector derivado de la fecha**: la fecha de última carpeta la digita el operador; el sistema deriva Archivo (< julio 2023) u Oficina 43 (≥ julio 2023).
- **HTTPS obligatorio en la LAN**: contraseñas y RUTs nunca viajan en claro; el puerto HTTP solo redirige.
- **Sin frameworks innecesarios**: sin EF Core, sin ASP.NET Identity, sin framework CSS/JS.

## 9. Incidentes detectados y corregidos durante el desarrollo

- **`.gitignore` mal anclado** *(2026-07-03)*: el patrón `data/*` no protegía carpetas `data/` anidadas; una llegó a contener PII real. Corregido a `**/data/*`.
- **Fuga de credenciales al publicar** *(2026-07-03)*: `appsettings.Development.json` se copiaba al `publish/`. Corregido con `CopyToPublishDirectory="Never"`.
- **Pérdida de datos al mover el repositorio de OneDrive al PC** *(2026-07-06)*: `data/comunas.csv` real (300+ comunas), `data/dev-cert.pfx` y `data/router.db` quedaron fuera (están en `.gitignore`, viven solo en la máquina). Se recuperó `comunas.csv` desde la Papelera de reciclaje; el certificado se regeneró; la base de datos se reconstruyó desde cero (con pérdida de historial de casos previos a esa fecha).
- **`appsettings.json` con rutas de otra máquina** *(2026-07-06)*: el archivo versionado en git tenía rutas absolutas de un PC distinto (`C:/Users/rauls/...`), que habrían hecho fallar el despliegue en producción (certificado y bases de datos inexistentes) de la misma forma que ya había fallado en desarrollo. Corregido a rutas relativas antes de instalar la tarea programada.
- **Checkbox "Marcar" nunca guardaba `true`** *(2026-07-06)*: el patrón hidden+checkbox tenía el campo oculto (`false`) ANTES del checkbox (`true`) en el HTML; ASP.NET Core toma el primer valor recibido cuando hay claves duplicadas, así que siempre llegaba `false`. Se detectó replicando el POST exacto del navegador (con ambos valores juntos, no uno a la vez) y se corrigió invirtiendo el orden.
- **Nombre de correo tomado de una frase instructiva** *(2026-07-07)*: ver sección 5 — el asunto `Fwd: SUBIR CARPETA PLATAFORMA CONASET 14.148.466-4` se registró con ese texto como "nombre" del contribuyente. Corregido de raíz: el nombre del asunto ya nunca se confía, sin importar qué tan razonable parezca.
- **Bug de "bleed" entre contribuyentes en un mismo correo** *(2026-07-06, encontrado durante desarrollo, nunca llegó a producción)*: al implementar la extracción multi-contribuyente con el formato "RUT NOMBRE" repetido, el nombre del primer contribuyente quedaba "libre" para la búsqueda del segundo y se lo robaba. Corregido acotando cada búsqueda por el texto ya consumido por el contribuyente anterior.
- **Migración de esquema sin restricción UNIQUE** *(2026-07-06)*: agregar soporte multi-contribuyente exigió quitar la restricción UNIQUE de `SourceMessageId` (SQLite no soporta esto vía `ALTER TABLE`; hubo que reconstruir la tabla). Se hizo backup manual de `data/router.db` antes de aplicar la migración sobre los datos reales de producción.

## 10. Riesgos vigentes documentados

- **PII en reposo sin cifrado propio**: SQLite y CSV contienen nombres y RUTs en texto plano — depende del cifrado de disco (BitLocker) del equipo.
- **Extracción heurística**: el cuerpo cubre la mayoría de los formatos reales conocidos, pero sigue siendo heurística — por eso todos los nombres (no solo los marcados "Requiere revisión") son editables desde el dashboard.
- **El botón "Marcar subida" no se ha probado en vivo contra un caso real**: hacerlo enviaría una confirmación real a una comuna externa y movería un correo real de producción. Quedó cubierto por tests unitarios con dobles de EWS; la primera prueba en vivo la hace el operador, idealmente reasignando temporalmente el correo de contacto de una comuna a un colega (vía la pantalla "Comunas") antes de probar.
- **Alcance de red**: el dashboard está pensado para la red municipal (LAN) con Firewall abierto solo ahí; no está expuesto a internet ni se recomienda hacerlo dado el tipo de datos que maneja (RUTs y nombres reales de contribuyentes).
- **Historial de casos anterior al 2026-07-06 se perdió** en el incidente de OneDrive (ver sección 9) — no recuperable; el sistema sigue funcionando correctamente hacia adelante.
