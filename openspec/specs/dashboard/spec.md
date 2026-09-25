# Spec: Dashboard

## Purpose

The operator-facing web dashboard for the upload-confirmation flow: case list with lifecycle status, manual data entry, confirmation actions, sector PDF generation, comuna directory management, and portable distribution. It runs on the municipal LAN with no login gate — access is controlled at the network layer, not the application.
## Requirements
### Requirement: Encrypted transport
The dashboard SHALL be served over HTTPS only; plain HTTP requests SHALL be redirected to HTTPS, never served with data.

#### Scenario: HTTP request redirected
- **WHEN** a browser requests `http://<host>:<port>/...`
- **THEN** the response is a redirect to the equivalent `https://` URL, with no page content served over the plain connection

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

### Requirement: Editable última-carpeta date
The dashboard SHALL let the operator set or change the `fecha_ultima_carpeta` for any case by typing it as free text — no calendar picker (the operator types faster than navigating a calendar) — in the format "día mes-en-palabras año" (e.g. `15 marzo 2024`, also accepting `15 de marzo de 2024`), case-insensitive. The stored/displayed value SHALL render in the same format, and the derived `sector` SHALL update immediately.

#### Scenario: Operator types the date
- **WHEN** a user types `10 enero 2024` on a case and saves
- **THEN** the case is saved with that date, displayed as `10 enero 2024`, and its sector becomes `Oficina 43`

#### Scenario: Unparseable date rejected
- **WHEN** a user types text that is not a valid Spanish date (e.g. `15 marzzo 2024`)
- **THEN** the change is rejected with a message and the case keeps its previous date

### Requirement: Operator-triggered confirmation send
The dashboard SHALL provide a "Enviar confirmación" action on cases with `status = Uploaded` and complete data, calling the existing `SendConfirmationAsync`. The action SHALL be unavailable (disabled or hidden) for cases that are not eligible, and the server SHALL enforce the same eligibility regardless of what the page displays.

#### Scenario: Sending a confirmation
- **WHEN** the operator triggers the action on an eligible `Uploaded` case
- **THEN** the confirmation email is sent, the case becomes `Confirmed`, and `confirmed_at` is recorded

#### Scenario: Attempting to confirm a non-eligible case
- **WHEN** the action is attempted on a case that is `Pending`, already `Confirmed`, or missing required data
- **THEN** the server refuses the action and returns the reason, without sending any email

### Requirement: Sector PDF generation
The dashboard SHALL render a print-ready document per sector (Archivo / Oficina 43), containing `full_name`, `rut`, `comuna`, and `fecha_ultima_carpeta` for every case in that sector.

#### Scenario: Generating the Archivo sector document
- **WHEN** the user requests the Archivo sector document
- **THEN** the printed output contains only cases whose derived sector is `Archivo`, with no navigation chrome, ready to print or save as PDF via the browser

### Requirement: Manual person-data entry for unextractable cases
For cases flagged `needs_review` (the request's data arrived in an attachment, an empty auto-reply, or a forward the extractor cannot parse), the dashboard SHALL let the operator type in the contributor's full name and RUT. The RUT SHALL be check-digit-validated and normalized like an auto-extracted one; on success the case stops being flagged for review and continues the normal lifecycle.

#### Scenario: Operator completes a case manually
- **WHEN** a user enters a name and a valid RUT on a `needs_review` case and saves
- **THEN** the case stores the normalized RUT and name, `needs_review` becomes false, and the case can now be confirmed like any other

#### Scenario: Invalid RUT rejected
- **WHEN** the entered RUT fails check-digit validation
- **THEN** the change is rejected with a message and the case keeps its review flag

### Requirement: Legal-deadline countdown and alert
Each case SHALL display the date its request email was received and a countdown of the legal upload deadline: 15 business days (Mon–Fri) counted from the received date. The countdown SHALL be visible from day one, escalate visually as the deadline approaches (warning at ≤7 business days, critical at ≤3 or overdue), and stop applying once the case is `Uploaded` or `Confirmed` (the legal duty — uploading — is fulfilled).

#### Scenario: Fresh case shows the countdown
- **WHEN** a request received today is listed
- **THEN** its row shows the received date and "quedan 15 días hábiles" with no alert color

#### Scenario: Deadline approaching
- **WHEN** 3 or fewer business days remain (or the deadline passed) on a case not yet uploaded
- **THEN** the countdown renders as a critical (red) alert; between 4 and 7 remaining it renders as a warning (amber)

#### Scenario: Uploaded case
- **WHEN** a case is `Uploaded` or `Confirmed`
- **THEN** no deadline alert is shown for it

### Requirement: Bounced-confirmation visibility
The dashboard SHALL mark every case whose confirmation email bounced (see the `routing` spec's
"Detect bounced confirmation emails") with a distinct row style and a "REBOTÓ" badge, offer a
filter that shows only those cases, and provide a "Marcar resuelto" action that clears the flag
once the operator has re-sent the confirmation or handled it another way.

#### Scenario: A bounced case stands out
- **WHEN** a `Confirmed` case has been flagged as bounced
- **THEN** its row is styled distinctly, shows a "REBOTÓ" badge, and appears under the "Rebotados" filter

#### Scenario: Operator resolves the bounce
- **WHEN** the operator triggers "Marcar resuelto" on a bounced case
- **THEN** the flag is cleared and the case no longer appears under the "Rebotados" filter

### Requirement: Comuna directory management
The dashboard SHALL provide a directory view listing every comuna (name, contact email, domain) and let the operator correct a comuna's contact email. Changes SHALL persist to the same CSV file the polling cycle reads, so the next confirmation email uses the corrected address.

#### Scenario: Operator corrects a changed email
- **WHEN** a user edits the contact email of a comuna and saves
- **THEN** the CSV directory is updated atomically, and subsequent confirmation sends for that comuna use the new address

#### Scenario: Invalid email rejected
- **WHEN** a user submits a contact value without a valid email shape
- **THEN** the change is rejected with a message and the directory is not modified

### Requirement: Portable distribution
The application SHALL be publishable as a self-contained single-file executable that runs on another Windows PC without a pre-installed .NET runtime.

#### Scenario: Copy to a second PC
- **WHEN** the published executable and its config/data folder are copied to another Windows machine and started
- **THEN** the worker and dashboard run without additional installation steps

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

### Requirement: Frozen identity columns
In Casos and F8 only Nombre and RUT SHALL stay fixed while scrolling horizontally; the Marcado group (Marcar, F8, Pendiente Carpeta) SHALL come right after RUT and scroll with the rest.

#### Scenario: Column order
- **WHEN** the operator opens Casos
- **THEN** the columns start with Nombre, RUT, Marcar, F8, Pendiente Carpeta

### Requirement: Casos keeps list state across actions
After any POST action in Casos, the dashboard SHALL redirect back to the same list state (status filter, needsReview, bounced, search) and SHALL show the action's result message once.

#### Scenario: Search kept after Caja
- **WHEN** the operator searches "17.143.599-4" and presses Caja on a result
- **THEN** the page reloads with search "17.143.599-4" still applied
- **AND** the message "Carpeta de … enviada a Caja" is shown

#### Scenario: Filter kept after checkbox toggle
- **WHEN** the operator is on the "Pendientes" filter and ticks Marcar on a row
- **THEN** the page reloads still filtered by Pendientes

