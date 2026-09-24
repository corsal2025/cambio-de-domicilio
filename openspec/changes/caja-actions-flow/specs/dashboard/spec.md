## MODIFIED Requirements

### Requirement: Case list reflecting the real lifecycle
The dashboard SHALL show tracked cases (`PersonRequest`) with their current status (`Pending`/`Uploaded`/`Confirmed`), filterable by status and by "Requiere revisión." Cases transferred to another screen (`TransferredAt` set, e.g. F8 or Caja) SHALL NOT appear in Casos. Cases closed without folder SHALL appear with the label "Cerrado sin carpeta" and no actions. The action column SHALL show only the actions valid for the case state, rendered as icon buttons with a tooltip and accessible label.

#### Scenario: Viewing cases
- **WHEN** the operator opens the case list
- **THEN** every tracked case not transferred to another screen is shown with `full_name`, `rut`, `comuna`, `status`, `fecha_ultima_carpeta` (if set), and `sector` (if derivable)

#### Scenario: Filtering by review status
- **WHEN** the user filters by "Requiere revisión"
- **THEN** only cases with `needs_review = true` are shown

#### Scenario: Case in Caja hidden from Casos
- **WHEN** a case has `Destination = Caja`
- **THEN** it is not shown in Casos

#### Scenario: Closed-without-folder case
- **WHEN** a case has `ClosedWithoutFolderAt` set
- **THEN** it is shown in Casos with the label "Cerrado sin carpeta" and no action buttons except delete

#### Scenario: Uploaded case actions
- **WHEN** a case has `Status` `Uploaded` or `Confirmed` and is not closed without folder
- **THEN** its action column includes the "Caja" action

#### Scenario: Reincorporated F8 case actions
- **WHEN** a case has `SoloCaja = true`
- **THEN** its action column shows only the "Caja" action (and delete), never "Marcar subida" nor confirmation actions

#### Scenario: Action button presentation
- **WHEN** any action button is rendered in Casos, F8 or Caja
- **THEN** it shows an inline SVG icon, a color by action type (upload blue, Caja green, Sin carpeta amber, delete red on hover), and a `title` and `aria-label`

## ADDED Requirements

### Requirement: Readable sector names
Every place that displays a folder sector (dashboard tables, filters, banners, and printed PDFs) SHALL show `Oficina 43` (with a space) and `Archivo`, never the raw enum name `Oficina43`.

#### Scenario: Sector shown in Casos
- **WHEN** a case has última carpeta from July 2023 onward
- **THEN** its sector cell reads "Oficina 43"

#### Scenario: Sector in printed document
- **WHEN** the operator prints the Oficina 43 sector PDF
- **THEN** the title and rows read "Oficina 43"

### Requirement: Icon navigation buttons
The header and sub-navigation buttons of every dashboard page (status filters, systems, documents, search, sync, back links) SHALL show an inline SVG icon next to their label, with a consistent pill style, hover state and focus ring.

#### Scenario: Header buttons have icons
- **WHEN** the operator opens Casos, F8, Caja, Estadísticas or Comunas
- **THEN** every navigation button shows an icon and its label

### Requirement: Auto-fit table columns
Every table column in the dashboard (Casos, F8, Caja, sector documents) SHALL size to its content so no text is clipped or ellipsized; wide tables SHALL scroll horizontally inside their card.

#### Scenario: Long name fully visible
- **WHEN** a case has a 40-character full name
- **THEN** the Nombre column shows the whole name without "…"

#### Scenario: Traspaso a F8 only when F8 is ticked
- **WHEN** a Pending case has its F8 checkbox unticked
- **THEN** its action column shows "Marcar subida" and not "Traspaso a F8"
