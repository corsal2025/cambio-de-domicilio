# Manual de Usuario Didáctico — Sistema Cambio de Domicilio

**Municipalidad de Valparaíso — Dirección de Tránsito**  
*Autor y Arquitectura:* Raúl Salazar  
*Versión del Sistema:* .NET 10 LTS — Septiembre 2026  

---

## 1. Visión General: ¿Qué hace el sistema y cómo se interconectan sus módulos?

El sistema **Cambio de Domicilio** gestiona integralmente el ciclo de vida de las solicitudes de carpetas de conductores que se han trasladado a otras comunas del país (trámite Conaset). Conecta el servidor de correo municipal (**Exchange Server**), una base de datos local ultra-rápida (**SQLite**), y una interfaz web moderna en red que permite a los operadores controlar cada paso con máxima fluidez y **sin recargas ni saltos molestos de pantalla**.

### Diagrama de Interconexión de Módulos (Mermaid)

```mermaid
flowchart TD
    classDef mail fill:#1e3a8a,stroke:#3b82f6,stroke-width:2px,color:#fff;
    classDef core fill:#065f46,stroke:#10b981,stroke-width:2px,color:#fff;
    classDef ui fill:#1f2937,stroke:#9ca3af,stroke-width:2px,color:#fff;
    classDef storage fill:#7c2d12,stroke:#f97316,stroke-width:2px,color:#fff;
    classDef docs fill:#581c87,stroke:#a855f7,stroke-width:2px,color:#fff;

    subgraph Correo["Buzón Exchange On-Premise"]
        INBOX["Bandeja de Entrada<br/>cambiodedomicilio@munivalpo.cl"]:::mail
        PEDIR["Carpeta Outlook:<br/>'CARP. PARA PEDIR'"]:::mail
        SUBIDAS["Carpeta Outlook:<br/>'CARP. YA SUBIDAS'"]:::mail
        INBOX -->|Clasificación manual| PEDIR
        INBOX -->|Clasificación manual| SUBIDAS
    end

    subgraph Backend["Motor Central (.NET 10 Service)"]
        SYNC["Sincronizador EWS<br/>(Botón 'Sincronizar ahora')"]:::core
        EXTRACT["Extractor Inteligente de Datos<br/>(Nombre, RUT normalizado, Comuna)"]:::core
        SECTOR_CALC["Clasificador de Sector<br/>(&lt; Jul 2023 = Archivo | &ge; Jul 2023 = Oficina 43)"]:::core
        REPORTS["Generador de Reportes CSV"]:::core
    end

    subgraph BaseDatos["Persistencia Local Segura"]
        DB[(Base de Datos SQLite<br/>data/casos.db)]:::storage
        DIR_CSV[(Directorio Comunas<br/>data/comunas.csv)]:::storage
    end

    subgraph ModulosUI["Dashboard Web en Red (Módulos del Sistema)"]
        MOD_CASOS["Módulo 1: Casos Principales<br/>(/Index)"]:::ui
        MOD_F8["Módulo 2: Casos F8<br/>(/F8 - Carpetas no encontradas)"]:::ui
        MOD_CAJA["Módulo 3: Cola y Cajas<br/>(/Caja - Archivo definitivo)"]:::ui
        MOD_SIN["Módulo 4: Sin Carpetas<br/>(/SinCarpetas - Casos cerrados)"]:::ui
        MOD_STATS["Módulo 5: Estadísticas<br/>(/Estadisticas - Métricas y gráficos)"]:::ui
        MOD_COMUNAS["Módulo 6: Directorio Comunas<br/>(/Comunas - Contactos y dominios)"]:::ui
    end

    subgraph Salidas["Documentación e Impresión"]
        PDF_SEC["PDF Sector<br/>(Archivo / Oficina 43)"]:::docs
        PDF_F8["PDF F8<br/>(Listado de Fichas F8)"]:::docs
        LIST_CAJA["Listado Oficial de Caja<br/>(Hoja rotulada numerada)"]:::docs
    end

    PEDIR -->|Lectura SOAP EWS| SYNC
    SUBIDAS -->|Detección de subida| SYNC
    SYNC --> EXTRACT
    DIR_CSV -.->|Validación de dominio remitente| EXTRACT
    EXTRACT -->|Alta de caso en estado Pendiente| DB

    DB <--> MOD_CASOS
    DB <--> MOD_F8
    DB <--> MOD_CAJA
    DB <--> MOD_SIN
    DB --> MOD_STATS
    DIR_CSV <--> MOD_COMUNAS

    MOD_CASOS -->|Edición instantánea fecha| SECTOR_CALC
    MOD_CASOS -->|Traspaso cuando no se halla carpeta| MOD_F8
    MOD_CASOS -->|Carpeta lista para archivar| MOD_CAJA
    MOD_F8 -->|Carpeta física hallada| MOD_CAJA
    MOD_F8 -->|Caso sin carpeta física| MOD_SIN
    MOD_F8 -->|Revertir error F8| MOD_CASOS

    MOD_CASOS -.-> PDF_SEC
    MOD_F8 -.-> PDF_F8
    MOD_CAJA -.-> LIST_CAJA
```

