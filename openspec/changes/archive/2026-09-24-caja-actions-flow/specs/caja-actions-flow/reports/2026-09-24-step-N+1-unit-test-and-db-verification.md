# Step N+1 Report - Unit Tests and Database Verification

- Date: 2026-09-24
- Change: caja-actions-flow
- Agent: Claude Code (Opus)

## Commands Executed
- `dotnet test --filter PersonRequestRepositoryTests`
- `dotnet test --filter "FullyQualifiedName~IndexModelTests|FullyQualifiedName~F8ModelTests"`
- `dotnet test` (full suite)

## Unit Test Results
- Baseline before the change: 334 passed, 0 failed
- Targeted tests (repository + Index/F8 page models): all passed
- Full suite: 349 passed, 0 failed, 0 skipped
- Runtime: ~25 s
- Notes: the unit tests use throwaway SQLite files under %TEMP%; they never open the production database.

## Spec Scenario Coverage (task 7.2)
| Scenario | Test |
|---|---|
| Confirmed case sent to Caja | `IndexModelTests.OnPostSubirACaja_ConfirmedCaseWithoutFecha_MovesToCajaAndLeavesCasos` |
| Pending case cannot be sent to Caja | `PersonRequestRepositoryTests.SendToCaja_PendingCaseWithoutSoloCaja_IsNotMoved` |
| F8 case sent to Caja | `SendToCaja_F8Case_MovesToQueueAsConfirmed`, `F8ModelTests.OnPostSendToCaja_MovesF8CaseToCajaQueue` |
| F8 case closed without folder | `CloseWithoutFolder_F8Case_ClosesAndReturnsToCasos`, `F8ModelTests.OnPostCloseWithoutFolder_ClosesCaseAndRemovesItFromF8` |
| Closed case never enters Caja | `SendToCaja_ClosedWithoutFolder_IsNotMoved` |
| Revert F8 not yet uploaded | `RevertF8AndReturnToCasos_NotUploadedF8_ReachesSameState` |
| Revert F8 already uploaded | `RevertF8AndReturnToCasos_UploadedF8_ClearsAllF8Data`, `F8ModelTests.OnPostRevertToCasos_ClearsF8DataAndReturnsAsSoloCaja` |
| Reverted case sent to Caja | `RevertF8AndReturnToCasos_ReincorporatesCaseAsSoloCaja` |
| Queued case returned | existing `Caja.OnPostUndoSingle` (`ClearDestination`), verified in E2E flow A |
| Case in Caja hidden from Casos | `OnPostSubirACaja_ConfirmedCaseWithoutFecha_MovesToCajaAndLeavesCasos` |
| Closed-without-folder case visible in Casos | `IndexModelTests.OnGet_ClosedWithoutFolderCase_StaysVisibleInCasos` |
| Action button presentation / per-state actions | E2E (see the E2E report) |

## Database State Verification (production `publish/data/router.db`)
- Pre-change baseline: 361 PersonRequest (Caja 24, F8 42, None 295), 2 Box
- Post-deploy: 361 PersonRequest (Caja 24, F8 42, None 295), 2 Box, plus the new column `ClosedWithoutFolderAt` (all NULL)
- State restored: not needed; the tests never touched production data
- Backups taken before each deploy:
  - `router.db.backup-20260924-165820-before-search-fix`
  - `router.db.backup-20260924-171133-before-caja-actions`

## Outcome
- Step N+1 status: PASS
- Blocking issues: none
