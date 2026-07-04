# Reporte Técnico — OutlookComunaRouter

**Última actualización:** 2026-07-03
**Fuente:** consolidado desde los artefactos OpenSpec del proyecto (`openspec/specs/`, `openspec/changes/`) — la metodología del proyecto exige que toda decisión quede documentada ahí antes de implementarse.

---

## 1. Qué es el sistema

`OutlookComunaRouter` es un servicio .NET 10 con dashboard web integrado que automatiza el trámite de solicitudes de carpeta de contribuyentes que **otras comunas le piden a Valparaíso** (ligado a Conaset): detecta las solicitudes que llegan por correo, extrae los datos de la persona, acompaña el trabajo manual del operador (buscar/escanear/subir la carpeta), y envía el aviso de "carpeta subida" a la comuna solicitante cuando el operador lo decide.

## 2. Estado de los cambios (OpenSpec)

| Cambio | Estado |
|---|---|
| `add-address-change-routing` | Archivado (diseño original, superado — asumía el flujo en dirección inversa) |
| `add-folder-based-triggering` | 10/11 — lectura por carpeta con nombre, sin filtro de fecha |
| `add-upload-confirmation-flow` | Flujo vigente: Pendiente → Subido → Confirmado con botón manual |
| `add-web-dashboard` | Dashboard web: login, lista de casos, fecha editable, botón de confirmación, PDF por sector, directorio de comunas editable, diseño institucional |

## 3. Diagrama de flujo general del sistema

```
                        ┌─────────────────────────────┐
                        │   Otra municipalidad envía   │
                        │   correo pidiendo carpeta    │
                        │   de un contribuyente        │
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
        ┌──────────────────────────────────────────────────────┐
        │              SERVICIO (RouterWorker)                  │
        │                                                       │
        │  ┌────────────┐   ┌────────────┐   ┌──────────────┐  │
        │  │ ¿Dominio    │──►│ Extraer    │──►│ ¿Duplicado    │  │
        │  │ de comuna   │no │ nombre+RUT │   │ (RUT+comuna)? │  │
        │  │ conocida?   │─┐ │ del cuerpo │   └──────┬───────┘  │
        │  └────────────┘ │ └─────┬──────┘          │no         │
        │                 │       │¿falló?          ▼           │
        │            (se ignora)  ▼            ┌──────────┐    │
        │                 ┌──────────────┐     │ Registrar │    │
        │                 │ PENDIENTE +  │     │ PENDIENTE │    │
        │                 │ "Requiere    │     └──────────┘    │
        │                 │  revisión"   │                      │
        │                 └──────────────┘                      │
        └──────────────────────────────┬───────────────────────┘
                                       │
                                       ▼
                        ┌─────────────────────────────┐
                        │   DASHBOARD WEB (HTTPS)      │
                        │   https://<pc>:5001          │
                        │   - login por usuario        │
                        │   - lista de casos           │
                        │   - fecha última carpeta ────┼──► deriva SECTOR:
                        │     (la digita el operador)  │    < jul 2023 → Archivo
                        │   - PDF por sector           │    ≥ jul 2023 → Oficina 43
                        │   - directorio de comunas    │
                        └─────────────────────────────┘
```

## 4. Diagrama del ciclo de vida de un caso

```
   correo llega a                  operador sube la          operador aprieta
   CARP. PARA PEDIR                carpeta a Conaset y       "Enviar confirmación"
        │                          mueve el correo a         en el dashboard
        │                          CARP. YA SUBIDAS               │
        ▼                               │                         ▼
  ┌───────────┐   detección EWS   ┌────▼──────┐  botón   ┌──────────────┐
  │ PENDIENTE │ ────────────────► │  SUBIDO   │ ───────► │  CONFIRMADO  │
  └───────────┘   (sin enviar     └───────────┘  (envía  └──────────────┘
        │          nada)                          correo a │
        │                                         la comuna│ queda registrado:
        ▼                                         + avisa  │ QUIÉN lo confirmó
  "Requiere revisión"                             al       │ y CUÁNDO
  si faltan nombre/RUT                            operador)│
  (el operador los ve                                      
   filtrados en el dashboard)                              

  REGLAS CLAVE:
  • Mover el correo a CARP. YA SUBIDAS NO envía nada — solo marca "Subido".
  • El correo a la comuna sale ÚNICAMENTE con el clic del operador.
  • Un caso Pendiente o ya Confirmado no puede confirmarse (el servidor lo rechaza
    aunque la página esté desactualizada).
  • Nunca hay dos filas para la misma persona+comuna (deduplicación).
```

## 5. Diagrama del flujo de extracción de datos (calibrado con correos reales)