---

## 2. Los 6 Módulos del Sistema en Detalle

### Módulo 1: Cambio de Domicilio (Casos Principales — `/Index`)
Es la pantalla operativa principal donde se reciben y procesan todas las solicitudes normales.

```mermaid
stateDiagram-v2
    [*] --> Pendiente: Correo ingresado desde 'CARP. PARA PEDIR'
    Pendiente --> Subido: Operador mueve correo a 'CARP. YA SUBIDAS' y sincroniza
    Pendiente --> Confirmado: Clic en 'Marcar subida' (Acción directa en 1 paso)
    Subido --> Confirmado: Clic en 'Enviar confirmación' (Envía correo a la comuna)
    Confirmado --> Pendiente: Clic en 'Rectificar' (Si hubo error en confirmación)
    
    Pendiente --> TraspasoF8: Carpeta física no localizada
    TraspasoF8 --> [*]: Pasa a Módulo F8

    Subido --> ColaCaja: Clic en 'Caja'
    Confirmado --> ColaCaja: Clic en 'Caja'
    ColaCaja --> [*]: Pasa a Módulo Caja
```

* **Campos editables en celda (sin recargas de página):**
  * **Nombre y RUT:** Edición en línea directa. Al terminar de editar, se guarda automáticamente con confirmación visual verde (pulso de celda) y sin saltos de scroll.
  * **Fecha última carpeta:** Al digitar la fecha (ej: `15/03/2024`), el sistema la valida inmediatamente en segundo plano y actualiza la columna **Sector** de inmediato.
  * **Casillas de selección (Marcar, F8, Pendiente Carpeta):** Mutuamente excluyentes por fila, se guardan silenciosamente.
* **Acciones principales:**
  * **Marcar subida:** Mueve el correo a `CARP. YA SUBIDAS` y envía la confirmación oficial a la comuna de destino en un solo clic.
  * **Caja:** Envía la carpeta física procesada a la cola de archivado de cajas.
  * **Traspaso a F8:** Deriva el caso a la sección F8 cuando no es posible hallar la carpeta física.

---

### Módulo 2: Casos F8 (`/F8`)
Módulo especializado para gestionar expedientes cuyas carpetas físicas no han podido ser localizadas o fichas anteriores al año 2000.

```mermaid
flowchart LR
    IN_F8["Caso derivado a F8"] --> INPUT["Ingreso de Código F8 y Fecha Penúltima"]
    INPUT --> CHECK{"¿Se encontró la carpeta física?"}
    
    CHECK -->|Sí| CAJA["Botón 'Caja':<br/>Va directo a la cola de Caja"]
    CHECK -->|No / Definitivo| SIN["Botón 'Sin carpeta':<br/>Pasa a Módulo Sin Carpetas"]
    CHECK -->|Fue un error| REV["Botón 'Revertir':<br/>Vuelve a Casos principales"]
    
    INPUT --> PDF["Generar PDF F8 Archivo / Oficina 43"]
```

* **Campos con guardado asíncrono:**
  * **Código F8:** Código asignado por el operador (ej. `F8-1052`). Se guarda en la celda con pulso verde sin mover la página.
  * **Fecha penúltima carpeta o S/C:** Admite fechas válidas o la sigla `S/C` (Sin Carpeta).
* **Acciones:**
  * **Marcar subida / Confirmar:** Envía el comprobante con la referencia F8 a la municipalidad solicitante.
  * **Caja:** Si la carpeta física finalmente aparece, se envía directamente a la cola de cajas.
  * **Sin carpeta:** Cierra el expediente sin carpeta física y lo archiva en el módulo respectivo.
  * **Revertir:** Limpia los datos F8 y regresa el caso al flujo habitual.

---

