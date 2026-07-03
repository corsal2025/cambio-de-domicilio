# Spec: Routing (delta)

## REMOVED Requirements

### Requirement: Resolve comuna contact and send folder request
Reason: the direction of the request was backwards — Valparaíso does not ask other comunas for folders; other comunas ask Valparaíso. Superseded by "Detect incoming folder request" below.

### Requirement: Detect and match comuna replies
Reason: there is no outbound request to receive a reply to. Superseded by "Detect upload signal" below.

### Requirement: Prevent duplicate requests for the same person and comuna
Reason: replaced by the simpler "any existing record blocks re-insertion" rule under "Detect incoming folder request."

### Requirement: Detect address-change notifications
Reason: superseded by "Detect incoming folder request" below, which reads a specific named folder instead of scanning generically, and does not send any outbound email.

## MODIFIED Requirements

### Requirement: Continuously-refreshed tracking report
The system SHALL rewrite a CSV report to a fixed local file path on every polling cycle. The report SHALL include a `Requiere revisión` column, the manually-entered `fecha_ultima_carpeta`, the derived `sector` (Archivo for dates before July 2023, Oficina 43 from July 2023 onwards), and `confirmed_at`.

#### Scenario: Report refreshed each cycle
- **WHEN** a polling cycle completes
- **THEN** the CSV file at the configured path is rewritten with one row per tracked case, containing `full_name`, `rut`, `comuna`, `status`, `fecha_ultima_carpeta`, `sector`, `confirmed_at`, and `Requiere revisión` — with no duplicate rows per person+comuna

## ADDED Requirements

### Requirement: Detect incoming folder request
The system SHALL scan the configured source folder ("CARP. PARA PEDIR") on each polling cycle for emails whose sender domain matches a known comuna domain, extract the contributor's `full_name` and `rut`, and record the requesting comuna — without sending any email in response.

#### Scenario: New request detected
- **WHEN** an email from a known comuna domain appears in the source folder and has not been processed before (by `InternetMessageId`)
- **THEN** a record is created with `status = Pending`, no outbound email is sent

#### Scenario: Duplicate request for an already-tracked person and comuna
- **WHEN** a new email's extracted `(rut, comuna)` matches an existing record
- **THEN** no second tracked case is created

### Requirement: Manually-entered última-carpeta date and derived sector
The system SHALL let the operator enter the contributor's última-carpeta date per case, and SHALL derive the physical sector from it: before 2023-07-01 → `Archivo`; on/after 2023-07-01 → `Oficina 43`. The sector SHALL be empty until the date is entered.

#### Scenario: Date entered
- **WHEN** the operator sets `fecha_ultima_carpeta = 2022-03-15` on a case
- **THEN** the case's sector is `Archivo`; with `2024-01-10` it is `Oficina 43`

### Requirement: Detect upload signal
The system SHALL scan the configured confirmation folder ("CARP. YA PEDIDAS") on each polling cycle. When an email found there matches a `Pending` record by `InternetMessageId`, the record SHALL transition to `Uploaded` — with NO email sent as a result of the move alone.

#### Scenario: Operator moves a completed case's email
- **WHEN** the moved email's `InternetMessageId` matches a `Pending` record
- **THEN** the record becomes `status = Uploaded` with `uploaded_at` set, and no email is sent

### Requirement: Operator-triggered confirmation email
The system SHALL send the standard "carpeta subida a Conaset" email to the requesting comuna ONLY when the operator explicitly triggers it for an `Uploaded` case (button/action). On success the case transitions to `Confirmed` and the operator is notified (toast + email).

#### Scenario: Operator triggers confirmation on an uploaded case
- **WHEN** the operator triggers the send for a case with `status = Uploaded` and complete data
- **THEN** the confirmation email is sent to that comuna's directory contact, the case becomes `Confirmed`, and the operator is notified

#### Scenario: Trigger on a non-uploaded or already-confirmed case
- **WHEN** the operator triggers the send for a case that is `Pending` or already `Confirmed`
- **THEN** nothing is sent and the system reports why
