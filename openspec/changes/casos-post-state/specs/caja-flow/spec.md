## MODIFIED Requirements

### Requirement: Close F8 case without folder
The system SHALL offer a "Sin carpeta" action on the F8 screen. Executing it SHALL record `ClosedWithoutFolderAt`, clear `FolderNotFound`, clear `Destination` and `TransferredAt` so the case returns to Casos, and SHALL NOT send any email.

#### Scenario: F8 case closed without folder
- **WHEN** the operator presses "Sin carpeta" on a case in the F8 screen
- **THEN** the case has `ClosedWithoutFolderAt` set, `FolderNotFound = false`, `Destination = None`, `TransferredAt = null`
- **AND** the case no longer appears in F8
- **AND** the case appears in Casos with status label "Cerrado sin carpeta" and its F8 checkbox unticked

#### Scenario: Closed case never enters Caja
- **WHEN** a Caja transfer is requested for a case with `ClosedWithoutFolderAt` set
- **THEN** the case is not changed
