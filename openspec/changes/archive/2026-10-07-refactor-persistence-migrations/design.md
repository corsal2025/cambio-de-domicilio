# Design

## Context

Current state (see `proposal.md` for motivation):

- `PersonRequestRepository` (1,069 lines) implements `IPersonRequestRepository` (35 methods) covering four concerns: cases, boxes + the Caja queue, and two tombstone tables. `DiscardedEmailRepository` is already separate and small.
- `EnsureSchema` creates the base tables, adds ~15 columns via `PRAGMA table_info` checks, rebuilds `PersonRequest` when the legacy `UNIQUE` on `SourceMessageId` is detected, and runs four `UPDATE` backfills. It runs on every startup (`Program.cs`) and the database carries no version marker (`PRAGMA user_version` is 0).
- Every connection is opened per operation through `SqliteConnectionSetup` (WAL + `busy_timeout` since the previous change). Only one process ever uses the file (named mutex in `Program.cs`).
- Dashboard pages and `StatisticsService` call `GetAll()` and filter in memory. The page models are constructed directly by tests; 12 test files construct `PersonRequestRepository` directly.
- Production data (PII) lives in `data/router.db`; there is no staging copy, so the upgrade must be recoverable.

## Goals / Non-Goals

**Goals:**
- Versioned, apply-once, transactional migrations with an automatic pre-upgrade backup (spec: `schema-migrations`).
- Smaller, single-purpose repositories whose interfaces list only what each caller uses.
- Dashboard reads that filter in SQL, with results identical to today's LINQ filters.
- Each step independently shippable with the full suite green (baby steps, TDD).

**Non-Goals:**
- No ORM, no new package, no schema redesign, no column drops/renames, no pagination UI.
- No change to the public behavior of any page, except the explicit backfill decision below.
- No attempt to make `Map()` generic or generate SQL; raw SQL stays.

## Decisions

### D1. `PRAGMA user_version` as the version marker
One integer in the database header, read/written in the same transaction as the migration. Rationale: no extra table, atomic with the DDL (SQLite makes `user_version` part of the transaction), and visible with any SQLite tool. Alternative considered: a `SchemaVersion` table — adds a table that must itself be bootstrapped and can drift from the data; rejected.

### D2. Migration = ordered class with `Version` and `Apply(connection, transaction)`
A static ordered list (`Migrations.All`) of small classes; the runner (`SchemaMigrator`) validates versions are contiguous from 1, applies pending ones each in its own `BEGIN IMMEDIATE` transaction, then sets `user_version`. No reflection/discovery: an explicit list is greppable and testable. Alternative: embedded `.sql` files — nice diffs but the baseline needs C# logic (`PRAGMA table_info` checks), so a mixed model would be harder to reason about.

### D3. Baseline migration V1 = today's `EnsureSchema`, frozen
V1 contains the current idempotent logic verbatim (base tables, additive columns, `UNIQUE` removal rebuild, and the four backfills). It is the only migration that must handle "every historical layout"; because it is frozen, the hand-listed column list in the rebuild can never drift from future changes. A fresh database goes through V1 too, so there is exactly one code path to create the schema. Alternative: a clean `CREATE` for fresh databases plus V1 only for legacy — two paths that can diverge; rejected.

### D4. Backfills become one-shot (owner decision, 2026-10-06)
The four `UPDATE` statements currently re-run on each startup with observable side effects (e.g. an Uploaded case awaiting confirmation is moved from Casos to Subidas on restart). They move into V1 and run once. Behavior for new rows after adoption is therefore whatever the application code writes, which is what the pages already assume. Alternative (a separate per-startup repair step) was offered and declined.

### D5. Backup before migrating, restore documented
If any migration is pending and the file exists, copy it with SQLite's online backup API (`SqliteConnection.BackupDatabase`) to `<db>.bak-v<currentVersion>-<yyyyMMddHHmmss>`. The backup API is safe in WAL mode, unlike a raw file copy. At most the three newest backups of the same version are kept: a failing migration rolls back and Task Scheduler restarts the process, so without a cap every retry would copy the whole database (PII) again. Reusing an older backup was rejected because enabling WAL and rolling back both touch the `-wal` file, so file timestamps cannot tell whether the data changed. Old backups are not pruned automatically (a short note in `deploy/README.md` tells the operator to delete them once the upgrade is verified). Alternative: `VACUUM INTO` — equivalent but rewrites the file and is slower on large databases.

