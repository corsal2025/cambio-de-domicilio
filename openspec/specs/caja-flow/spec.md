# caja-flow Specification

## Purpose
TBD - created by archiving change caja-actions-flow. Update Purpose after archive.
## Requirements
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

### Requirement: Send F8 case to Última Carpeta
The F8 screen SHALL NOT offer a "Caja" action. Instead it SHALL offer an "Última carpeta" action. Executing it SHALL move the case to the Última Carpeta module (`Destination = UltimaCarpeta`, `TransferredAt` set), keep every datum already typed (name, RUT, `CodigoF8`, `FechaUltimaCarpeta`) and SHALL NOT send any email. Only an F8 case that is not closed without folder can be moved.

#### Scenario: F8 case sent to Última Carpeta
- **WHEN** the operator presses "Última carpeta" on a case in the F8 screen
- **THEN** the case has `Destination = UltimaCarpeta`, `TransferredAt` set and `UltimaCarpetaRequestedAt = null`
- **AND** the case no longer appears in F8 and appears in Última Carpeta
- **AND** the F8 screen has no "Caja" button

#### Scenario: Case that is not in F8 cannot be sent
- **WHEN** a transfer to Última Carpeta is requested for a case whose destination is not F8
- **THEN** the case is not changed

### Requirement: Última Carpeta module
The system SHALL provide an "Última Carpeta" module, reachable from the Sistemas navigation of every page, that lists the cases sent from F8 (oldest transfer first) with name, RUT, comuna, F8 code, date of the last folder, transfer date and request state. The operator SHALL be able to print the list or save it as PDF, mark a case as requested ("Pedida", recording `UltimaCarpetaRequestedAt`), undo that mark, send a case to the Caja open queue when the last folder arrived, or close it with "Sin carpeta" when the folder does not exist. Closing with "Sin carpeta" moves the case to the Sin Carpetas module, where the process ends. No action in this module SHALL send an email.

#### Scenario: Mark as requested
- **WHEN** the operator presses "Pedida" on a listed case
- **THEN** `UltimaCarpetaRequestedAt` is set and the row shows the request date

#### Scenario: Undo requested
- **WHEN** the operator undoes the request mark
- **THEN** `UltimaCarpetaRequestedAt` is null again

#### Scenario: Send to Caja from Última Carpeta
- **WHEN** the operator presses "Caja" on a listed case
- **THEN** the case has `Destination = Caja`, `Status = Confirmed`, `BoxId = null`, `TransferredAt` set
- **AND** the case appears in the Caja open queue and no longer in Última Carpeta

#### Scenario: The folder does not exist
- **WHEN** the operator presses "Sin carpeta" on a listed case
- **THEN** the case has `ClosedWithoutFolderAt` set, `SinCarpeta = true` and `Destination = SinCarpetas`
- **AND** the case no longer appears in Última Carpeta and appears in Sin Carpetas, where it has no further actions

#### Scenario: Closed without folder never reaches Caja from here
- **WHEN** a Caja transfer is requested for a case with `ClosedWithoutFolderAt` set
- **THEN** the case is not changed

### Requirement: Close F8 case without folder
The system SHALL offer a "Sin carpeta" action on the F8 and Subidas screens (also reachable by typing `S/C` as the F8 date). Executing it SHALL record `ClosedWithoutFolderAt`, set `SinCarpeta`, clear `FolderNotFound`, set `Destination = SinCarpetas` and `TransferredAt`, and SHALL NOT send any email. The case is listed on the Sin Carpetas screen.

#### Scenario: F8 case closed without folder
- **WHEN** the operator presses "Sin carpeta" on a case in the F8 screen
- **THEN** the case has `ClosedWithoutFolderAt` set, `FolderNotFound = false`, `Destination = SinCarpetas`, `TransferredAt` set
- **AND** the case no longer appears in F8 and appears on the Sin Carpetas screen

#### Scenario: Closed case never enters Caja
- **WHEN** a Caja transfer is requested for a case with `ClosedWithoutFolderAt` set
- **THEN** the case is not changed

### Requirement: Sin Carpetas is a terminal, read-only list
The Sin Carpetas screen SHALL list every case closed without folder (the penultimate folder was not found) and SHALL offer no actions on them: no revert to F8, no Caja, no edit. The only available action is printing/saving the list as PDF. The system SHALL NOT expose any handler that moves a case out of `SinCarpetas`.

#### Scenario: Listed case has no actions
- **WHEN** a case sits on the Sin Carpetas screen
- **THEN** its row shows only data (name, RUT, comuna, dates, F8 code, closing date) and no action column

#### Scenario: No way back
- **WHEN** a POST to a revert handler is attempted on the Sin Carpetas page
- **THEN** no such handler exists and the case keeps `Destination = SinCarpetas`

### Requirement: Unified F8 revert
The F8 screen SHALL expose a single "Revertir" action. Regardless of whether the F8 was already uploaded, it SHALL clear the F8 status (`FolderNotFound`, `Destination`, `TransferredAt`, confirmation data, marks) while keeping the data the operator already typed (`FullName`, `Rut`, `CodigoF8`, `FechaUltimaCarpeta`, `SinCarpeta`) and return the case to Casos as `Pending` with `SoloCaja = true`. No email SHALL be sent.

#### Scenario: Revert F8 not yet uploaded
- **WHEN** the operator presses "Revertir" on an F8 case with `CodigoF8 = null`
- **THEN** the case has `Destination = None`, `Status = Pending`, `SoloCaja = true`, `FolderNotFound = false`

#### Scenario: Revert F8 already uploaded
- **WHEN** the operator presses "Revertir" on an F8 case with `CodigoF8 = "F8-123"` and `Status = Confirmed`
- **THEN** the case keeps `CodigoF8 = "F8-123"` and has `ConfirmedAt = null`, `Status = Pending`, `SoloCaja = true`, `Destination = None`
- **AND** in Casos it shows only the "Caja" action

#### Scenario: Reverted case explicitly marked for F8 again
- **WHEN** the operator ticks the F8 checkbox on a case with `SoloCaja = true` in Casos
- **THEN** the action column offers "Traspaso a F8" instead of "Caja"
- **AND** the transfer clears `SoloCaja` without sending email

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

### Requirement: Box codes may repeat
Closing the queue with a code already used by a closed box SHALL be allowed, because several closes can be packed into the same physical box (operator decision 2026-09-24).

#### Scenario: Same physical box
- **WHEN** box `A1-CD` exists and the operator closes the queue with number 1
- **THEN** another closed box record with code `A1-CD` is created with the queued cases

### Requirement: Register old folders straight into Caja
The manual entry form SHALL offer an option to register old folders directly into the Caja queue. With it, each valid row SHALL be stored as uploaded, sent to the open Caja queue in entry order, and SHALL NOT send any email.

#### Scenario: Old folders go to the queue without email
- **WHEN** the operator enters two people with "Carpeta antigua: enviar directo a Caja" ticked
- **THEN** both cases are in the Caja queue in entry order
- **AND** neither appears in Casos
- **AND** no email is sent

#### Scenario: Without the option nothing changes
- **WHEN** the operator enters a person without ticking the option
- **THEN** the case is created as Pendiente in Casos

