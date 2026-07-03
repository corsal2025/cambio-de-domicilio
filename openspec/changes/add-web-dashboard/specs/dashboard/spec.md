# Spec: Dashboard

## ADDED Requirements

### Requirement: Per-user authentication
The dashboard SHALL require a per-user login (username + password) before showing any data. Passwords SHALL be stored only as salted hashes.

#### Scenario: Unauthenticated access
- **WHEN** a browser requests any dashboard page without a valid session
- **THEN** it is redirected to the login page and no personal data is served

#### Scenario: Successful login
- **WHEN** a user submits valid credentials
- **THEN** a session cookie is issued and the dashboard is shown

### Requirement: Encrypted transport
The dashboard SHALL be served over HTTPS only; plain HTTP requests SHALL be redirected to HTTPS, never served with data.

#### Scenario: HTTP request redirected
- **WHEN** a browser requests `http://<host>:<port>/...`
- **THEN** the response is a redirect to the equivalent `https://` URL, with no page content or session cookie issued over the plain connection

### Requirement: Case list reflecting the real lifecycle
The dashboard SHALL show tracked cases (`PersonRequest`) with their current status (`Pending`/`Uploaded`/`Confirmed`), filterable by status and by "Requiere revisión."

#### Scenario: Viewing cases
- **WHEN** an authenticated user opens the case list
- **THEN** every tracked case is shown with `full_name`, `rut`, `comuna`, `status`, `fecha_ultima_carpeta` (if set), and `sector` (if derivable)

#### Scenario: Filtering by review status
- **WHEN** the user filters by "Requiere revisión"
- **THEN** only cases with `needs_review = true` are shown

### Requirement: Editable última-carpeta date
The dashboard SHALL let an authenticated user set or change the `fecha_ultima_carpeta` for any case, and the derived `sector` SHALL update immediately for display.

#### Scenario: Operator enters the date
- **WHEN** a user sets `fecha_ultima_carpeta = 2024-01-10` on a case
- **THEN** the case is saved with that date and its displayed sector becomes `Oficina 43`

### Requirement: Operator-triggered confirmation send
The dashboard SHALL provide a "Enviar confirmación" action on cases with `status = Uploaded` and complete data, calling the existing `SendConfirmationAsync`. The action SHALL be unavailable (disabled or hidden) for cases that are not eligible, and the server SHALL enforce the same eligibility regardless of what the page displays.

#### Scenario: Sending a confirmation
- **WHEN** an authenticated user triggers the action on an eligible `Uploaded` case
- **THEN** the confirmation email is sent, the case becomes `Confirmed`, and `confirmed_by_user_id` / `confirmed_at` are recorded for that user and timestamp

#### Scenario: Attempting to confirm a non-eligible case
- **WHEN** the action is attempted on a case that is `Pending`, already `Confirmed`, or missing required data
- **THEN** the server refuses the action and returns the reason, without sending any email

### Requirement: Sector PDF generation
The dashboard SHALL render a print-ready document per sector (Archivo / Oficina 43), containing `full_name`, `rut`, `comuna`, and `fecha_ultima_carpeta` for every case in that sector.

#### Scenario: Generating the Archivo sector document
- **WHEN** the user requests the Archivo sector document
- **THEN** the printed output contains only cases whose derived sector is `Archivo`, with no navigation chrome, ready to print or save as PDF via the browser

### Requirement: Portable distribution
The application SHALL be publishable as a self-contained single-file executable that runs on another Windows PC without a pre-installed .NET runtime.

#### Scenario: Copy to a second PC
- **WHEN** the published executable and its config/data folder are copied to another Windows machine and started
- **THEN** the worker and dashboard run without additional installation steps
