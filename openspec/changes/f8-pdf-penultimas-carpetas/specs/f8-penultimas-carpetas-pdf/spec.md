## ADDED Requirements

### Requirement: Penúltimas Carpetas PDF generation
The dashboard SHALL render a print-ready document, independent of sector, containing only `full_name`, `rut`, and `fecha_ultima_carpeta` for every F8 case that is marked (`Marked = true`) and has a folder date (`FechaUltimaCarpeta` is not null), ordered by the time it was marked (`MarkedAt`). Cases without a folder date (S/C — sin carpeta) or that are not marked SHALL be excluded.

#### Scenario: Generating the Penúltimas Carpetas document
- **WHEN** the user requests the Penúltimas Carpetas document
- **THEN** the printed output contains only F8 cases where `Marked = true` and `FechaUltimaCarpeta` is not null, ordered by `MarkedAt`, showing only full name, RUT, and folder date, with no Comuna, F8 code, or Sector columns, and no navigation chrome, ready to print or save as PDF via the browser

#### Scenario: Excluding cases without a folder date
- **WHEN** an F8 case is marked but has no `FechaUltimaCarpeta` (S/C)
- **THEN** that case does not appear in the Penúltimas Carpetas document

#### Scenario: Excluding unmarked cases
- **WHEN** an F8 case has a `FechaUltimaCarpeta` but `Marked = false`
- **THEN** that case does not appear in the Penúltimas Carpetas document

### Requirement: Independent print-state tracking for Penúltimas Carpetas
The system SHALL track whether a case has been printed on the Penúltimas Carpetas document using a dedicated field (`PenultimasCarpetasPdfGeneratedAt`), separate from the sector document's print-state field (`SectorPdfGeneratedAt`). Marking a case as printed on one document SHALL NOT affect its visibility on the other.

#### Scenario: Printing on Penúltimas Carpetas does not hide the case from the sector PDF
- **WHEN** an operator marks a case as printed on the Penúltimas Carpetas document
- **THEN** that case's `SectorPdfGeneratedAt` is unchanged and the case still appears on its sector's PDF document if otherwise eligible

#### Scenario: Printing on the sector PDF does not hide the case from Penúltimas Carpetas
- **WHEN** an operator marks a case as printed on its sector's PDF document
- **THEN** that case's `PenultimasCarpetasPdfGeneratedAt` is unchanged and the case still appears on the Penúltimas Carpetas document if otherwise eligible

#### Scenario: A case already printed on Penúltimas Carpetas is excluded from the list
- **WHEN** a case's `PenultimasCarpetasPdfGeneratedAt` is set
- **THEN** that case does not appear in the Penúltimas Carpetas document until it is re-marked

### Requirement: End-of-month pending-print warning
When a new calendar month begins, the system SHALL check whether any F8 case uploaded (`UploadedAt`) during the immediately preceding calendar month is marked (`Marked = true`), has a folder date (`FechaUltimaCarpeta` is not null), and has not yet been printed on the Penúltimas Carpetas document (`PenultimasCarpetasPdfGeneratedAt` is null). If at least one such case exists, the dashboard SHALL display a persistent warning banner, visible on every dashboard page, naming the pending month (e.g. "julio"). The banner SHALL remain visible across page loads and sessions until every case uploaded in that month has been printed on the Penúltimas Carpetas document.

#### Scenario: Warning appears on the first day of a new month
- **GIVEN** it is August 1st or later
- **AND** at least one F8 case uploaded in July is `Marked = true`, has a `FechaUltimaCarpeta`, and has `PenultimasCarpetasPdfGeneratedAt = null`
- **WHEN** any dashboard page is loaded
- **THEN** a warning banner is shown stating that July's Penúltimas Carpetas must be printed

#### Scenario: Warning persists across unrelated actions
- **GIVEN** the warning banner is showing for July
- **WHEN** the operator performs any action that does not print all of July's pending Penúltimas Carpetas cases (including navigating pages, printing a partial subset, or printing other months)
- **THEN** the warning banner remains visible

#### Scenario: Warning clears once every pending case for that month is printed
- **GIVEN** the warning banner is showing for July
- **WHEN** every F8 case uploaded in July that is `Marked = true` and has a `FechaUltimaCarpeta` has `PenultimasCarpetasPdfGeneratedAt` set
- **THEN** the warning banner no longer appears

#### Scenario: No warning when there are no eligible cases for the prior month
- **GIVEN** no F8 case uploaded in July is both `Marked = true` and has a `FechaUltimaCarpeta`
- **WHEN** any dashboard page is loaded in August
- **THEN** no warning banner is shown for July

#### Scenario: Multiple pending months are all reported
- **GIVEN** July and June both have unprinted eligible cases (e.g. the operator skipped printing for two months)
- **WHEN** any dashboard page is loaded
- **THEN** the warning banner names every pending month with unprinted eligible cases, not only the most recent one
