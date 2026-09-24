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
