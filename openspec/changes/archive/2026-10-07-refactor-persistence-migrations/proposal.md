# Proposal

## Why

`PersonRequestRepository.EnsureSchema` has grown into the project's only migration mechanism: ~15 `EnsureColumnExists` calls, four data backfills and a hand-written table rebuild, all re-executed on every startup, with the rebuild listing every column by hand (adding a column and forgetting it there silently drops data). The same 1,069-line class also owns four unrelated concerns (cases, boxes, deleted-email and bounce tombstones) behind a 35-method interface, and every dashboard page loads the whole table through `GetAll()` and filters it in LINQ. The database holds real PII with no versioning, so each new feature raises the risk of a bad upgrade on the production machine.

## What Changes

- Replace the ad-hoc `EnsureSchema` logic with **numbered, versioned migrations** tracked in `PRAGMA user_version`, each applied once inside its own transaction. Existing production databases (version 0 with the full legacy schema) are adopted by a baseline migration without data loss.
- Take a **file backup of the database before any pending migration runs**, and abort startup with a clear error (leaving the database untouched) if a migration fails.
- Split the persistence layer into focused repositories: cases (`PersonRequest`), boxes (`Box` and Caja queue operations), and message tombstones (`DeletedSourceMessage`, `ProcessedBounce`). Callers depend only on the interface they need.
- Add **SQL-side case queries** (by `Destination`, `Status`, and the searches the pages already perform) and move the dashboard pages and statistics off `GetAll()` + in-memory LINQ where the filter is expressible in SQL.
- Freeze the legacy table-rebuild (and its hand-written column list) inside the baseline migration, so it can no longer drift from later schema changes; later migrations use plain `ALTER`/`CREATE` statements.
- **BEHAVIOR**: the data backfills that today re-run on every startup (`SoloCaja` legacy flag, Uploaded/Confirmed → Subidas, SinCarpetas repair) run **once**, as part of the baseline migration. Restarting the app no longer silently moves an Uploaded case that is still awaiting confirmation out of Casos into Subidas, nor re-flags cases as `SoloCaja`. Decision confirmed with the owner on 2026-10-06.
- Update `docs/data-model.md` with the migration policy and the new repository boundaries.

Apart from the backfill change above, there are no user-visible changes: every page keeps showing the same cases in the same order, and no email flow changes.

Maps to `docs/flujo-proceso.md`: touches no business step; it protects persistence of every step (request intake, upload, confirmation, F8, Caja).

## Capabilities

### New Capabilities
- `schema-migrations`: how the application upgrades its SQLite database across versions — version tracking, apply-once ordering, pre-migration backup, atomic failure handling, and adoption of pre-versioning databases.

### Modified Capabilities
<!-- None: repository split and SQL filtering are implementation details with unchanged observable behavior. -->

## Non-goals

- No ORM, no new NuGet dependency (the project stays on `Microsoft.Data.Sqlite` with raw SQL).
- No schema redesign (no column renames or drops, no new tables beyond the version marker); existing columns stay as-is per the project's additive-schema convention.
- No change to email, routing, EWS or dashboard behavior, and no pagination UI.
- No change to the single-process / single-file deployment model.

## Impact

- **Code**: `Persistence/` (new `Migrations/` folder, new repository interfaces and classes, `PersonRequestRepository` slimmed), `Program.cs` (migration runner at startup, DI registrations), `RouterWorker`, `AddressChangeRoutingService`, all `Dashboard/Pages/*.cshtml.cs` and `StatisticsService` (constructor dependencies and queries).
- **Tests**: 12 test files construct `PersonRequestRepository` directly; they move to a shared test fixture. New tests for the migration runner (fresh, legacy, already-current, failing migration, backup).
- **Data**: production `data/router.db` is upgraded in place on first start; a `.bak` copy is written next to it. `.gitignore` already excludes `**/data/*`.
- **Docs**: `docs/data-model.md`, README structure section.
- **Rollback**: restore the pre-migration backup (documented in `deploy/README.md`).
