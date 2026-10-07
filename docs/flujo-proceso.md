# Flujo del proceso — Cambio de Domicilio / Conaset

Diagrama acordado con el operador (2026-07-03), actualizado a octubre de 2026 con el comportamiento actual del sistema (Subidas a Sistema, Caja, Sin Carpetas). Fuente de verdad del proceso de negocio.

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
 Cuando el operador presiona "Sincronizar ahora" (no hay revisión
 automática periódica), el sistema revisa CARP. PARA PEDIR y por
 cada correo nuevo de una comuna del directorio extrae:
   • Nombre del contribuyente
   • RUT (validado con dígito verificador)
   • Comuna + mail de la municipalidad solicitante
 Lo guarda en la BASE DE DATOS (estado: PENDIENTE, destino: Casos)
 y lo refleja en la planilla (nombre, rut, comuna, última carpeta
 [vacía]). Sin duplicados: una fila por persona+comuna. Los correos
 de dominios desconocidos quedan en Descartados, con su motivo.
 Plazo legal: 15 días hábiles (lunes a viernes, sin descontar
 feriados) desde que llegó el correo.

 PASO 4 — FECHA DE ÚLTIMA CARPETA                         [manual]
 ─────────────────────────────────────────────
 El operador la consigue en otro servicio y la DIGITA en el
 sistema (celda editable por caso).

 PASO 5 — PDF POR SECTOR                     [automático, a pedido]
 ─────────────────────────────────────────────
 Con la fecha ingresada, el sistema asigna sector:
   • Última carpeta ANTES del 1 de julio 2023  → ARCHIVO
   • Última carpeta desde el 1 de julio 2023   → OFICINA 43
   (sin fecha, o con "S/C", no hay sector)
 Botón "Generar PDF por sector": documento con todos los casos
 de ese sector (nombre, rut, comuna, fecha última carpeta),
 listo para llevar a pedir las carpetas físicas.

 PASO 6 — CARPETA FÍSICA                                  [manual]
 ─────────────────────────────────────────────
 Le traen la carpeta → escanea → sube a Conaset
 → mueve el correo a CARP. YA SUBIDAS
 (el sistema lo detecta al sincronizar y marca el caso como
  SUBIDA, pero NO envía nada todavía)
 Atajo de 1 clic: "Marcar subida" mueve el correo él mismo,
 envía la confirmación y deja el caso en CONFIRMADO de una vez.

 PASO 7 — CONFIRMACIÓN CON BOTÓN                 [manual + sistema]
 ─────────────────────────────────────────────
 En el programa, el operador ve los casos SUBIDA y aprieta
 "Enviar confirmación": el sistema manda el correo estándar
 a esa municipalidad ("ya se subió la carpeta de [Nombre],
 RUT [X] a Conaset") y marca el caso CONFIRMADO. El caso pasa a
 la pantalla "Subidas a Sistema" (destino Subidas), donde se
 decide Caja o Sin carpeta. Nada se envía sin ese clic.
 Si se confirmó por error: "Rectificar confirmación" envía un
 correo de rectificación y el caso vuelve a PENDIENTE en Casos.
 Si el correo de confirmación rebota, el caso queda marcado
 "REBOTÓ" hasta que el operador lo marca como resuelto.

 PASO 7b — CARPETA FÍSICA A CAJA                          [manual]
 ─────────────────────────────────────────────
 Con la carpeta ya subida (SUBIDA o CONFIRMADO), el operador
 aprieta "Caja" (en Casos o en Subidas a Sistema): el caso queda
 en la cola de la pantalla Caja, en orden de llegada, hasta que
 se cierra la caja física con "Cerrar Caja" (se asigna un rótulo,
 p. ej. A1-CD, que puede repetirse; cada carpeta recibe su N°).
 En la cola, "Devolver a casos" lo regresa a Casos (por error).
 Una caja cerrada se puede reabrir (sus carpetas vuelven a la
 cola) o se puede quitar una carpeta, que vuelve a Casos.

 Casos en F8 (no se encontró la carpeta):
   • "Caja"        → apareció la carpeta: va directo a la cola
                     de Caja.
   • "Sin carpeta" → se cierra el proceso sin carpeta (también
                     escribiendo S/C como fecha en F8, o desde
                     Subidas a Sistema): el caso pasa a la
                     pantalla "Sin Carpetas", una lista final de
                     solo lectura (sin acciones, sin vuelta atrás,
                     nunca entra a Caja; solo se imprime).
   • "Revertir"    → el F8 se pidió por error: se limpia el estado
                     F8 (esté subido o no) y el caso vuelve a Casos
                     como PENDIENTE "solo Caja", con el botón
                     "Caja" como única acción. Se conservan los
                     datos ya digitados (nombre, RUT, código F8,
                     fecha). No se envía correo.

 ESTADOS DE UN CASO (trámite ante la comuna):
   PENDIENTE → SUBIDA → CONFIRMADO
   (+ marca "Requiere revisión" si faltan datos extraíbles)

 DESTINOS DE UN CASO (en qué pantalla está; eje independiente):
   Casos → F8 | Subidas a Sistema | Caja | Sin Carpetas
   Casos:             caso nuevo, aún en trabajo
   F8:                carpeta no encontrada (Traspaso a F8)
   Subidas a Sistema: ya confirmado, falta decidir Caja o Sin carpeta
   Caja:              cola abierta, luego caja cerrada con rótulo
   Sin Carpetas:      terminal, solo lectura

 COLORES DE FILA EN CASOS:
   blanco = sin trabajar · azul = terminado (subido/confirmado,
   en Caja o cerrado sin carpeta) · amarillo = "Marcar" ·
   morado = "Pendiente carpeta" · gris = casilla F8 marcada ·
   rojo = requiere revisión o la confirmación rebotó
```

## Programa en red (dashboard)

El proceso se opera desde una interfaz web accesible en la red municipal
(cambio `add-web-dashboard`), sin login (el acceso se controla a nivel de
red): celda editable de fecha de última carpeta, botón de PDF por sector,
botón de envío de confirmación, y la base de datos SQLite detrás. El esquema
de la base está versionado (`PRAGMA user_version`) y, antes de aplicar una
migración, se hace un respaldo en línea de la base.

## Paso 8 — Estadísticas (reporte, no bloquea el flujo)

Capa de solo lectura sobre los mismos datos de los pasos anteriores (Casos,
F8, Descartados) — no agrega ningún paso al trámite en sí, solo
lo hace visible de un vistazo: casos por estado, ingresos por semana, top
comunas, tiempo promedio de confirmación, sector Archivo/Oficina 43, plazo
F8 (dentro/vencido de 15 días hábiles), PDFs generados, y correos
descartados por motivo (para priorizar qué comunas faltan en el directorio).
