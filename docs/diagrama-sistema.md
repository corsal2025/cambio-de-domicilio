# Diagrama del sistema — CambioDeDomicilio

**Última actualización:** 2026-10-07
**Propósito:** referencia rápida para reportar el flujo del sistema (jefatura, auditoría, onboarding). Refleja el comportamiento real del código a esta fecha, no el diseño original.

> Nota de vigencia: el sondeo de correo **ya no es automático cada 30 min** — corre solo cuando el operador presiona "Sincronizar ahora" en el dashboard (`RouterWorker.RunCycleAsync`, disparado por `IndexModel.OnPostSyncNowAsync`). Otros documentos del proyecto (`reporte-tecnico.md`) todavía describen el sondeo periódico viejo; este diagrama es la versión actualizada.

## 1. Vista general del sistema

```mermaid
flowchart TB
    subgraph Origen["Origen del correo"]
        M1["Otra comuna pide<br/>carpeta de un contribuyente"]
        M2["Buzón Exchange on-premise<br/>cambiodedomicilio@munivalpo.cl"]
        M1 --> M2
    end

    M2 -->|"operador clasifica<br/>manualmente"| F1["Carpeta Outlook:<br/>CARP. PARA PEDIR"]
    M2 -->|"operador clasifica<br/>manualmente"| F2["Carpeta Outlook:<br/>CARP. YA SUBIDAS"]

    F1 -->|"Sincronizar ahora<br/>(botón, manual)"| RW
    F2 -->|"Sincronizar ahora<br/>(botón, manual)"| RW

    subgraph RW["RouterWorker.RunCycleAsync"]
        direction TB
        D{"¿Dominio del<br/>remitente conocido<br/>en el directorio<br/>de comunas?"}
        D -->|no| DISC["Descartado<br/>(pantalla Discarded)"]
        D -->|sí| EX["Extraer TODOS los<br/>contribuyentes del<br/>cuerpo (y asunto<br/>como respaldo)"]
        EX --> DUP{"¿Duplicado<br/>RUT + comuna?"}
        DUP -->|sí, se ignora| SKIP["No se registra de nuevo"]
        DUP -->|no| INS["Insertar caso<br/>Status = Pending"]
        UP["Correo en CARP. YA SUBIDAS<br/>+ Message-ID conocido"] --> SETUP["Caso pasa a<br/>Status = Uploaded"]
        NDR["Rebote en Bandeja de entrada<br/>(postmaster + menciona Conaset)"] --> BNC["Caso Confirmado<br/>marcado 'REBOTÓ'<br/>(por RUT del NDR)"]
    end

    INS --> CSV["Reporte CSV<br/>regenerado cada ciclo"]
    SETUP --> CSV

    INS --> DASH
    SETUP --> DASH

    subgraph DASH["Dashboard web (HTTPS, sin login — acceso por red)"]
        CASOS["/Index — Casos<br/>(Cambio de Domicilio)"]
        F8P["/F8 — Casos F8"]
        SUBP["/SubidasASistema"]
        CAJAP["/Caja — cola y cajas cerradas"]
        SINP["/SinCarpetas — lista final, solo lectura"]
        DISCP["/Discarded"]
        COMU["/Comunas"]
    end

    CASOS -->|"Traspaso a F8<br/>(TransferredAt, Destination=F8)"| F8P
    CASOS -->|"Marcar subida / Enviar confirmación<br/>(Destination=Subidas)"| SUBP
    SUBP -->|"Caja"| CAJAP
    F8P -->|"Caja"| CAJAP
    F8P -->|"Sin carpeta"| SINP
    SUBP -->|"Sin carpeta"| SINP
    DISC --> DISCP
```
## 2. Ciclo de vida de un caso (Casos / Index)

