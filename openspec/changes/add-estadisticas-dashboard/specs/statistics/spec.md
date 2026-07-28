## ADDED Requirements

### Requirement: Statistics screen access
The system SHALL provide a `/Estadisticas` screen, reachable only by an authenticated operator, linked from the navigation header of every existing dashboard screen (Casos, F8, Certificado, Discarded, Comunas).

#### Scenario: Unauthenticated user is redirected to login
- **WHEN** an unauthenticated visitor requests `/Estadisticas`
- **THEN** the system redirects to `/Login`, consistent with every other dashboard page

#### Scenario: Nav link visible from every screen
- **WHEN** an authenticated operator is on Casos, F8, Certificado, Discarded, or Comunas
- **THEN** the page header shows a link to `/Estadisticas`

### Requirement: Case status distribution
The system SHALL show the count of `PersonRequest` rows grouped by `Status` (Pending, Uploaded, Confirmed) as a donut chart.

#### Scenario: Counts reflect current data
- **WHEN** the repository has 3 Pending, 2 Uploaded, and 5 Confirmed cases
- **THEN** the status chart shows exactly those three counts, summing to 10

### Requirement: Weekly intake trend
The system SHALL show a time-series chart of case volume grouped by ISO week of `ReceivedAt`, covering all recorded weeks.

#### Scenario: Weeks with zero intake are not silently skipped
- **WHEN** a week between the earliest and latest `ReceivedAt` has no cases
- **THEN** that week appears on the chart with a value of zero, not omitted from the axis

### Requirement: Top comunas by volume
The system SHALL show the 10 comunas with the highest case count, ranked descending.

#### Scenario: Fewer than 10 distinct comunas exist
- **WHEN** only 4 distinct comunas have cases
- **THEN** the chart shows exactly those 4, with no placeholder entries

### Requirement: Average confirmation turnaround
The system SHALL compute the average number of days between `ReceivedAt` and `ConfirmedAt`, counting only cases with `Status == Confirmed`.

#### Scenario: Pending and Uploaded cases are excluded from the average
- **WHEN** the repository has Confirmed cases with known turnaround plus Pending/Uploaded cases with no `ConfirmedAt`
- **THEN** the average uses only the Confirmed cases' turnaround days

#### Scenario: No confirmed cases yet
- **WHEN** zero cases have `Status == Confirmed`
- **THEN** the screen shows an explicit "no data yet" state instead of a division-by-zero value or a misleading zero

### Requirement: Folder sector distribution
The system SHALL show the count of cases in sector Archivo vs Oficina43, computed from `FechaUltimaCarpeta` using the same cutoff (July 2023) already used by `PersonRequest.Sector`.

#### Scenario: Cases without a fecha última carpeta are excluded
- **WHEN** a case has no `FechaUltimaCarpeta` set
- **THEN** it is not counted in either sector bucket

### Requirement: F8 deadline backlog
The system SHALL show, among cases with `Destination == F8`, how many are within the configured `PlazoDiasHabiles` business-day deadline versus past it, using the same `DeadlineCalculator` logic the F8 screen already applies per row.

#### Scenario: Deadline bucketing matches the F8 screen's own per-row badge
- **WHEN** a case would render a red "vencido" badge on `/F8`
- **THEN** the same case counts toward the "past deadline" bucket on `/Estadisticas`

### Requirement: F8 sector PDF status
The system SHALL show, among F8-destined cases with a folder sector assigned, how many have been included in a sector PDF (`SectorPdfGeneratedAt` set) versus not yet.

#### Scenario: Re-generating a PDF updates the count
- **WHEN** a previously-pending case is included in a new sector PDF run, setting `SectorPdfGeneratedAt`
- **THEN** the "generated" count increases and the "pending" count decreases by one

### Requirement: Certificado folder-location split
The system SHALL show, among cases with `Destination == Certificado`, how many have `FolderNotFound` set versus not.

#### Scenario: Folder found is the default bucket
- **WHEN** a Certificado case has `FolderNotFound == false`
- **THEN** it counts toward the "found" bucket, not "not found"

### Requirement: Certificado notification status
The system SHALL show, among cases with `Destination == Certificado`, how many have been notified (`CertificadoNotifiedAt` set) versus pending notification.

#### Scenario: Newly transferred case starts as pending
- **WHEN** a case is transferred to Certificado and has not yet been included in an "Avisar certificado" batch
- **THEN** it counts toward "pending", not "notified"

### Requirement: Discarded emails by reason
The system SHALL show a count of discarded emails grouped by their recorded discard reason (e.g., unrecognized sender domain), to help prioritize additions to the comuna directory.

#### Scenario: Reason grouping is stable across re-runs
- **WHEN** the same set of discarded emails is queried twice with no new data
- **THEN** both queries return identical group counts
