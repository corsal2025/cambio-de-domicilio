## ADDED Requirements

### Requirement: Send uploaded case to Caja from Casos
The system SHALL offer a "Caja" action in Casos for every case whose physical folder has been uploaded (`Status` is `Uploaded` or `Confirmed`) and is not closed without folder. Executing it SHALL set `Destination = Caja` and `TransferredAt`, leave `BoxId` null (open queue), and SHALL NOT send any email.

#### Scenario: Confirmed case sent to Caja
- **WHEN** the operator presses "Caja" on a `Confirmed` case in Casos
- **THEN** the case has `Destination = Caja`, `BoxId = null`, `TransferredAt` set
- **AND** the case appears in the Caja open queue
- **AND** the case no longer appears in Casos

#### Scenario: Pending case cannot be sent to Caja
- **WHEN** a `Caja` transfer is requested for a `Pending` case without `SoloCaja`
- **THEN** the case is not changed

### Requirement: Send F8 case with found folder to Caja
The system SHALL offer a "Caja" action on the F8 screen for every F8 case. Executing it SHALL move the case to the Caja open queue with the same effect as the Casos "Caja" action, without sending any email.

#### Scenario: F8 case sent to Caja
- **WHEN** the operator presses "Caja" on a case in the F8 screen
- **THEN** the case has `Destination = Caja`, `BoxId = null`, `TransferredAt` set
- **AND** the case appears in the Caja open queue and no longer in F8

### Requirement: Close F8 case without folder
The system SHALL offer a "Sin carpeta" action on the F8 screen. Executing it SHALL record `ClosedWithoutFolderAt`, clear `Destination` and `TransferredAt` so the case returns to Casos, and SHALL NOT send any email.

#### Scenario: F8 case closed without folder
- **WHEN** the operator presses "Sin carpeta" on a case in the F8 screen
- **THEN** the case has `ClosedWithoutFolderAt` set, `Destination = None`, `TransferredAt = null`
- **AND** the case no longer appears in F8
- **AND** the case appears in Casos with status label "Cerrado sin carpeta"

#### Scenario: Closed case never enters Caja
- **WHEN** a Caja transfer is requested for a case with `ClosedWithoutFolderAt` set
- **THEN** the case is not changed

### Requirement: Unified F8 revert
The F8 screen SHALL expose a single "Revertir" action. Regardless of whether the F8 was already uploaded, it SHALL clear all F8 data (`CodigoF8`, `FolderNotFound`, `Destination`, `TransferredAt`, confirmation data) and return the case to Casos as `Pending` with `SoloCaja = true`. No email SHALL be sent.

#### Scenario: Revert F8 not yet uploaded
- **WHEN** the operator presses "Revertir" on an F8 case with `CodigoF8 = null`
- **THEN** the case has `Destination = None`, `Status = Pending`, `SoloCaja = true`, `FolderNotFound = false`

#### Scenario: Revert F8 already uploaded
- **WHEN** the operator presses "Revertir" on an F8 case with `CodigoF8 = "F8-123"` and `Status = Confirmed`
- **THEN** the case has `CodigoF8 = null`, `ConfirmedAt = null`, `Status = Pending`, `SoloCaja = true`, `Destination = None`
- **AND** in Casos it shows only the "Caja" action

#### Scenario: Reverted case sent to Caja
- **WHEN** the operator presses "Caja" in Casos on a case with `SoloCaja = true`
- **THEN** the case moves to the Caja open queue and `SoloCaja` becomes false

### Requirement: Return case from Caja to Casos
The Caja screen SHALL offer a "Devolver a casos" action on every case in the open queue. Executing it SHALL clear `Destination` and `TransferredAt`, so the case reappears in Casos with its "Caja" action available.

#### Scenario: Queued case returned
- **WHEN** the operator presses "Devolver a casos" on a queued Caja case
- **THEN** the case has `Destination = None`, `TransferredAt = null`
- **AND** it appears in Casos with the "Caja" action

### Requirement: Caja lists keep insertion order
The Caja open queue and every closed box listing (screen and printed document) SHALL list cases in the order they were sent to Caja (`TransferredAt` ascending, then `Id`), never alphabetically nor by Fecha última Carpeta.

#### Scenario: Queue in insertion order
- **WHEN** case B (older última carpeta) is sent to Caja after case A
- **THEN** the queue lists A first and B second

#### Scenario: Closed box keeps insertion order
- **WHEN** the queue with A then B is closed into a box
- **THEN** the box listing shows A first and B second

### Requirement: Unique box codes
Closing a box SHALL be rejected when the chosen code (e.g. `A1-CD`) already belongs to a closed box. The queue SHALL stay open and the operator SHALL see an error naming the duplicated code.

#### Scenario: Duplicate code rejected
- **WHEN** box `A1-CD` exists and the operator closes the queue with number 1
- **THEN** no box is created, the queue is unchanged, and an error mentions `A1-CD`

#### Scenario: Next number accepted
- **WHEN** box `A1-CD` exists and the operator closes the queue with number 2
- **THEN** box `A2-CD` is created with the queued cases
