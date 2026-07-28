# Flujo del proceso — Cambio de Domicilio / Conaset

Diagrama acordado con el operador (2026-07-03). Fuente de verdad del proceso de negocio.

```
════════════════════════ PROCESO COMPLETO ════════════════════════

 PASO 1 — LLEGA LA SOLICITUD                              [manual]
 ─────────────────────────────────────────────
 Otra municipalidad envía correo: "suba a Conaset la carpeta
 del contribuyente X que se cambió de domicilio"

 PASO 2 — EL OPERADOR CLASIFICA EL CORREO                 [manual]
 ─────────────────────────────────────────────
   Carpeta que hay que subir      → mover a  CARP. PARA PEDIR
   Carpeta ya subida en Conaset   → mover a  CARP. YA SUBIDAS

 PASO 3 — ANÁLISIS Y EXTRACCIÓN                       [automático]
 ─────────────────────────────────────────────
 El sistema revisa CARP. PARA PEDIR cada 30 min y por cada
 correo nuevo de una comuna del directorio extrae:
   • Nombre del contribuyente
   • RUT (validado con dígito verificador)
   • Comuna + mail de la municipalidad solicitante
 Lo guarda en la BASE DE DATOS (estado: PENDIENTE) y lo refleja
 en la planilla (nombre, rut, comuna, última carpeta [vacía]).
 Sin duplicados: una fila por persona+comuna.

 PASO 4 — FECHA DE ÚLTIMA CARPETA                         [manual]
 ─────────────────────────────────────────────
 El operador la consigue en otro servicio y la DIGITA en el
 sistema (celda editable por caso).

 PASO 5 — PDF POR SECTOR                     [automático, a pedido]
 ─────────────────────────────────────────────
 Con la fecha ingresada, el sistema asigna sector:
   • Última carpeta ANTES de julio 2023      → ARCHIVO
   • Última carpeta julio 2023 → hoy         → OFICINA 43
 Botón "Generar PDF por sector": documento con todos los casos
 de ese sector (nombre, rut, comuna, fecha última carpeta),
 listo para llevar a pedir las carpetas físicas.

 PASO 6 — CARPETA FÍSICA                                  [manual]
 ─────────────────────────────────────────────
 Le traen la carpeta → escanea → sube a Conaset
 → mueve el correo a CARP. YA SUBIDAS
 (el sistema lo detecta y marca el caso como SUBIDA,
  pero NO envía nada todavía)

 PASO 7 — CONFIRMACIÓN CON BOTÓN                 [manual + sistema]
 ─────────────────────────────────────────────
 En el programa, el operador ve los casos SUBIDA y aprieta
 "Enviar confirmación": el sistema manda el correo estándar
 a esa municipalidad ("ya se subió la carpeta de [Nombre],
 RUT [X] a Conaset") y marca el caso CONFIRMADO.
 Nada se envía sin ese clic.

 ESTADOS DE UN CASO:
   PENDIENTE → SUBIDA → CONFIRMADO
   (+ marca "Requiere revisión" si faltan datos extraíbles)
```

## Programa en red (dashboard)

El proceso se opera desde una interfaz web accesible en la red municipal
(cambio `add-web-dashboard`), con: login por usuario, celda editable de
fecha de última carpeta, botón de PDF por sector, botón de envío de
confirmación, y la base de datos SQLite detrás.

## Paso 8 — Estadísticas (reporte, no bloquea el flujo)

Capa de solo lectura sobre los mismos datos de los pasos anteriores (Casos,
F8, Certificado, Descartados) — no agrega ningún paso al trámite en sí, solo
lo hace visible de un vistazo: casos por estado, ingresos por semana, top
comunas, tiempo promedio de confirmación, sector Archivo/Oficina 43, plazo
F8 (dentro/vencido de 15 días hábiles), PDFs generados, y correos
descartados por motivo (para priorizar qué comunas faltan en el directorio).
