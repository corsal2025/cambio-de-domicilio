# Spec: Routing (delta)

## ADDED Requirements

### Requirement: Detect outbound folder requests by folder membership
The system SHALL treat a second, independently configured Outlook folder (`RouterOptions.OutboundSourceFolderName`, default `"CARP. PARA PEDIR OTRAS COMUNAS"`) as the sole trigger for outbound requests — cases where Valparaíso must request a person's folder **from** another comuna. The system SHALL poll this folder on the same cycle as the existing inbound `SourceFolderName` and `ConfirmationFolderName` polling, without altering their behavior.

#### Scenario: Email placed in the outbound source folder
- **WHEN** the poller lists `OutboundSourceFolderName` and an email is present
- **THEN** the email is a candidate for outbound processing regardless of its original receipt date

#### Scenario: Existing inbound folders are unaffected
- **WHEN** a polling cycle runs
- **THEN** `SourceFolderName` and `ConfirmationFolderName` are polled and processed exactly as before, independent of outbound folder contents

### Requirement: Process outbound requests using the same extraction and resolution rules as inbound
The system SHALL process each outbound-folder email using the same sender-domain-to-comuna resolution (`IComunaDirectory`), own-domain-sender skip, and person extraction (`PersonDataExtractor`, body merged with subject) as the inbound flow. Each resulting case SHALL be created with `Direction = Outbound` and `Status = Pending`.

#### Scenario: Recognized comuna domain, single contributor
- **WHEN** an outbound-folder email's extracted recipient comuna resolves via the directory and one contributor is found
- **THEN** one case is created with `Direction = Outbound`, `Status = Pending`, and the extracted name/RUT

#### Scenario: Own-domain sender is ignored
- **WHEN** an email in the outbound folder was sent from the organization's own domain
- **THEN** the system does not track it, mirroring inbound own-domain handling

### Requirement: Outbound dedupe is scoped to the Outbound direction
The system SHALL NOT create a duplicate outbound case for a `(rut-or-name, comuna)` pair already tracked as `Direction = Outbound`. This dedupe check SHALL be scoped to `Direction = Outbound` only, so an existing Inbound case for the same person and comuna SHALL NOT block or match against a new outbound request.

#### Scenario: Same person tracked as both directions
- **GIVEN** a person is already tracked as an Inbound case for comuna X
- **WHEN** an outbound-folder email requests that same person's folder from comuna X
- **THEN** a new Outbound case is created — the existing Inbound case does not block it

#### Scenario: Resent outbound request for an already-tracked outbound case
- **WHEN** a new outbound-folder email resolves to a RUT+comuna pair already tracked as `Direction = Outbound`
- **THEN** no second outbound case is created

### Requirement: Outbound idempotent processing per source message
The system SHALL NOT re-create outbound cases for a source email already tracked as `Direction = Outbound`, across polling cycles, mirroring inbound `SourceMessageId` idempotency.

#### Scenario: Two consecutive cycles with no folder change
- **WHEN** the poller runs again with the same items still in the outbound source folder
- **THEN** no duplicate outbound cases are created for messages that already have at least one tracked outbound case

### Requirement: Send the outbound request only on an explicit operator action
The system SHALL send the formal outbound request email only when the operator explicitly triggers "Enviar solicitud" on a Pending Outbound case, and SHALL record who sent it and when. Sending SHALL transition the case directly from `Pending` to `Requested` — there is no `Uploaded` step and no PDF/Conaset step for Outbound cases.

#### Scenario: Explicit send on a Pending outbound case
- **WHEN** the operator triggers "Enviar solicitud" on a Pending, review-complete Outbound case
- **THEN** the system sends one formal email to the resolved comuna's `ContactEmail`, asking that comuna to upload the person's folder to the SGL platform, transitions the case to `Requested`, and records the sending user and timestamp

#### Scenario: Cannot send for a non-eligible case
- **WHEN** the operator attempts "Enviar solicitud" on a case that is not `Pending` Outbound, or is still `needs_review`
- **THEN** the system refuses and returns the reason, without sending any email

### Requirement: Outbound cases are excluded from the tracking CSV report
The system SHALL exclude `Direction = Outbound` cases from the CSV report written to `ReportCsvPath`, since its columns (`fecha_ultima_carpeta`, `status`, `sector`, `confirmed_at`) describe the inbound upload-confirmation workflow and do not apply to outbound requests.

#### Scenario: Report refreshed with outbound cases present
- **WHEN** a polling cycle completes and both Inbound and Outbound cases exist
- **THEN** the CSV at `ReportCsvPath` contains only Inbound rows, unchanged from today's columns and content