### Módulo 3: Control y Archivo de Cajas (`/Caja`)
Permite registrar y ordenar físicamente las carpetas que ya fueron subidas y confirmadas en cajas rotuladas permanentes.

```mermaid
flowchart TD
    CASO_SUBIDO["Carpetas derivadas a Caja"] --> COLA["Cola de Carpetas Pendientes de Caja"]
    COLA --> OPERADOR{"Operador revisa carpetas físicas"}
    OPERADOR -->|Selecciona y cierra| CERRAR["Botón 'Cerrar caja con estas carpetas'<br/>Asigna Código (ej: A1-CD)"]
    OPERADOR -->|Equivocación| DEVOLVER["Botón 'Devolver a casos'<br/>Retorna a Casos"]
    CERRAR --> HISTORIAL["Caja Cerrada Oficial"]
    HISTORIAL --> IMPRIMIR["Impresión de Hoja de Ruta Oficial con N° correlativo"]
```

* **Búsqueda transversal:** Cualquier contribuyente archivado en una caja puede localizarse en segundos desde el buscador general, indicando el **Código de Caja**, la **fecha de cierre** y su **número de orden correlativo** exacto dentro del archivador.

---

### Módulo 4: Sin Carpetas (`/SinCarpetas`)
Registro histórico de todos los expedientes que concluyeron su tramitación sin existencia de carpeta física.
* Permite consulta, auditoría y expedición de certificados históricos.
* Casos blindados para evitar que ingresen por error al circuito de cajas físicas.

---

### Módulo 5: Estadísticas Institucionales (`/Estadisticas`)
Panel de reportería ejecutiva y métricas de desempeño del departamento.
* **Tiempos de respuesta:** Cumplimiento del plazo legal de 15 días hábiles.
* **Distribución por Sector:** Volumen derivado a Archivo vs. Oficina 43.
* **Comunas con mayor demanda:** Gráficos comparativos de solicitudes recibidas por municipalidad.
* **Totalmente en tiempo real:** Lee directamente de la base de datos sin retardos.

---

### Módulo 6: Directorio de Comunas (`/Comunas`)
Directorio de contactos oficiales de las más de 300 municipalidades del país.
* Registra comuna, dominio de correo autorizado (ej: `valparaiso.cl`) y casilla electrónica para el envío de confirmaciones.
* Controla qué correos son reconocidos por el sistema para evitar filtración de spam o correos no institucionales a la pantalla de trabajo.

---

## 3. Resumen de Flujo de Trabajo Operativo Recomendado

```mermaid
sequenceDiagram
    autonumber
    actor Op as Operador Municipal
    participant Out as Outlook (Exchange)
    participant Sys as Sistema (Dashboard)
    participant Mun as Municipalidad Solicitante

    Mun->>Out: Envía correo solicitando carpeta de contribuyente
    Op->>Out: Mueve correo a carpeta 'CARP. PARA PEDIR'
    Op->>Sys: Presiona botón 'Sincronizar ahora'
    Sys->>Sys: Extrae Nombre, RUT y Comuna automáticamente
    Sys-->>Op: Muestra el caso en estado PENDIENTE
    Op->>Sys: Escribe la Fecha de última carpeta en la celda
    Sys-->>Op: Guarda en silencio y muestra Sector (Archivo u Oficina 43)
    Op->>Sys: Genera e imprime PDF por Sector para solicitar la carpeta física
    Note over Op: Se escanea la carpeta y se sube a plataforma Conaset
    Op->>Sys: Presiona botón 'Marcar subida'
    Sys->>Out: Mueve correo a 'CARP. YA SUBIDAS'
    Sys->>Mun: Envía correo automático confirmando la subida a Conaset
    Op->>Sys: Presiona botón 'Caja'
    Sys-->>Op: El caso pasa a la cola de archivado de cajas
```

---

## 4. Garantías de Estabilidad y Buenas Prácticas

1. **Sin saltos de página ni pérdidas de scroll:** Cada celda editable (`Fecha`, `Código F8`, `Nombre`, `RUT`) utiliza comunicación AJAX asíncrona. Al presionar Tab o salir de la celda, los datos se guardan silenciosamente y se ofrece una animación visual verde de confirmación inmediata.
2. **Sin correos accidentales:** Ninguna notificación o correo hacia otra municipalidad sale sin que el operador presione expresamente el botón correspondiente.
3. **Respaldo integral:** Los datos se encuentran consolidados en `data/casos.db` respaldados y versionados en el repositorio central de GitHub.
