# Reporte Técnico — OutlookComunaRouter

**Fecha:** 2026-07-02
**Fuente:** consolidado a partir de `openspec/changes/archive/2026-07-02-add-address-change-routing/` y `openspec/changes/add-web-dashboard/`

---

## 1. Qué es el sistema

`OutlookComunaRouter` es un servicio en segundo plano (.NET 10) que automatiza el trámite de "cambio de domicilio" del Municipio de Valparaíso: detecta notificaciones de cambio de domicilio recibidas de otras comunas, solicita automáticamente la última carpeta del contribuyente a la comuna correspondiente, y hace seguimiento hasta que esa comuna responde.

## 2. Estado actual

| Módulo | Estado |
|---|---|
| `add-address-change-routing` (motor de ruteo) | **Completo y archivado** — 37/37 tests, build limpio, sin vulnerabilidades conocidas |
| `add-web-dashboard` (interfaz web) | Propuesta aprobada, 0/25 tareas — pendiente de implementación |

## 3. Arquitectura de acceso al correo (por qué ya no se usa Azure)

**Supuesto inicial (descartado):** se asumió que el buzón `cambiodedomicilio@munivalpo.cl` estaba en Exchange Online (Microsoft 365 nube), lo que habría requerido un registro de aplicación en Azure AD con permisos `Mail.Read`/`Mail.Send` y consentimiento de administrador del tenant — una dependencia externa de TI del municipio.

**Verificación en vivo (2026-07-02):** se comprobó que:
- El buzón **no existe** como identidad de inicio de sesión en Entra ID (Azure AD) — es una implementación híbrida.
- El buzón vive en un **Exchange Server 2016 on-premise** (`mail.munivalpo.cl`), con endpoint EWS (`/EWS/Exchange.asmx`) accesible.
- Las credenciales de Active Directory propias del buzón (`servervalpo\cambiodedomicilio`) autentican correctamente contra ese endpoint vía Basic Auth sobre TLS.

**Consecuencia:** Microsoft Graph no puede alcanzar un buzón on-premise (solo sirve Exchange Online). Se reemplazó la integración por **EWS** (Exchange Web Services), el protocolo SOAP nativo de Exchange. Esto **elimina por completo la dependencia de Azure AD y de TI** — la autenticación es la misma credencial de AD que ya existe para ese buzón.

Implementación: cliente EWS propio sobre `HttpClient` (SOAP crudo), sin paquete de terceros — el paquete oficial de Microsoft es solo para .NET Framework y está sin mantenimiento.

## 4. Flujo funcional completo

```
┌─────────────────────────────────────────────────────────────────┐
│  Cada 30 minutos (configurable), RouterWorker (BackgroundService) │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
   1. Carga el directorio de comunas (CSV: comuna → correo → dominio)
                              │
                              ▼
   2. EWS FindItem + GetItem: lista correos nuevos desde el último ciclo
                              │
                              ▼
   3. Para cada correo:
      a) ¿Dominio del remitente está en el directorio de comunas
         y no es el dominio propio (munivalpo.cl)? → si no, se ignora
      b) Extrae nombre completo y RUT (regex + validación de dígito
         verificador chileno) del cuerpo del correo
      c) ¿Datos incompletos? → queda "pendiente", visible en el
         reporte con columna "Requiere revisión"
      d) ¿Ya existe una solicitud activa (enviada/respondida) para
         ese mismo RUT + comuna? → se vincula, no se reenvía
         (evita duplicados aunque llegue en un correo distinto)
      e) Caso nuevo y válido → envía correo formal solicitando la
         última carpeta (EWS CreateItem) y marca "enviado"
                              │
                              ▼
   4. Para cada correo, además se evalúa si es una RESPUESTA:
      - Coincide el hilo (ConversationId) con una solicitud enviada → respondido
      - Si no, pero el cuerpo contiene el mismo RUT de una solicitud
        enviada desde ese dominio de comuna → respondido (fallback)
      - Al marcar "respondido": notifica por pantalla (Windows toast)
        + correo (siempre, funciona igual en PC o en un futuro VPS)
                              │
                              ▼
   5. Reescribe el reporte CSV (data/reporte.csv) con el estado de
      todas las personas: nombre, RUT, comuna, estado, fecha de
      última carpeta, y si requiere revisión manual
```

