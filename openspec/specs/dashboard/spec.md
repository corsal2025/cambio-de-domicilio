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
The dashboard SHALL show tracked cases (`PersonRequest`) with their current status (`Pending`/`Uploaded`/`Confirmed`), filterable by status and by "Requiere revisión."

#### Scenario: Viewing cases
- **WHEN** the operator opens the case list
- **THEN** every tracked case is shown with `full_name`, `rut`, `comuna`, `status`, `fecha_ultima_carpeta` (if set), and `sector` (if derivable)

#### Scenario: Filtering by review status
- **WHEN** the user filters by "Requiere revisión"
- **THEN** only cases with `needs_review = true` are shown

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
