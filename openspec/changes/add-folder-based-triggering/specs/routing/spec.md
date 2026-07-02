# Spec: Routing (delta)

## MODIFIED Requirements

### Requirement: Detect address-change notifications
The system SHALL scan a configured, named Outlook folder (default "Para pedir") on each polling cycle for unprocessed emails whose sender domain matches a known comuna domain (from the `ComunaContact` directory) and is not the organization's own domain. The Inbox SHALL NOT be scanned directly, and any folder outside the configured one (e.g. "Carpetas subidas a Conaset") SHALL NOT be read or modified by this capability.

#### Scenario: Item moved into the source folder is picked up regardless of original receipt date
- **WHEN** an email received three days ago is moved into the configured source folder today
- **THEN** the next polling cycle detects and processes it (no `DateTimeReceived`-based filtering excludes it)

#### Scenario: Already-processed item remains in the folder
- **WHEN** a polling cycle lists an item whose `InternetMessageId` already exists in `PersonRequest`
- **THEN** it is skipped without creating a duplicate record or sending a duplicate request

#### Scenario: Configured folder cannot be resolved
- **WHEN** the configured folder name does not match any folder in the mailbox
- **THEN** the cycle logs a warning and processes no items, without crashing the service

#### Scenario: Archive folder is untouched
- **WHEN** a polling cycle runs
- **THEN** "Carpetas subidas a Conaset" (or any folder other than the configured source folder) is neither listed nor modified
