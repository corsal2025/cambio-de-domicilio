# Tasks

## 0. Setup (MANDATORY — FIRST STEP)

- [x] 0.1 Work on the session-designated branch `claude/confident-dirac-boeb8d` (stands in for `feature/refactor-persistence-migrations`); verify with `git branch --show-current`
- [x] 0.2 Record the baseline: run `dotnet test` and note the passing count in `reports/baseline.md` (expected ≥ 402, 0 failed) so later groups can prove no regression

## 1. Migration runner (TDD) — spec: version tracking, apply-once, atomic failure, backup

- [x] 1.1 RED: `SchemaMigratorTests` — fresh file database ends at the latest version and a second run applies zero migrations (verify the test fails because `SchemaMigrator` does not exist)
- [x] 1.2 GREEN: `Persistence/Migrations/IMigration`, `Migrations.All` (empty/test list injectable) and `SchemaMigrator.Migrate(connectionString)` reading/writing `PRAGMA user_version`; verify 1.1 passes
- [x] 1.3 RED+GREEN: pending migrations apply in ascending order, each exactly once; runner rejects a list with duplicate or non-contiguous versions (tests with fake migrations record call order)
- [x] 1.4 RED+GREEN: a migration that throws midway rolls back all its changes, leaves `user_version` unchanged and surfaces an exception naming the failing version; verify with a fake migration that creates a table then throws
- [x] 1.5 RED+GREEN: database with `user_version` greater than the latest known version is refused without modification (hash of the file unchanged)
- [x] 1.6 RED+GREEN: backup via `SqliteConnection.BackupDatabase` named `<db>.bak-v<version>-<timestamp>` is created before the first pending migration and only when one is pending; verify the backup opens and contains the pre-upgrade rows
- [x] 1.7 Verify concurrency guard: migrator takes `BEGIN IMMEDIATE`; test that a second connection writing during a migration waits/fails per `busy_timeout` instead of interleaving

## 2. Baseline migration V1 and legacy adoption — spec: adoption without data loss, backfills run once

- [ ] 2.1 RED: `LegacyDatabaseAdoptionTests` builds fixture databases for each historical layout (original schema with `UNIQUE` on `SourceMessageId`; schema before `SoloCaja`/`Destination`; rows with `MovedToF8At`, `Destination='Certificado'`, Uploaded/Confirmed with `Destination='None'`, closed-without-folder rows) and asserts every row/column value survives adoption
- [ ] 2.2 GREEN: `Migrations/V001_Baseline` containing the current `EnsureSchema` logic verbatim (tables `PersonRequest`, `DeletedSourceMessage`, `ProcessedBounce`, `Box`, `DiscardedEmail`; additive columns; `UNIQUE` rebuild; four backfills); verify 2.1 passes
- [ ] 2.3 RED+GREEN: backfills are one-shot — restarting an adopted database leaves an Uploaded/`None` case untouched, and adoption itself moves it to Subidas exactly once
- [ ] 2.4 Wire `Program.cs`: run `SchemaMigrator` after `Build()` and before `--smoke-test`/`app.Run()`; remove `EnsureSchema()` from `IPersonRequestRepository`, `IDiscardedEmailRepository`, `PersonRequestRepository`, `DiscardedEmailRepository` and `RouterWorker`; verify the solution builds
- [ ] 2.5 Add `TestDatabase` test helper (creates a temp file, runs the migrator, cleans `-wal`/`-shm`) and move the 12 test files that call `EnsureSchema()` onto it; verify full suite is green with the same test count as the baseline plus the new tests
- [ ] 2.6 Document in `docs/data-model.md` the versioning policy (how to add `V00N`, never edit a shipped migration, backfills belong to their own migration) and in `deploy/README.md` the backup/restore procedure; verify the documented restore steps against a scratch database

## 3. Split the repositories — one concern per commit

