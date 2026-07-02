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

### Requirement: LAN real-time visibility
The dashboard SHALL be reachable from other machines on the local network and reflect new data without manual page reloads.

#### Scenario: Colleague views live state
- **WHEN** an authenticated user has the dashboard open and a polling cycle ingests new mail
- **THEN** the visible list updates within one polling cycle without the user pressing reload

### Requirement: Supervised mail classification
The system SHALL auto-classify every mail from a known comuna domain as `OutgoingReply`, `IncomingRequest`, `AddressChangeNotification`, or `Unclassified`, and SHALL let an authenticated user reclassify any item. A manual classification SHALL never be overwritten by the auto-classifier.

#### Scenario: Auto-classification proposed
- **WHEN** a mail from a known comuna domain is ingested
- **THEN** it appears in the dashboard with its proposed classification and a marker that it is unconfirmed

#### Scenario: User reclassifies
- **WHEN** a user changes an item's classification
- **THEN** the new classification is stored as manual, displayed as confirmed, and subsequent polling cycles do not change it

### Requirement: Separate reports for sent and received requests
The dashboard SHALL provide two report views switchable by button: requests sent by Valparaíso (with reply status) and requests received from other comunas.

#### Scenario: Switching reports
- **WHEN** the user presses the "Solicitudes recibidas" button
- **THEN** only mail classified as `IncomingRequest` is listed, and vice versa for "Solicitudes enviadas"

### Requirement: Printable report document
The dashboard SHALL render a print-ready document containing, per person: full name, RUT, date of last folder, and the comuna of origin when the case stems from an address change from another comuna.

#### Scenario: Printing
- **WHEN** the user invokes print on the report view
- **THEN** the printed output contains only the document content (no navigation chrome) with the four required fields

### Requirement: Portable distribution
The application SHALL be publishable as a self-contained single-file executable that runs on another Windows PC without a pre-installed .NET runtime.

#### Scenario: Copy to a second PC
- **WHEN** the published executable and its config/data folder are copied to another Windows machine and started
- **THEN** the worker and dashboard run without additional installation steps
