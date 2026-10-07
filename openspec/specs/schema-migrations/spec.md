# schema-migrations Specification

## Purpose
Defines how the application upgrades its SQLite database between releases so that operator data (cases, boxes, tombstones) is never lost or silently altered by a startup, and so that an interrupted or failing upgrade can be recovered.

## Requirements

### Requirement: Database schema version is tracked
The system SHALL record the schema version of the database inside the database file itself, and SHALL expose the same version after every successful startup.

#### Scenario: Fresh database
- **WHEN** the application starts and the database file does not exist
- **THEN** the database is created at the latest schema version and the recorded version equals the number of known migrations

#### Scenario: Database already at the latest version
- **WHEN** the application starts and the recorded version equals the latest known version
- **THEN** no migration runs and no data is modified

### Requirement: Migrations apply once and in order
The system SHALL apply each pending migration exactly once, in ascending version order, and SHALL never re-run a migration that was already recorded as applied.

#### Scenario: Several pending migrations
- **WHEN** the database is at version N and versions N+1 and N+2 exist
- **THEN** N+1 runs before N+2 and the recorded version ends at N+2

#### Scenario: Restart after a successful upgrade
- **WHEN** the application is restarted immediately after an upgrade
- **THEN** the second startup applies zero migrations and leaves every row unchanged

### Requirement: Pre-versioning databases are adopted without data loss
The system SHALL upgrade a database created before version tracking existed, including databases from any earlier layout, to the latest version while preserving every existing row and column value.

#### Scenario: Production database from before versioning
- **WHEN** the application starts against a database whose recorded version is zero but which already contains cases, boxes and tombstones
- **THEN** after startup the same rows exist with identical values and the recorded version is the latest

#### Scenario: Database that still has the old unique constraint on the source message id
- **WHEN** the application starts against a database whose case table still forbids two cases sharing one source email
- **THEN** the constraint is removed and every existing case, including its status, dates and flags, is preserved

### Requirement: Pre-migration backup
The system SHALL copy the database file to a backup next to it before applying any pending migration, and SHALL NOT create a backup when no migration is pending.

#### Scenario: Upgrade creates a backup
- **WHEN** at least one migration is pending
- **THEN** a backup file containing the pre-upgrade data exists before the first migration statement executes, and its name states the schema version it was taken at

#### Scenario: Nothing to migrate
- **WHEN** the database is already at the latest version
- **THEN** no new backup file is created

#### Scenario: Repeated failed upgrade attempts keep backups bounded
- **WHEN** upgrade attempts at the same version fail repeatedly and the application is restarted each time
- **THEN** at most three backups of that version are kept, always including the one taken before the most recent attempt

### Requirement: Failed migration is atomic and stops startup
The system SHALL run each migration in a single transaction, and if a migration fails SHALL roll it back, leave the database at the last successfully applied version, and stop startup with an error that names the failing version.

#### Scenario: Migration throws midway
- **WHEN** a migration fails after changing some tables
- **THEN** none of that migration's changes remain, the recorded version is unchanged, and the application does not start serving requests

#### Scenario: Database from a newer release
- **WHEN** the recorded version is higher than any migration this release knows
- **THEN** the application refuses to start and reports that the database belongs to a newer version, without modifying it

### Requirement: Concurrent writers do not corrupt an upgrade
The system SHALL ensure that no other connection writes to the database while a migration is running.

#### Scenario: Request arrives during an upgrade
- **WHEN** a migration is running
- **THEN** the application is not yet accepting dashboard requests, so no concurrent write is possible

### Requirement: Data backfills run once
The system SHALL run each data backfill (reclassifying existing rows to match a newer model) only as part of the migration that introduces it, and SHALL NOT repeat it on later startups.

#### Scenario: Restart does not reclassify cases
- **WHEN** a case with Uploaded status and no destination exists and the application restarts at the latest version
- **THEN** the case keeps its status and destination exactly as before the restart

#### Scenario: Adoption applies the backfills once
- **WHEN** a pre-versioning database containing Uploaded or Confirmed cases with no destination is adopted
- **THEN** those cases are moved to the Subidas destination during that upgrade and never again afterwards