### D6. Migrations run before the host starts serving
`Program.cs` calls the migrator after `builder.Build()` and before `app.Run()`, but after the `--smoke-test` branch (that mode promises no side effects, so it must not migrate or back up). A failure throws and the process exits non-zero, so Task Scheduler's restart policy shows the error instead of serving a half-upgraded database. A database whose `user_version` exceeds the latest known version is refused (protects against running an old executable on a newer database).

### D7. Repository split along existing seams
- `IPersonRequestRepository` (cases): everything keyed on `PersonRequest`, including state transitions and the new queries (D8).
- `IBoxRepository`: `Box` table plus the operations whose subject is a box — `GetBoxes`, `FindBoxById`, `GetCasesByBoxId`, `CloseBox`, `ReopenBox`, `RemoveCaseFromClosedBox`. `SendToCaja` and `GetCajaQueue` stay with cases (they transition/read `PersonRequest` rows by `Destination`), keeping box code free of case-transition rules.
- `IMessageTombstoneRepository`: `RecordDeletedSourceMessage`, `IsSourceMessageDeleted`, `RecordProcessedBounce`, `IsBounceProcessed`.
Implementation classes share `SqliteConnectionSetup` and the `Map` helpers via small internal classes; no inheritance. Rollout is incremental: first extract the new interfaces while `PersonRequestRepository` still implements all three (callers untouched), then move each caller to the narrow interface one page at a time, then physically move the code. This keeps every commit green.
Alternative: one repository per table — `DeletedSourceMessage`/`ProcessedBounce` are two 5-line tables that are always used together by the same service, so one class is enough.

### D8. SQL-side queries as a small, typed query object
Add `CaseQuery` (optional `Destination`, `Statuses`, `Search`, `OnlyNeedsReview`, `OnlyBounced`, ordering) and `IPersonRequestRepository.Find(CaseQuery)`. Pages that filter by `Destination`/`Status` today (Index, F8, SinCarpetas, Subidas, Caja matches, sectors) switch to it. The sector (Archivo / Oficina 43) is deliberately **not** a query filter: it derives from `FechaUltimaCarpeta`, and a text comparison in SQL would misclassify any row stored in a non-ISO format that `DateOnly.Parse` tolerates. Sector pages push down `Marked`/`Transferred`/destination and keep `c.Sector == sector` in LINQ. The `Subidas` page merges two SQL slices and de-duplicates by id, since the slices are read at different instants. Free-text search (`MatchesQuery` in `Index`) keeps its exact normalization rules (accent/case-insensitive across name, RUT, comuna, code); where SQLite cannot reproduce them, the page pushes down only `Destination`/`Status` and keeps the in-memory text match on the reduced set. Each page migration is proven equivalent by a test that runs the old LINQ filter and the new query over the same fixture and compares ids and order. An index on `(Destination, Status)` is added as migration V2.
Alternative: expose `IQueryable`/dynamic SQL strings to pages — leaks SQL into the UI layer; rejected.

## Risks / Trade-offs

- [Upgrade corrupts or loses production data] → pre-migration backup (D5), one transaction per migration (D2), V1 verbatim from the current code, and a test that builds each historical layout (pre-`SoloCaja`, with the `UNIQUE` constraint, with `MovedToF8At`/`Certificado` rows) and asserts every row survives.
- [Behavior change from one-shot backfills (D4)] → documented in proposal/spec; V1 still performs them once on adoption, so the first restart after deploy equals today's behavior. Release notes mention it.
- [Backup files accumulate and hold PII] → `.gitignore` already excludes `**/data/*`; `deploy/README.md` instructs deleting backups after verification; backups sit next to the database with the same disk-encryption assumption already documented.
- [Large refactor touching many callers] → strictly incremental (D7), one caller group per task, full suite after each; no step changes more than one concern.
- [SQL filters diverge subtly from LINQ (accents, case, ordering)] → equivalence tests (D8); text search stays in memory when not trivially reproducible.
- [`user_version` visibility] → any SQLite client shows it; a failing runner includes the version in the exception message.

## Migration Plan

1. Release N: ships the migrator. First start on production: backup `router.db.bak-v0-…`, V1 (adoption + one-time backfills), V2 (index), `user_version = 2`.
2. Verify in the dashboard (case counts per screen match the pre-upgrade counts recorded in the task report), then delete the backup.
3. Rollback: stop the app, replace `data/router.db` with the backup (delete `-wal`/`-shm` files), run the previous executable.

## Open Questions

- Should old `.bak-v*` files be pruned automatically after N days? Deferred: manual deletion is enough for a single-operator tool and can be added later without changing specs.