```mermaid
stateDiagram-v2
    [*] --> Pending: correo llega a<br/>CARP. PARA PEDIR

    Pending --> Uploaded: Camino A (manual) — operador mueve<br/>el correo a CARP. YA SUBIDAS,<br/>el sistema lo detecta al sincronizar
    Pending --> Confirmed: Camino B (1 clic) — botón "Marcar subida":<br/>mueve el correo él mismo + confirma de inmediato

    Uploaded --> Confirmed: botón "Enviar confirmación"<br/>(envía correo real a la comuna,<br/>requiere confirmación de diálogo JS)
    Uploaded --> Pending: el correo reaparece en CARP. PARA PEDIR<br/>(se deshace la subida; nunca toca Confirmed)

    Confirmed --> Pending: "Rectificar confirmación"<br/>(envía correo de rectificación<br/>y limpia UploadedAt/ConfirmedAt/rebote)

    note right of Confirmed
        Reglas clave:
        - Mover el correo a CARP. YA SUBIDAS
          NO envía nada por sí solo.
        - El correo a la comuna sale SOLO
          con acción explícita del operador.
        - Nunca dos filas para la misma
          persona+comuna (deduplicación).
        - El Estado (esta vista) es independiente
          del Destino (siguiente vista).
    end note
```

### Destinos del caso (eje independiente del estado)

`PersonRequest.Destination` indica en qué pantalla está el caso. Valores: `None` (Casos), `F8`, `Subidas`, `Caja`, `SinCarpetas`.

```mermaid
stateDiagram-v2
    [*] --> Casos: correo extraído<br/>(Destination = None)

    Casos --> Subidas: "Marcar subida" o "Enviar confirmación"<br/>(Destination = Subidas)
    Casos --> F8: "Traspaso a F8" (requiere casilla F8)
    Casos --> Caja: "Caja" (Subido/Confirmado o Solo Caja)

    Subidas --> Casos: "Rectificar confirmación"
    Subidas --> Caja: "Caja"
    Subidas --> SinCarpetas: "Sin carpeta"

    F8 --> Caja: "Caja" (apareció la carpeta)
    F8 --> SinCarpetas: "Sin carpeta" o fecha "S/C"
    F8 --> Casos: "Revertir" (Pending + SoloCaja,<br/>solo acción Caja)

    Caja --> Casos: "Devolver a casos" (cola abierta)<br/>o "Quitar de caja" (caja cerrada)
    Caja --> Caja: "Cerrar Caja" (asigna BoxId)<br/>"Reabrir caja" (BoxId = NULL)

    SinCarpetas --> [*]: terminal, solo lectura<br/>(sin acciones ni vuelta atrás)

    note right of SinCarpetas
        Un caso con ClosedWithoutFolderAt
        nunca entra a Caja. Ninguna de
        estas transiciones envía correo,
        salvo Marcar subida / Confirmar
        y Rectificar.
    end note
```

**Colores de fila en Casos:** blanco = sin trabajar; azul = terminado (`IsActionCompleted`: Subido/Confirmado, en Caja o cerrado sin carpeta); amarillo = "Marcar"; morado = "Pendiente carpeta"; gris pizarra = casilla F8 marcada; rojo/rosado = requiere revisión o la confirmación rebotó (REBOTÓ).

## 3. Extracción de datos de un correo

```mermaid
flowchart TB
    A["Cuerpo del correo"] --> B{"¿Trae aviso<br/>'CORREO EXTERNO'<br/>de Exchange?"}
    B -->|sí| C["Se elimina antes<br/>de extraer"]
    B -->|no| D
    C --> D["Buscar TODOS los RUT del cuerpo<br/>(prioridad: con prefijo → con puntos<br/>→ sin puntos → formato Viña separado)"]
    D --> E{"¿Encontró<br/>algún RUT<br/>válido?"}
    E -->|no| F["Probar el ASUNTO<br/>(mismo algoritmo,<br/>solo el RUT, NUNCA el nombre)"]
    F --> G{"¿RUT en<br/>el asunto?"}
    G -->|no| H["1 caso 'Requiere revisión'<br/>sin nombre ni RUT"]
    G -->|sí| I
    E -->|sí, uno o varios| I["Por cada RUT: buscar el nombre<br/>en la ventana de texto pegada a<br/>ESE RUT, acotada por los RUT vecinos"]
    I --> J{"¿RUT formato Viña<br/>Y nombre de<br/>4 palabras?"}
    J -->|sí| K["Reordenar:<br/>APELLIDO APELLIDO NOMBRE NOMBRE<br/>→ NOMBRE NOMBRE APELLIDO APELLIDO"]
    J -->|no| L["Nombre queda como está"]
    K --> M["Caso registrado:<br/>nombre + RUT normalizado"]
    L --> M
```

