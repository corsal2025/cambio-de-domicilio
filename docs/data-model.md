# Data Model Documentation

This document describes the data model for **CambioDeDomicilio**, a background service that
tracks folder requests other comunas make to Valparaíso for a contributor's "última carpeta"
(most recent driver's-license folder), from detection through upload and confirmation.

Storage: SQLite, single file, no ORM (`Microsoft.Data.Sqlite` directly).

## Model Descriptions

### 1. PersonRequest

Represents one tracked contributor request — one row per person, even when several people were
named in the same source email.

**Fields:**
- `Id`: Primary key (integer, autoincrement)
- `FullName`: Full name as extracted (e.g. `GUSTAVO ANDRÉS PEÑA CASTRO`), nullable if extraction
  failed or the case still needs manual review
- `Rut`: Chilean RUT, normalized to canonical dotted form (e.g. `18.785.387-7`) regardless of source
  punctuation, nullable if extraction failed
- `Comuna`: Comuna name, resolved via the sender domain (or exact address, for domains shared by
  several comunas) against `ComunaContact` — never guessed from the domain string
- `SourceMessageId`: `InternetMessageId` (RFC 5322 `Message-ID` header) of the original request
  email — chosen over the EWS `ItemId` because `ItemId`/`ChangeKey` change when an item is moved or
  otherwise touched, while `InternetMessageId` is immutable. **Not unique**: a single source email
  can list more than one contributor, and each gets its own row sharing this value.
- `SourceConversationId`: Exchange `ConversationId` of the original email
- `SourceSubject`: Subject of the original email, kept for manual-review reference
- `SourceSender`: Sender address of the original email, kept for manual-review reference
- `NeedsReview`: `true` when `FullName` or `Rut` could not be resolved automatically, or when a name
  was only found via the subject-line fallback (a subject-derived name is never auto-trusted — see
  the `extraction` spec)
- `Status`: One of `Pending`, `Uploaded`, `Confirmed` (see lifecycle below)
- `ReceivedAt`: When the source email was received — the legal upload deadline counts from this date
- `FechaUltimaCarpeta`: Date of the contributor's última carpeta, typed in manually by the operator;
  derives `Sector` (computed, not stored: `Archivo` if before July 2023, else `Oficina43`)
- `UploadedAt`: When the case transitioned to Uploaded (nullable until then)
- `ConfirmedAt`: When the confirmation email was sent — a real email goes to another municipality,
  so the timestamp is always recorded
- `ConfirmedByUserId`: legacy attribution column, kept nullable per the additive-schema convention;
  no longer populated since the dashboard has no user accounts
- `ConfirmationBouncedAt`: Set when a non-delivery report for this case's confirmation email is
  found in the mailbox inbox (the requesting comuna's mail server rejected it). Cleared by the
  operator with "Marcar resuelto". Null means the confirmation is presumed delivered.
- `Marked`: Operator-only bookkeeping checkbox (boolean), independent of `Status` — lets the
  operator tick off cases they've cross-checked manually, with zero effect on the routing/
  confirmation logic
- `CreatedAt`: When this record was first created

**Validation rules:**
- A case can only transition `Pending → Uploaded` when its source email is found in
  "CARP. YA SUBIDAS" (or via the one-click "Marcar subida" action, which moves it there itself).
- A case can only be confirmed (`Uploaded → Confirmed`) when `NeedsReview = false` and
  `FullName`/`Rut`/`Comuna` are all present.
- A given `(Rut, Comuna)` pair has at most one tracked case — a resend for the same person+comuna
  does not create a duplicate row.
- `SourceMessageId` is deliberately **not** a unique constraint (see field description above); the
  "already processed" idempotency check is "does at least one row exist for this message", not a
  schema-level uniqueness guarantee.

**Status lifecycle:**
```
Pending -> Uploaded -> Confirmed
```
- `Pending`: request detected, folder not yet uploaded to Conaset
- `Uploaded`: operator uploaded the folder and the source email was found in "CARP. YA SUBIDAS"
  (manually moved, or via "Marcar subida") — nothing has been sent to the comuna yet
- `Confirmed`: the confirmation email was sent to the requesting comuna (only ever on an explicit
  operator action) — `ConfirmedAt` is set

### 2. DiscardedEmail

An email whose sender domain could not be resolved to a known comuna, kept for operator visibility
instead of being silently dropped.

**Fields:**
- `Id`, `SourceMessageId` (unique — one discard record per message), `SourceSubject`,
  `SourceSender`, `Reason` (human-readable, e.g. "Dominio no reconocido..." or "Dominio compartido
  por varias comunas..."), `DiscardedAt`

**Lifecycle:** deleted automatically the next time its message resolves to a known comuna (directory
updated, or an exact address registered for a domain shared by several comunas) — see the `routing`
spec's "stale Discarded records" requirement.

### 3. ComunaContact

Reference directory mapping a comuna to its municipal contact email, loaded from
`data/comunas.csv` (editable from the dashboard's "Comunas" page).

**Fields:** `Comuna`, `ContactEmail`, `Domain` — not a database table; loaded fresh from the CSV on
each polling cycle. Multiple rows for the same `(Comuna, Domain)` are allowed (contact-email
history); the last one wins. Multiple *different* comunas can share the same `Domain` (a generic
webmail provider) — resolution then requires an exact `ContactEmail` match, not domain alone (see
`routing` spec).

### 4. Tombstone tables (`DeletedSourceMessage`, `ProcessedBounce`)

Two single-column key-plus-timestamp tables that record "this message has been dealt with, never
act on it again":
- `DeletedSourceMessage` (`SourceMessageId`, `DeletedAt`): a request email the operator deleted
  every case for, so a later poll never recreates it while it still sits in "CARP. PARA PEDIR".
- `ProcessedBounce` (`BounceMessageId`, `ProcessedAt`): a non-delivery report already scanned, so a
  later poll never re-flags a case from the same NDR (the message stays in the inbox).

## Repository boundaries

Persistence is split by concern; each caller depends only on the interface it needs:

| Interface | Owns | Used by |
|---|---|---|
| `IPersonRequestRepository` | `PersonRequest` rows: intake, edits, status/destination transitions, Caja queue (`GetCajaQueue`, `SendToCaja`) | routing service, worker, every dashboard page |
| `IBoxRepository` | `Box` rows and box-level operations: `CloseBox`, `ReopenBox`, `RemoveCaseFromClosedBox`, `GetBoxes`, `FindBoxById`, `GetCasesByBoxId` | `Caja`, `Index` (search location) |
| `IMessageTombstoneRepository` | `DeletedSourceMessage` and `ProcessedBounce` write-once logs | routing service, `Index`, `F8` |
| `IDiscardedEmailRepository` | `DiscardedEmail` rows | routing service, `Discarded`, statistics |

All of them open short-lived connections through `SqliteConnectionSetup` and map cases through
`PersonRequestMapper`. Schema creation is not a repository concern (see below).

## Schema versioning and migrations

The schema version is stored in the database file itself (`PRAGMA user_version`). On every startup,
`SchemaMigrator` (`Persistence/Migrations/`) applies the migrations newer than that version — each
one once, in order, inside its own immediate transaction — before the application serves anything.

- **Adding a schema change**: create `V00N_<Name>.cs` implementing `IMigration` with the next
  version number and append it to `Migrations.All`. Use plain `ALTER`/`CREATE` statements; assign
  every command to the supplied transaction.
- **Never edit a shipped migration.** Production databases have already recorded its version, so a
  fix is a new migration. `V001_Baseline` is deliberately frozen: it is the former `EnsureSchema`
  (including the legacy table rebuild and its hand-written column list) and adopts every database
  created before versioning, as well as building the full schema on a fresh file.
- **Data backfills belong to the migration that introduces them** and run once. They used to be
  re-run on every startup; after the baseline adoption a restart no longer reclassifies cases.
- **Backup**: before the first pending migration on a database that already has tables, the file is
  copied (SQLite online backup, safe in WAL mode) to `<db>.bak-v<currentVersion>-<yyyyMMddHHmmss>`.
  No backup is made for a new database or when nothing is pending.
- **Failure**: the failing migration is rolled back, the version stays at the last applied one, and
  startup aborts with an error naming the version. A database whose version is higher than this
  release knows is refused untouched (an old executable never writes to a newer database).
- Additive-schema convention still applies: existing columns are not dropped or renamed.

## Entity Relationship

```
ComunaContact (1) ----< (N) PersonRequest        via comuna/domain (or exact address) match
```

The dashboard has no user accounts — it runs on the municipal LAN with access gated at the
network layer, so there is no `DashboardUser` table and no authentication schema.

## Identification Rules (business logic, not schema)

- **Request source detection**: an email is a candidate request if its sender domain matches a
  `Domain` already present in `ComunaContact` and is **not** the organization's own domain
  (`munivalpo.cl`). Domains not present in the directory are discarded for manual review, not
  ignored outright.
- **Shared-domain resolution**: when a domain matches more than one comuna, resolution requires an
  exact sender-address match against a registered `ContactEmail`; an unrecognized address under an
  ambiguous domain is discarded rather than guessed.
- **Duplicate suppression**: before creating a case, check for an existing `(Rut, Comuna)` pair; if
  found, the new source email is not turned into a second row.

## Read-only aggregation (Estadísticas)

`/Estadisticas` reads `PersonRequest` and `DiscardedEmail` only through `IPersonRequestRepository
.GetAll()` / `IDiscardedEmailRepository.GetAll()` — the same accessors every other screen already
uses — and computes every chart's numbers in memory via `Statistics.StatisticsService`. No new
table, no new column, no write path: this is purely additive reporting over the schema above.
