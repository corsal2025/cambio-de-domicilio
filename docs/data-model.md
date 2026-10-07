# Data Model Documentation

This document describes the data model for **CambioDeDomicilio**, a web dashboard plus mailbox
synchronizer that tracks folder requests other comunas make to Valparaíso for a contributor's
"última carpeta" (most recent driver's-license folder), from detection through upload,
confirmation and physical filing in a box.

Storage: SQLite, single file, no ORM (`Microsoft.Data.Sqlite` directly). Schema versions are tracked
with `PRAGMA user_version` (see "Schema versioning and migrations" below).

> Reference: domain types live in `Domain/PersonRequest.cs` (`PersonRequest`, `RequestStatus`,
> `CaseDestination`, `Box`), `Domain/DiscardedEmail.cs`, `Domain/FolderSectorRule.cs` and
> `Domain/DeadlineCalculator.cs`; persistence in `Persistence/*.cs`.

## Model Descriptions

### 1. PersonRequest

Represents one tracked contributor request — one row per person, even when several people were
named in the same source email.

**Identity and source fields:**
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

**Lifecycle fields:**
- `Status`: One of `Pending`, `Uploaded`, `Confirmed` (see lifecycle below). Stored as text.
- `ReceivedAt`: When the source email was received — the legal upload deadline counts from this date
- `UploadedAt`: When the case transitioned to Uploaded (nullable until then)
- `ConfirmedAt`: When the confirmation email was sent — a real email goes to another municipality,
  so the timestamp is always recorded
- `ConfirmedByUserId`: legacy attribution column, kept nullable per the additive-schema convention;
  no longer populated since the dashboard has no user accounts
- `ConfirmationBouncedAt`: Set when a non-delivery report for this case's confirmation email is
  found in the mailbox inbox (the requesting comuna's mail server rejected it). Cleared by the
  operator with "Marcar resuelto". Null means the confirmation is presumed delivered.
- `CreatedAt`: When this record was first created

**Folder fields:**
- `FechaUltimaCarpeta`: Date of the contributor's última carpeta, typed in manually by the operator;
  derives `Sector` (computed, not stored — see "Business rules" below)
- `SinCarpeta`: Operator-entered "S/C" (Sin Carpeta) in place of a date. Mutually exclusive with
  `FechaUltimaCarpeta` (setting one clears the other)
- `FolderNotFound`: Operator-ticked "F8" checkbox in Casos — the physical folder could not be
  located. Only marks the case as an F8 candidate; the case leaves Casos only when the operator
  presses "Traspaso a F8" (which sets `Destination = F8`)
- `CodigoF8`: Free-text F8 case code, editable at any time, no fixed format enforced
- `SectorPdfGeneratedAt`, `PenultimasCarpetasPdfGeneratedAt`: when the case was last included in the
  printed sector document / the printed Penúltimas Carpetas document. Tracked independently; a case
  already printed is excluded from the next printout of that document.

**Operator bookkeeping fields (no effect on routing or confirmation):**
- `Marked` / `MarkedAt`: "Marcar" checkbox and the moment it was ticked on (`MarkedAt` drives the
  display/print order of marked cases). `Marked` is reset when the case is sent to Caja.
- `PendienteCarpeta`: "Pendiente carpeta" checkbox — visual work-in-progress marker only.

**Destination fields (where the case lives — independent of `Status`):**
- `Destination`: `CaseDestination`, stored as text, default `None`:
  - `None` — still in Casos (`/Index`)
  - `F8` — transferred to `/F8` (folder not found)
  - `Subidas` — transferred to `/SubidasASistema` once the confirmation email is sent from Casos
    (including through the one-click "Marcar subida")
  - `Caja` — in the Caja screen (open queue while `BoxId` is null, or in a closed box)
  - `SinCarpetas` — closed without a physical folder; listed on `/SinCarpetas`
- `TransferredAt`: When the case was moved to its current `Destination`. Null means not transferred.
- `BoxId`: Closed `Box` this case was packed into. Null means it is still in the open Caja queue
  (only meaningful when `Destination = Caja`).
- `SoloCaja`: Set when an F8 case is reverted ("Revertir") back into Casos. A `SoloCaja` case can
  only be sent to Caja (or explicitly re-ticked for F8); it can never be re-confirmed or emailed.
  `SendToCaja` clears it.
- `ClosedWithoutFolderAt`: Set by "Sin carpeta" (from F8 or Subidas, or typing `S/C` as the F8 date).
  Distinct from `SinCarpeta` (the "S/C" typed in place of a date). Such a case is stored with
  `Destination = SinCarpetas`, `SinCarpeta = 1`, `FolderNotFound = 0`, and can never enter Caja.

**Computed members (not stored):**
- `Sector`: `FolderSectorRule.For(FechaUltimaCarpeta)`; null while there is no date.
- `IsActionCompleted`: true when `Status` is `Uploaded`/`Confirmed`, or `Destination = Caja`, or
  `ClosedWithoutFolderAt` is set. Drives the "done" row colour in Casos.

**Validation rules:**
- A case can only transition `Pending → Uploaded` when its source email is found in
  "CARP. YA SUBIDAS" (or via the one-click "Marcar subida" action, which moves it there itself).
- A case can only be confirmed (`Uploaded → Confirmed`) when `NeedsReview = false`,
  `FullName`/`Rut`/`Comuna` are all present, the comuna is in the directory, and either
  `FechaUltimaCarpeta` or `SinCarpeta` is set.
- Sending to Caja (`SendToCaja`) only affects cases that are `Uploaded`/`Confirmed`, `SoloCaja`, or in
  F8, and never those with `SinCarpeta` or `ClosedWithoutFolderAt`. Sending an F8 or `SoloCaja` case
  to Caja sets its `Status` to `Confirmed` without sending any email.
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
  (manually moved) — nothing has been sent to the comuna yet
- `Confirmed`: the confirmation email was sent to the requesting comuna (only ever on an explicit
  operator action) — `ConfirmedAt` is set. "Marcar subida" performs `Pending → Uploaded → Confirmed`
  in a single click.
- Backwards transitions: "Rectificar confirmación" (sends a rectification email and resets a
  `Confirmed` case to `Pending`, clearing `UploadedAt`, `ConfirmedAt` and the bounce flag, and
  returning a `Subidas` or open-queue `Caja` destination to `None`); and automatically, if the
  original email reappears in "CARP. PARA PEDIR", `Uploaded` (never `Confirmed`) cases for that
  email revert to `Pending`.

**Destination lifecycle (Casos is `None`):**
```
None --Marcar subida / Enviar confirmación--> Subidas
None --Traspaso a F8 (needs the F8 checkbox)--> F8
None | Subidas | F8 --Caja--> Caja (open queue, BoxId NULL) --Cerrar Caja--> Caja (BoxId set)
F8 | Subidas --Sin carpeta (or "S/C" as F8 date)--> SinCarpetas   (terminal, read-only)
F8 --Revertir--> None (Status Pending, SoloCaja = 1)
Subidas --Rectificar--> None (Status Pending)
Caja (open queue) --Devolver a casos--> None
Caja (closed box) --Quitar de caja--> None;  Reabrir caja--> its cases return to the open queue
```
"Sin Carpetas" has no handler that moves a case out of it: no revert, no Caja, no edit; the only
action is printing the list.

### 2. Box

One closed batch of Caja cases, in the physical order they were packed. Table `Box`.

**Fields:** `Id`, `Number` (sequential: `MAX(Number)+1`), `Code` (label such as `A1-CD`, default
empty), `ClosedAt`.

**Rules:**
- "Cerrar Caja" (`BoxRepository.CloseBox`) inserts the box and assigns its `Id` to every case with
  `Destination = Caja AND BoxId IS NULL AND SinCarpeta = 0`, in one transaction.
- `Code` may repeat: several closes can be packed into the same physical box.
- Cases are always listed in the order they were sent to Caja (`TransferredAt`, then `Id`); a case's
  N° is its position in that listing.
- "Reabrir caja" (`ReopenBox`) sets `BoxId = NULL` on its cases (they stay in `Caja`) and deletes the
  `Box` row; `RemoveCaseFromClosedBox` returns one case to Casos (`Destination = None`,
  `BoxId`/`TransferredAt` cleared).

### 3. DiscardedEmail

An email whose sender domain could not be resolved to a known comuna, kept for operator visibility
instead of being silently dropped.

**Fields:**
- `Id`, `SourceMessageId` (unique — one discard record per message), `SourceSubject`,
  `SourceSender`, `Reason` (human-readable, e.g. "Dominio no reconocido..." or "Dominio compartido
  por varias comunas..."), `DiscardedAt`

**Lifecycle:** deleted automatically the next time its message resolves to a known comuna (directory
updated, or an exact address registered for a domain shared by several comunas) — see the `routing`
spec's "stale Discarded records" requirement.

### 4. ComunaContact

Reference directory mapping a comuna to its municipal contact email, loaded from
`data/comunas.csv` (editable from the dashboard's "Comunas" page).

**Fields:** `Comuna`, `ContactEmail`, `Domain` — not a database table; loaded fresh from the CSV on
each synchronization. Multiple rows for the same `(Comuna, Domain)` are allowed (contact-email
history); the last one wins. Multiple *different* comunas can share the same `Domain` (a generic
webmail provider) — resolution then requires an exact `ContactEmail` match, not domain alone (see
`routing` spec).

### 5. Tombstone tables (`DeletedSourceMessage`, `ProcessedBounce`)

Two single-column key-plus-timestamp tables that record "this message has been dealt with, never
act on it again". They are written and read through `IMessageTombstoneRepository`, kept apart from
the cases because they are a write-once log keyed by message id, not part of any case's lifecycle:
- `DeletedSourceMessage` (`SourceMessageId`, `DeletedAt`): a request email the operator deleted
  every case for, so a later synchronization never recreates it while it still sits in
  "CARP. PARA PEDIR".
- `ProcessedBounce` (`BounceMessageId`, `ProcessedAt`): a non-delivery report already scanned, so a
  later synchronization never re-flags a case from the same NDR (the message stays in the inbox).

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

## Reading cases: `Find` vs `GetAll`

`IPersonRequestRepository.Find(CaseQuery)` and `Count(CaseQuery)` run the filter in SQL and always return
rows ordered by `Id` (the same order as `GetAll()`, so a page that sorts afterwards with a stable LINQ
`OrderBy` keeps its tie order). Filters combine with AND and a null property does not filter:

```csharp
// Casos list: only cases still in Casos that need review
repository.Find(new CaseQuery { Destinations = [CaseDestination.None], NeedsReview = true });

// Sector print list: marked cases that were never transferred (the sector itself is derived in LINQ)
repository.Find(new CaseQuery { Marked = true, Transferred = false })
    .Where(c => c.Sector == FolderSector.Archivo);
```

Available filters: `Destinations`, `Statuses`, `NeedsReview`, `Bounced`, `Marked`, `Transferred` and
`InSinCarpetasBucket`. The sector is deliberately not a filter: it derives from `FechaUltimaCarpeta`, and comparing
dates as text in SQL would misclassify any row stored in a non-ISO format that `DateOnly.Parse` accepts. The `(Destination, Status)` index (migration V002) backs the common listings.
Predicates that have no filter (for example `SectorPdfGeneratedAt is null`, free-text search) stay in LINQ
over the already-reduced rows. Use `GetAll()` only for genuinely whole-table work: the CSV report
(`RouterWorker`) and the statistics screen. `CaseQueryTests` compares every filter with an in-memory
oracle over a fixture covering the cross product of the flags the pages branch on — add a case there
whenever a filter is added.

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
Box           (1) ----< (N) PersonRequest        via PersonRequest.BoxId (null = open Caja queue)
```

The dashboard has no user accounts — it runs on the municipal LAN with access gated at the
network layer, so there is no `DashboardUser` table and no authentication schema.

## Schema versioning and migrations

- The schema version is stored in the SQLite header (`PRAGMA user_version`). `SchemaMigrator`
  (`Persistence/Migrations/`) applies each pending migration exactly once, in ascending order, each
  in its own immediate (write-locked) transaction that also bumps `user_version`; a failure rolls
  that migration back and stops startup with an error naming the version.
- Migrations are an append-only list (`Migrations.All`); versions must be contiguous from 1:
  - **V001_Baseline** — adopts every pre-versioning database and builds the full schema on a fresh
    file: creates `PersonRequest`, `DiscardedEmail`, `DeletedSourceMessage`, `ProcessedBounce`, `Box`;
    adds every later column additively (`ALTER TABLE ... ADD COLUMN` guarded by `PRAGMA table_info`);
    rebuilds `PersonRequest` if it still has the old `UNIQUE` on `SourceMessageId`; runs the data
    backfills once (legacy `MovedToF8At` → `Destination = F8`; removed `Certificado` destination →
    `None`; Uploaded/Confirmed cases in Casos → `Subidas`; closed-without-folder and F8 `SinCarpeta`
    cases → `SinCarpetas`; legacy F8-coded cases → `SoloCaja`). It is frozen and must never be edited.
  - **V002_CaseListIndex** — `IX_PersonRequest_DestinationStatus` on `(Destination, Status)`, since
    every dashboard page selects by destination and often status. (`IX_PersonRequest_RutComuna` on
    `(Rut, Comuna)` comes from the baseline.)
- Before the first pending migration on a database that already has tables, an **online backup**
  (`SqliteConnection.BackupDatabase`) is written next to the file as
  `<db>.bak-v<currentVersion>-<UTC yyyyMMddHHmmss>`; at most 3 backups per version are kept. No backup
  is made on a brand-new database or when nothing is pending.
- A database whose `user_version` is higher than the latest known migration is refused
  (`DatabaseNewerThanApplicationException`) and left untouched.
- Legacy columns (`ConfirmedByUserId`, `MovedToF8At`, `CertificadoNotifiedAt`,
  `FolderNotFoundNotifiedAt`) remain in the table per the additive-schema convention.

## Persistence layer

Raw SQL, split by concern (`Persistence/`):
- `PersonRequestRepository` (`IPersonRequestRepository`) — case reads/writes, status transitions,
  `SendToCaja`, `CloseWithoutFolder`, `RevertF8AndReturnToCasos`, `Find(CaseQuery)` / `Count(CaseQuery)`.
- `BoxRepository` (`IBoxRepository`) — `CloseBox`, `ReopenBox`, `RemoveCaseFromClosedBox`, `GetBoxes`,
  `FindBoxById`, `GetCasesByBoxId`.
- `MessageTombstoneRepository` (`IMessageTombstoneRepository`) — the two tombstone tables.
- `DiscardedEmailRepository` — `DiscardedEmail`.
- `PersonRequestMapper` — the single row-to-`PersonRequest` mapping shared by every repository that
  returns cases (it fails loudly on a missing column instead of defaulting).
- `CaseQuery` / `CaseQuerySql` — SQL-side filtering so pages stop loading the whole table. All
  properties are optional and combined with AND: `Destinations`, `Statuses`, `NeedsReview`, `Bounced`,
  `Marked`, `Transferred`, `InSinCarpetasBucket` (destination `SinCarpetas`, or closed without folder,
  or `SinCarpeta` flagged). Results are ordered by `Id`.
- `SqliteConnectionSetup` — opens configured connections (and enables WAL during migrations).

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

## Business rules derived from the model

- **Sector rule** (`FolderSectorRule`): `FechaUltimaCarpeta < 2023-07-01` → `Archivo`; otherwise
  `Oficina43` (displayed as "Oficina 43"). No date (or "S/C") means no sector.
- **Legal deadline** (`DeadlineCalculator`): 15 business days (Mon–Fri, per the `dashboard` spec)
  counted from `ReceivedAt`; the number of days is the `PlazoDiasHabiles` option. Chilean public
  holidays are **not** excluded, so the countdown is slightly conservative. The alert stops
  applying once the case is `Uploaded`/`Confirmed`.
- **Synchronization is manual**: `RouterWorker.ExecuteAsync` does nothing; the mailbox is read only
  when the operator presses "Sincronizar ahora" (`IndexModel.OnPostSyncNowAsync` →
  `RunCycleAsync`). There is no polling interval. In a development environment (EWS URL host ends
  in `.invalid`) the cycle returns an explanatory "correo desactivado" message instead of syncing.
- **No email without a click**: only "Enviar confirmación", "Marcar subida" and "Rectificar
  confirmación" send mail to a comuna. Caja, Sin carpeta, Revertir, Devolver and Cerrar Caja never do.
- **Row colours in Casos** (`wwwroot/css/dashboard.css`, class chosen in `Index.cshtml`): white =
  untouched; blue (`#93c5fd`, `row-done`) = completed (`IsActionCompleted`); yellow = `Marked`;
  purple = `PendienteCarpeta`; slate = `FolderNotFound` (F8 candidate); red/pink = `NeedsReview` or a
  bounced confirmation (`ConfirmationBouncedAt`). The CSS rules for marked/pendiente/F8 come after
  and override "done".

## Read-only aggregation (Estadísticas)

`/Estadisticas` reads `PersonRequest` and `DiscardedEmail` only through `IPersonRequestRepository
.GetAll()` / `IDiscardedEmailRepository.GetAll()` — the same accessors every other screen already
uses — and computes every chart's numbers in memory via `Statistics.StatisticsService`. No new
table, no new column, no write path: this is purely additive reporting over the schema above.
