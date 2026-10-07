# Step 5 — Unit tests, database state and upgrade rehearsal

Date: 2026-10-07. Executed by the agent (not delegated).

## Unit tests

| Point | Passing | Failed |
|---|---|---|
| Baseline before this change (task 0.2) | 402 | 0 |
| After group 1 (migrator) | 415 | 0 |
| After group 2 (baseline V1, startup wiring, test helper) | 420 | 0 |
| After group 3 (tombstone + box repositories) | 423 | 0 |
| After group 4 (CaseQuery, V002, page migration) | 449 | 0 |

Final command: `dotnet test -c Release` → `Passed! Failed: 0, Passed: 449, Skipped: 0, Total: 449`.
Release build: 0 warnings, 0 errors.

### Existing tests that were edited (task 5.1)

No test changed meaning. All edits are one of:

- **Fixture construction only** (`repository.EnsureSchema()` → `TestDatabase.Migrate(dbPath)`; extra constructor
  argument for the new tombstone/box repositories): 14 files.
- **Legacy-layout tests in `PersonRequestRepositoryTests`**: renamed `EnsureSchema_*` → `Baseline_*` and now call
  `TestDatabase.RerunBaseline(dbPath)` (resets `user_version` to 0 and runs the migrator) instead of `EnsureSchema()`.
  Assertions untouched; they still prove the legacy table rebuild keeps `BoxId`, `Marked`, `SectorPdfGeneratedAt`, etc.
- **One test moved**: `RecordProcessedBounce_IsIdempotentAndQueryable` → `MessageTombstoneRepositoryTests` (same assertions).
- `SqliteConnectionSetupTests`: concurrent-write test now uses `MessageTombstoneRepository` (the method it exercised moved).

No test reads or writes `data/router.db`: every database is a temp file created through `TestDatabase` and removed in `Dispose`
(grep for `router.db` in `tests/` only finds string literals in `RouterOptionsValidatorTests`, never opened).

## Upgrade rehearsal on a representative legacy database (tasks 5.2, 5.3, 5.5)

Method:

1. Built the **previous release** (commit `5f4b660`, before this change) in a git worktree and started it once on an empty
   path, so the schema was created by the old `EnsureSchema` (`user_version = 0`).
2. Populated that database with 480 cases covering the cross product of destination (5) × status (3) × needs-review ×
   sin-carpeta × closed-without-folder × bounced × marked, with last-folder dates on both sides of the sector cutoff, 3 boxes
   (60 cases packed), 5 deleted-source tombstones, 5 processed bounces and 7 discarded emails.
3. Copied it twice and started the **old** release on one copy and the **new** release on the other, same configuration.

Results:

| Check | Result |
|---|---|
| New release on the legacy copy | log: `Database upgraded from schema v0 to v2; backup: …new.db.bak-v0-20261007004614`; `user_version = 2` |
| `PersonRequest`, `Box`, `DeletedSourceMessage`, `ProcessedBounce`, `DiscardedEmail` after startup, old vs new | **identical, row for row, column for column** (480 / 3 / 5 / 5 / 7 rows). 321 of the 480 case rows were changed by the one-time backfills, identically by both releases |
| HTML of 22 URLs (Casos with every filter and search, F8, Caja, Caja box detail, SinCarpetas, Subidas, Sector ×2, SectorF8 ×2, Estadísticas, Discarded, Comunas) fetched from old and new app, antiforgery tokens normalized | **22 / 22 byte-identical** |
| Pages are not trivially empty | e.g. F8 24 case rows, SinCarpetas 384, Subidas 40, Sector/Archivo 4, Discarded 7 |
| HTTP status of Index, F8, Caja, SinCarpetas, SubidasASistema, Sector/Archivo, Estadisticas on the new release | 200 each |
| Second start of the new release | no `Database upgraded` log line, no new backup (exactly one `.bak-v0-…` exists), `user_version` stays 2 |

### The one intended behavior difference (design D4, owner decision)

After the first start, a case inserted as `Uploaded` with destination `None` (awaiting confirmation in Casos):

| Release | After the next restart |
|---|---|
| Old | silently moved to destination `Subidas` (backfill re-run on every startup); not shown in Casos |
| New | stays in `Casos` (destination `None`), still shown with its confirm action |

## E2E smoke with Playwright (task 5.4)

Chromium (preinstalled), new release in `Development` on the migrated copy:

```
/                  status=200 rows=9   consoleErrors=0
/F8                status=200 rows=24  consoleErrors=0
/Caja              status=200 rows=3   consoleErrors=0
/SinCarpetas       status=200 rows=384 consoleErrors=0
/SubidasASistema   status=200 rows=41  consoleErrors=0
/Sector/Archivo    status=200 rows=4   consoleErrors=0
/SectorF8/Archivo  status=200 rows=6   consoleErrors=0
/Estadisticas      status=200 rows=0   consoleErrors=0
/Discarded         status=200 rows=7   consoleErrors=0
search "CASO SUBIDO" in Casos finds the unconfirmed Uploaded case: true
```

Screenshots: `casos-after-migration.png`, `caja-after-migration.png`.

Note: running the published DLL under an environment name other than `Development` does not serve `wwwroot`
(static web assets 404). That is how the harness first ran, it happens identically with the old release, and it is not
a regression; the smoke above uses `Development`.

## Restore rehearsal (task 2.6)

The `deploy/README.md` restore procedure (copy `.bak-v*` over the database, delete `-wal`/`-shm`) was executed on a scratch
SQLite file: after the restore the file held the pre-upgrade row count again.

## Adversarial review (task 5.6) and re-verification

Findings fixed (artifacts updated first, then code, tests first):

| Severity | Finding | Fix |
|---|---|---|
| Major | A failing migration + Task Scheduler restarts copied the whole database (PII) on every retry | Keep at most the 3 newest backups per version (spec scenario added; test `Migrate_RepeatedFailedAttempts_KeepAtMostThreeBackups_IncludingTheNewest`) |
| Major | A failing `Rollback()` replaced the migration error with an unrelated exception (reproduced RED: `InvalidOperationException` escaped) | Rollback guarded; original error reported (`Migrate_FailureWhileRollingBack_StillReportsTheOriginalError`) |
| Major | `CaseQuery.Sector` compared dates as text in SQL; a row in a non-ISO format that `DateOnly.Parse` accepts would be misclassified | Filter removed; sector derived in LINQ as before (design D8, docs updated) |
| Minor | Subidas merged two queries read at different instants; a case moved in between could appear twice | `DistinctBy(Id)` |
| Minor (docs) | `deploy/README.md` claimed an old executable refuses a migrated database; pre-versioning executables do not | Rewritten: rollback must restore the backup; first-upgrade backfill effect and backup retention documented |

Final state: 450 tests passing, Release build 0 warnings / 0 errors.

Re-run of the rehearsal with the final build on a fresh copy of the legacy database: 22 / 22 URLs identical to the **previous
release** after normalizing the two clock fields the sector/SinCarpetas print headers contain (folio stamp and "Generado" time).

Accepted gaps (Minor / question, not blocking):

- Backup failure path (disk full) aborts startup by construction (the exception propagates before any migration), but has no dedicated test.
- No golden-schema test guarding `V001_Baseline` against accidental edits; policy is documented in `docs/data-model.md`.
- First upgrade moves currently-unconfirmed Uploaded cases to Subidas once (same as one restart of the old release); documented in `deploy/README.md`.