- [ ] 3.1 RED+GREEN: `IMessageTombstoneRepository` + implementation for the deleted-source and processed-bounce tables, with tests moved from `PersonRequestRepositoryTests`; `PersonRequestRepository` temporarily implements it by delegation so no caller changes; verify suite green
- [ ] 3.2 Move `AddressChangeRoutingService` (and its tests) to `IMessageTombstoneRepository`; remove the four tombstone methods from `IPersonRequestRepository`; verify build and tests
- [ ] 3.3 RED+GREEN: `IBoxRepository` + implementation for `GetBoxes`, `FindBoxById`, `GetCasesByBoxId`, `CloseBox`, `ReopenBox`, `RemoveCaseFromClosedBox` (transactions preserved); verify the existing Caja repository tests pass unchanged against the new class
- [ ] 3.4 Move `CajaModel` (and `CajaModelTests`) to `IBoxRepository`; remove box methods from `IPersonRequestRepository`; verify build and tests
- [ ] 3.5 Register the three interfaces in `Program.cs` as singletons over the same connection string; delete dead code left in `PersonRequestRepository`; verify `PersonRequestRepository.cs` is under 700 lines and the interface lists only case operations
- [ ] 3.6 Update the README "Estructura" section and `docs/data-model.md` repository boundaries; verify links and file names match the tree

## 4. SQL-side case queries — dashboard reads

- [ ] 4.1 RED: `CaseQueryTests` — `Find(CaseQuery)` by `Destination`, by `Statuses`, combined, with the same ordering the pages use today (verify against fixture data)
- [ ] 4.2 GREEN: `CaseQuery` record + `IPersonRequestRepository.Find` + migration `V002_CaseListIndex` (`CREATE INDEX IF NOT EXISTS IX_PersonRequest_DestinationStatus ON PersonRequest (Destination, Status)`); verify `EXPLAIN QUERY PLAN` uses the index in a test
- [ ] 4.3 Equivalence test harness: for a rich fixture, assert the legacy LINQ filter and `Find` return identical ids in identical order; apply to Index (Casos list and its search counts), then verify green
- [ ] 4.4 Switch `IndexModel` list + search-match counts to `Find`, keeping `MatchesQuery` text normalization in memory over the reduced set; verify `IndexModelTests` unchanged and green
- [ ] 4.5 Switch F8, SinCarpetas, SubidasASistema, Sector, SectorF8 and Caja queue reads to `Find`, one page per sub-step, each with its equivalence assertion; verify each page's model tests stay green
- [ ] 4.6 Keep `GetAll()` only for `Estadisticas`, `RouterWorker` CSV report and `Discarded` (whole-table by nature); verify with a grep that no other page calls it
- [ ] 4.7 Document the query object and when to use `Find` vs `GetAll` in `docs/data-model.md`; verify the examples compile (copy into a scratch test)

## 5. Integration checks (agent must execute)

- [ ] 5.1 Step "Review and Update Existing Unit Tests": confirm no existing test changed meaning (only fixture construction) and that no test touches `data/router.db`; list any test edited and why in the report
- [ ] 5.2 Step "Run Unit Tests and Verify Database State": run targeted tests per group, then `dotnet test` in Release; capture pre/post row counts of a copy of a representative database (cases per Destination/Status, boxes, tombstones) and confirm they match after adoption except for the documented one-time backfills; write `reports/step-5-unit-test-and-db-verification.md`
- [ ] 5.3 Manual endpoint testing with curl: start the app against the migrated copy and `curl` `/`, `/F8`, `/Caja`, `/SinCarpetas`, `/SubidasASistema`, `/Sector/Archivo`, `/Estadisticas`; confirm 200 and that rendered case counts equal the pre-upgrade counts from 5.2
- [ ] 5.4 E2E smoke with Playwright (Chromium preinstalled): load each page above on the migrated copy and confirm the main table renders with the expected row counts and no console errors; save screenshots under `reports/`
- [ ] 5.5 Upgrade rehearsal: run the application twice on a legacy fixture; confirm first start creates one backup and reaches the latest version, second start creates no backup and applies nothing (matches spec scenarios)
- [ ] 5.6 Run `/adversarial-review` on the migration runner and V1 before archiving; resolve findings by updating these artifacts first

## Workflow follow-up

- Archive the change (`/opsx:archive`) once 5.x are complete, syncing `specs/schema-migrations` into `openspec/specs/`.
- After deploying, delete the `.bak-v*` file next to `data/router.db` once the operator has verified the dashboards.