## 5. Modelo de datos (SQLite, sin ORM)

**`PersonRequest`** — una fila por notificación de cambio de domicilio detectada:
- Identificación de la persona: `full_name`, `rut` (normalizado, con dígito verificador validado), `comuna`
- Trazabilidad del correo origen: `InternetMessageId` (clave de idempotencia — no cambia si el correo se mueve de carpeta, a diferencia del ID de EWS), `ConversationId`, asunto, remitente
- Ciclo de vida: `status` (`pending` → `sent` → `responded`), timestamps y IDs de los correos de solicitud y respuesta
- `needs_review`: marca los casos que no se pudieron procesar automáticamente

**`ComunaContact`** — directorio importado desde CSV: comuna, correo de contacto, dominio (usado tanto para detectar notificaciones entrantes como para reconocer respuestas).

## 6. Decisiones técnicas relevantes y por qué

| Decisión | Alternativa descartada | Razón |
|---|---|---|
| Worker Service con sondeo cada 30 min | Tarea batch una vez al día | El usuario necesita enterarse de respuestas el mismo día, no al día siguiente |
| Detección de comuna por directorio propio | Adivinar patrón `muni<comuna>.cl` en el dominio | El patrón no es universal (nombres de comuna con espacios/tildes); el directorio real es la única fuente confiable |
| EWS con cliente SOAP propio | Microsoft Graph / paquete `Microsoft.Exchange.WebServices` | Graph no llega a buzones on-prem; el paquete oficial de EWS es .NET Framework-only y sin mantenimiento |
| Notificación de respuesta por dos canales (toast + correo) | Solo reporte CSV del día siguiente | El correo funciona igual en el PC actual y en el futuro VPS headless; el toast es best-effort y no bloquea el ciclo si falla |
| Notificación toast sin paquete NuGet (llamada nativa a PowerShell/WinRT) | `Microsoft.Toolkit.Uwp.Notifications` | Ese paquete traía una vulnerabilidad **crítica** (`System.Drawing.Common` 4.7.0) y forzaba el proyecto a un TFM exclusivo de Windows, incompatible con la migración futura a VPS Linux |
| Retry con backoff exponencial en llamadas EWS/Graph | Sin reintento | Evita que fallas transitorias (5xx, throttling) tumben un ciclo completo de sondeo |
| Deduplicación por (RUT, comuna) además de por ID de correo | Solo deduplicar por ID de correo | Un mismo trámite puede llegar notificado en dos correos distintos (reenvío); sin esto se enviarían solicitudes duplicadas a la misma comuna |

## 7. Riesgos documentados (no resueltos en código, a tener presente)

- **PII en reposo sin cifrado propio**: la base SQLite y el CSV de reporte contienen nombres y RUTs en texto plano en el disco del equipo. Depende de que el cifrado de disco (BitLocker) del equipo esté activo — la aplicación no agrega su propia capa de cifrado.
- **Matching por RUT-fallback es heurístico**: si una comuna responde en un hilo nuevo (no como "Responder"), se hace match por RUT — teóricamente podría haber una coincidencia errónea, mitigado porque toda respuesta queda para verificación manual antes de continuar el trámite.
- **Alcance de red al migrar a VPS**: el endpoint EWS está verificado como accesible dentro de la red municipal; su alcance desde un VPS externo aún no está verificado — podría requerir VPN/túnel si el firewall del municipio bloquea el acceso externo.

## 8. Lo que viene (`add-web-dashboard`, no implementado aún)

Interfaz web embebida en el mismo proceso (ASP.NET Core + Kestrel) para que varios funcionarios vean el estado en tiempo real desde el navegador, con:
- Login por usuario (obligatorio — hay datos personales de por medio)
- **HTTPS obligatorio** (se corrigió en la propuesta: HTTP plano exponía contraseñas y RUTs sin cifrar en la red)
- Clasificación supervisada de correos entrantes/salientes, con **auditoría de quién reclasificó qué y cuándo** (se agregó en la revisión de la propuesta)
- Vistas separadas de solicitudes enviadas vs. recibidas, documento imprimible
- Publicación como ejecutable único portable (copiar a otro PC sin instalar el runtime de .NET)

Detalle completo en `openspec/changes/add-web-dashboard/`.