**Por qué el nombre del asunto nunca se confía:** un correo real con asunto `Fwd: SUBIR CARPETA PLATAFORMA CONASET 14.148.466-4` (una instrucción, no un nombre) se registró una vez como si ese texto fuera el nombre del contribuyente. Desde entonces, el RUT del asunto se acepta como respaldo pero el nombre del asunto **jamás** — el caso siempre queda "Requiere revisión".

## 4. F8 — pista separada después del traspaso

```mermaid
flowchart LR
    CASOS["/Index — Casos"] -->|"Traspaso a F8"| F8["/F8"]

    subgraph F8flow["F8"]
        F8 --> F8sector{"Fecha última<br/>carpeta (S/C admitido)"}
        F8sector -->|"< 1 jul 2023"| ARCH["Sector: Archivo"]
        F8sector -->|"≥ 1 jul 2023"| OF43["Sector: Oficina 43"]
        ARCH --> MARCA["Operador marca casos<br/>(orden = orden de marcado)"]
        OF43 --> MARCA
        MARCA --> PDFARCH["PDF Archivo"]
        MARCA --> PDFOF43["PDF Oficina 43"]
    end
```

## 5. Componentes técnicos

```mermaid
flowchart TB
    subgraph Proceso[".NET 10 — un solo proceso"]
        BG["BackgroundService<br/>(RouterWorker)<br/>ExecuteAsync no hace nada;<br/>el ciclo corre solo con<br/>'Sincronizar ahora'"]
        RP["ASP.NET Core Razor Pages<br/>Dashboard HTTPS :5001"]
    end
    EWS["Cliente EWS propio (SOAP)<br/>Exchange Server 2016 on-premise"]
    SQLITE["SQLite (sin ORM)<br/>PersonRequest, Box, DiscardedEmail,<br/>DeletedSourceMessage, ProcessedBounce<br/>PRAGMA user_version + migraciones"]
    MIG["SchemaMigrator<br/>V001_Baseline, V002_CaseListIndex<br/>respaldo en línea previo"]
    CSVDIR["CSV editable<br/>directorio de comunas<br/>(300+ filas)"]
    CSVOUT["CSV de reporte<br/>regenerado cada ciclo"]

    RP -->|"Sincronizar ahora"| BG
    BG <--> EWS
    MIG -->|"al arrancar, antes de atender"| SQLITE
    BG <--> SQLITE
    BG --> CSVDIR
    BG --> CSVOUT
    RP <--> SQLITE
```

La capa de persistencia está dividida por responsabilidad: `PersonRequestRepository` (casos, con filtros del lado SQL mediante `CaseQuery`), `BoxRepository` (cajas cerradas), `MessageTombstoneRepository` (correos ya tratados), `DiscardedEmailRepository` y `PersonRequestMapper` (mapeo de fila a entidad compartido). Antes de la primera migración pendiente se escribe un respaldo en línea `<base>.bak-v<versión>-<fecha>` (máximo 3 por versión).

## 6. Estadísticas — pantalla de solo lectura (2026-07-28)

```mermaid
flowchart TB
    SQLITE["SQLite<br/>(PersonRequest + DiscardedEmail)"] --> SVC["StatisticsService<br/>(agregación en memoria, LINQ)"]
    SVC --> EST["/Estadisticas<br/>(EstadisticasModel.OnGet)"]
    EST --> CHARTS["Chart.js vendorizado<br/>(sin CDN, un solo archivo)"]

    subgraph Metricas["Métricas por proceso"]
        M1["Casos: estado, ingresos por semana,<br/>top comunas, turnaround promedio,<br/>sector Archivo/Oficina43"]
        M2["F8: plazo 15 días hábiles<br/>(dentro/vencido), PDFs generados"]
        M4["Descartados: por motivo/dominio<br/>no reconocido"]
    end

    CHARTS --> Metricas
```

No agrega ningún paso al trámite — es una capa de reporte sobre datos que los otros procesos ya generan, sin escritura y sin cambio de schema.
