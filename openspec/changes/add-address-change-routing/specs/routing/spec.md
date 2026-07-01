# Spec: Routing

## ADDED Requirements

### Requirement: Detect address-change notifications
The system SHALL scan the configured mailbox on each polling cycle for unprocessed emails whose sender domain matches a known comuna domain (from the `ComunaContact` directory) and is not the organization's own domain.

#### Scenario: Notification from a recognized comuna domain
- **WHEN** a new email arrives from `rfloresc@municatemu.cl`
- **THEN** the system identifies it as an address-change notification candidate for comuna `Catemu`

#### Scenario: Email from own domain is ignored
- **WHEN** a new email arrives from `alguien@munivalpo.cl`
- **THEN** the system does not treat it as an address-change notification

### Requirement: Extract person data
The system SHALL extract `full_name` and `rut` from the body of a detected notification email, case-insensitively for the name and regardless of RUT punctuation.

#### Scenario: Uppercase name, RUT with dots
- **WHEN** the email body contains `GUSTAVO ANDRÉS PEÑA CASTRO` and `RUT: 18.785.387-7`
- **THEN** the system extracts `full_name = "GUSTAVO ANDRÉS PEÑA CASTRO"` and `rut = "18.785.387-7"` (normalized form, dots preserved for display)

#### Scenario: Lowercase/mixed-case name, RUT without dots
- **WHEN** the email body contains `Gustavo Andrés Peña Castro` and `RUT: 18785387-7`
- **THEN** the system extracts `full_name = "Gustavo Andrés Peña Castro"` and `rut` normalized to the same canonical form as the dotted version (e.g. `18.785.387-7`), so both formats match the same person for idempotency/matching purposes

#### Scenario: Missing extractable data
- **WHEN** the email body does not contain a recognizable RUT pattern (with or without dots)
- **THEN** the record is created with `status = pending` and is not sent automatically

### Requirement: Resolve comuna contact and send folder request
The system SHALL resolve the comuna's contact email from an imported directory and send a predefined request email for the last case folder.

#### Scenario: Known comuna
- **WHEN** a `PersonRequest` has `comuna = Catemu` and the directory has a matching entry
- **THEN** the system sends the predefined request email to that contact and sets `status = sent`, recording `request_message_id` and `request_sent_at`

#### Scenario: Unknown comuna
- **WHEN** a `PersonRequest`'s comuna has no entry in the directory
- **THEN** the record stays `status = pending` and no email is sent

### Requirement: Prevent duplicate requests for the same person and comuna
The system SHALL NOT send a second folder request for a RUT+comuna pair that already has a `sent` or `responded` request, even if the notification arrives via a different source email.

#### Scenario: Same person notified twice via different emails
- **WHEN** a new notification is extracted with a RUT and comuna that already has a `sent` or `responded` `PersonRequest`
- **THEN** the system does not send a new request and links the new source email to the existing record instead of creating a duplicate

### Requirement: Idempotent processing
The system SHALL NOT process the same source email twice across polling cycles.

#### Scenario: Two consecutive polling cycles with no new mail
- **WHEN** the background poller runs a cycle with no new messages since the last cycle
- **THEN** no duplicate `PersonRequest` records or duplicate outgoing emails are created

### Requirement: Continuous polling
The system SHALL run as a long-lived background process that polls the mailbox on a configurable interval (default: 30 minutes), rather than a single daily batch run.

#### Scenario: Default interval
- **WHEN** no interval is configured
- **THEN** the system polls every 30 minutes

### Requirement: Detect and match comuna replies
The system SHALL detect replies from comuna domains and match them to the originating `PersonRequest`.

#### Scenario: Reply on the same thread
- **WHEN** a reply arrives on the same conversation as a `sent` request
- **THEN** the system marks that request `status = responded`, records `response_message_id` and `response_received_at`, and fires a notification (see below)

#### Scenario: Reply as a new email referencing the RUT
- **WHEN** a new email (not on the original thread) arrives from a recognized comuna domain and its body contains a RUT matching a `sent` request
- **THEN** the system marks that request `status = responded` and fires a notification

### Requirement: Notify on new response
The system SHALL notify the operator as soon as a `PersonRequest` transitions to `responded`, so the folder can be verified before continuing the "tramitación".

#### Scenario: Running on a PC with an active desktop session
- **WHEN** a `PersonRequest` transitions to `responded` and the on-screen channel is enabled
- **THEN** a Windows toast notification is shown identifying the person and comuna

#### Scenario: Any environment (PC or headless VPS)
- **WHEN** a `PersonRequest` transitions to `responded`
- **THEN** an email notification is sent to the configured address, identifying the person and comuna, regardless of whether the toast channel is enabled

### Requirement: Continuously-refreshed tracking report
The system SHALL rewrite a CSV report to a fixed local file path on every polling cycle, so the operator can open/copy it at any time without an explicit export step. The report SHALL include a `Requiere revisión` column marking records that could not be auto-processed (missing data or unknown comuna).

#### Scenario: Report refreshed each cycle
- **WHEN** a polling cycle completes
- **THEN** the CSV file at the configured path is rewritten with one row per `PersonRequest`, containing `full_name`, `rut`, `comuna`, `status`, `last_folder_date` (empty if not yet responded), and `Requiere revisión` (`Sí` for `pending` records with missing data or unknown comuna, `No` otherwise)

#### Scenario: Pending record with missing data
- **WHEN** a notification could not be parsed for `full_name`/`rut`, or resolved to a known comuna
- **THEN** its row still appears in the report with `Requiere revisión = Sí` and the original email's subject/sender as reference, so it is not silently lost