```
   Cuerpo del correo
        │
        ▼
   ¿Tiene el aviso "CORREO EXTERNO" de Exchange?
        │ sí → se elimina antes de extraer (antes contaminaba
        │      todos los nombres extraídos)
        ▼
   Buscar RUT, en este orden de prioridad:
     1. Con prefijo:  RUT: / R.U.T. / RUN / R.U.N.   (16 de 38 reales)
     2. Pelado con puntos:  12.345.678-9              ( 9 de 38 reales)
     3. Pelado sin puntos:  12345678-9                ( 3 de 38 reales)
   Siempre validado con el dígito verificador chileno.
        │
        ├── no hay RUT válido (10 de 38: adjuntos, reenvíos vacíos)
        │        └──► caso queda "Requiere revisión" — sin inventar nombres
        ▼
   Buscar el NOMBRE en la ventana de texto pegada al RUT
   (antes del RUT; si no, después), quitando don/doña/Sr./Sra.
   — ÚNICA señal confiable: los correos reales no traen "Nombre:"
        │
        ▼
   Caso registrado con nombre + RUT normalizado (12.345.678-9)
```

## 6. Arquitectura de acceso al correo (por qué no se usa Azure)

El buzón `cambiodedomicilio@munivalpo.cl` vive en un **Exchange Server 2016 on-premise** (`mail.munivalpo.cl`), no en Exchange Online — verificado en vivo el 2026-07-02. Microsoft Graph no puede alcanzar buzones on-premise, así que la integración es **EWS** (SOAP) con las credenciales de Active Directory del propio buzón. **No se necesita Azure AD, ni registro de aplicación, ni aprobación de TI.** El cliente EWS es propio (SOAP crudo sobre `HttpClient`), porque el paquete oficial de Microsoft es solo .NET Framework y está abandonado.

## 7. Componentes técnicos

| Componente | Tecnología | Rol |
|---|---|---|
| Servicio de sondeo | .NET 10 `BackgroundService` | Lee ambas carpetas Outlook cada 30 min vía EWS |
| Dashboard | ASP.NET Core Razor Pages (mismo proceso) | UI del operador, HTTPS-only en puerto 5001 |
| Autenticación | PBKDF2 + cookies (sin ASP.NET Identity) | Login por usuario, bloqueo tras 5 intentos fallidos |
| Base de datos | SQLite (sin ORM) | Casos + usuarios, archivo único portable |
| Directorio de comunas | CSV editable (263 comunas) | Dominio → comuna → correo de contacto; editable desde el dashboard |
| Reporte | CSV regenerado cada ciclo | nombre, rut, comuna, estado, fecha, sector, confirmado |
| Notificaciones | Toast Windows + correo al operador | Solo al confirmar (feedback del envío real) |
| Distribución | `dotnet publish` self-contained single-file | Un .exe (~100 MB) copiable a otro PC sin instalar .NET |

## 8. Decisiones técnicas relevantes (resumen; detalle en cada `design.md`)

- **Confirmación por botón, nunca automática**: mover el correo de carpeta solo marca "Subido"; el envío del aviso a la comuna requiere el clic explícito del operador, con registro de quién y cuándo.
- **Detección por directorio, no por patrón**: los dominios municipales chilenos no siguen ningún patrón (`litueche.cl`, `munisanfelipe.cl`, `maho.cl`...); la fuente de verdad es el CSV de 263 comunas.
- **Extracción anclada al RUT**: calibrada contra los 38 correos reales de producción; sin RUT válido no se inventa nombre (queda para revisión manual).
- **Sector derivado de la fecha**: la fecha de última carpeta la digita el operador (no viene en los correos); el sistema deriva Archivo (< julio 2023) u Oficina 43 (≥ julio 2023) y genera el listado imprimible por sector.
- **HTTPS obligatorio en la LAN**: contraseñas y RUTs nunca viajan en claro; el puerto HTTP solo redirige.
- **Sin frameworks innecesarios**: sin EF Core, sin ASP.NET Identity, sin framework CSS/JS — la superficie del sistema no los justifica.

## 9. Incidentes de seguridad detectados y corregidos durante el desarrollo

- **`.gitignore` mal anclado**: el patrón `data/*` no protegía carpetas `data/` anidadas; una llegó a contener PII real. Corregido a `**/data/*`, carpeta eliminada, nunca llegó a git.
- **Fuga de credenciales al publicar**: `appsettings.Development.json` (contraseña EWS real) se copiaba al `publish/`. Corregido con `CopyToPublishDirectory="Never"`.

## 10. Riesgos vigentes documentados

- **PII en reposo sin cifrado propio**: SQLite y CSV contienen nombres y RUTs en texto plano — depende del cifrado de disco (BitLocker) del equipo.
- **Extracción heurística**: ~26% de los correos reales no traen el RUT en el cuerpo (viene en adjuntos) — esos casos quedan siempre en "Requiere revisión" para gestión manual, por diseño.
- **Alcance de red del VPS futuro**: el endpoint EWS no está verificado desde fuera de la red municipal.
