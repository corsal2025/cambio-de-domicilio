## 0. Setup: Create Feature Branch (MANDATORY - FIRST STEP)

- [x] 0.1 Decide with the operator what to do with the uncommitted work on `system-cleanup-2026-09` (commit it there first, or carry it over) before branching
- [x] 0.2 Create feature branch `feature/caja-actions-flow` and verify it is the current branch
- [x] 0.3 Run the full test suite (`dotnet test`) as a baseline and record the results — baseline 2026-09-24: 334 passed, 0 failed

## 1. Domain + Schema: ClosedWithoutFolderAt (TDD)

- [x] 1.1 RED: repository test asserting that `ClosedWithoutFolderAt` round-trips (null by default, persisted when set)
- [x] 1.2 GREEN: add `PersonRequest.ClosedWithoutFolderAt`, the `EnsureColumnExists` column, and mapping in `Map`/`Insert`

## 2. Repository: CloseWithoutFolder (TDD)

- [x] 2.1 RED: test `CloseWithoutFolder` on an F8 case: `ClosedWithoutFolderAt` set, `Destination = None`, `TransferredAt = null`
- [x] 2.2 GREEN: implement `CloseWithoutFolder(id, closedAt)` in the interface and the repository

## 3. Repository: SendToCaja guards (TDD)

- [x] 3.1 RED: test that an F8 case (`Destination = F8`, `Pending`) is moved to the Caja queue with `Status = Confirmed`
- [x] 3.2 RED: test that a closed-without-folder case is NOT moved
- [x] 3.3 RED: test that a `Pending` case without `SoloCaja` is NOT moved (guard kept)
- [x] 3.4 GREEN: extend the `SendToCaja` WHERE/SET clauses

## 4. Repository: Unified F8 revert (TDD)

- [x] 4.1 RED: test that a revert on an uploaded F8 (`CodigoF8` set, `Confirmed`) clears `CodigoF8`, `ConfirmedAt` and `UploadedAt`, and sets `Pending`, `SoloCaja = true`, `Destination = None`
- [x] 4.2 RED: test that a revert on a not-uploaded F8 yields the same end state
- [x] 4.3 GREEN: extend `RevertF8AndReturnToCasos` to clear `CodigoF8` and `UploadedAt`

## 5. Pages: handlers

- [x] 5.1 F8: add `OnPostSendToCaja(id)` (calls `SendToCaja`) and `OnPostCloseWithoutFolder(id)`
- [x] 5.2 F8: remove `OnPostUndoTransfer` and its button; keep `OnPostRevertToCasos` as the single "Revertir" with a confirm() that states F8 data will be erased
- [x] 5.3 Index: extend `OnPostSubirACaja` to accept `Uploaded`/`Confirmed` cases (drop the fecha guard for them, keep it for `SoloCaja`), with the message "enviada a Caja"
- [x] 5.4 Caja: relabel `UndoSingle`, `UndoBatch` and `RemoveFromClosedBox` as "Devolver a casos"

## 6. UI: action columns + icon buttons

- [x] 6.1 Create the partial `Dashboard/Pages/Shared/_ActionIcon.cshtml` (it also includes confirm/f8/rectify/resolve icons) with inline SVGs: upload, caja, sin-carpeta, revert, return, delete
- [x] 6.2 Add `.btn-action` + modifier classes (upload blue, caja green, sin-carpeta amber, revert/return neutral, delete red on hover), with focus-visible ring and tooltip via `title` + `aria-label`, in `dashboard.css`
- [x] 6.3 Index action column: `SoloCaja` shows only Caja; `Uploaded`/`Confirmed` shows Caja (plus Confirmar/Rectificar as today); closed-without-folder shows the label "Cerrado sin carpeta" + delete only
- [x] 6.4 Index status cell: render the label "Cerrado sin carpeta" when `ClosedWithoutFolderAt` is set
- [x] 6.5 F8 action column: Caja, Sin carpeta, Revertir, delete
- [x] 6.6 Caja queue: "Devolver a casos" icon button
- [x] 6.7 Replace the existing text buttons (Marcar subida, Traspaso a F8, delete) with the icon style

## 7. Review and Update Existing Unit Tests (MANDATORY)

- [x] 7.1 Update tests that reference `UndoTransfer` or the old `RevertF8AndReturnToCasos` state, and the `SendToCaja` guard expectations
- [x] 7.2 Confirm every spec scenario in `specs/caja-flow` and `specs/dashboard` maps to at least one test

## 8. Run Unit Tests and Verify Database State (MANDATORY - AGENT MUST EXECUTE)

- [x] 8.1 Capture the pre-test baseline of the dev SQLite DB (PersonRequest counts by Destination/Status, Box count)
- [x] 8.2 Run the targeted tests (`dotnet test --filter PersonRequestRepositoryTests`)
- [x] 8.3 Run the full suite (`dotnet test`)
- [x] 8.4 Verify the post-test DB state matches the baseline; restore it if needed
- [x] 8.5 Create the report `specs/caja-actions-flow/reports/2026-09-24-step-N+1-unit-test-and-db-verification.md`

## 9. Manual Endpoint Testing with curl (MANDATORY - AGENT MUST EXECUTE)

- [x] 9.1 Start the app against a copy of the DB (never production data). The production app was stopped during the test because of the single-instance mutex; EWS was disabled on the copy
- [x] 9.2 GET `/`, `/F8`, `/Caja`: expect 200
- [x] 9.3 POST (with antiforgery token + cookie) `F8?handler=SendToCaja`, `F8?handler=CloseWithoutFolder`, `F8?handler=RevertToCasos`, `?handler=SubirACaja`, `Caja?handler=UndoSingle`, and verify the DB state after each
- [x] 9.4 Error case: nonexistent id and closed case sent to Caja, with no state change
- [x] 9.5 Restore the DB copy; document commands and responses in `specs/caja-actions-flow/reports/`

## 10. E2E Testing with the built-in browser (MANDATORY - AGENT MUST EXECUTE)

- [x] 10.1 Flow A: uploaded case, then Caja, then disappears from Casos and appears in the Caja queue, then Devolver a casos, then back in Casos with Caja
- [x] 10.2 Flow B: F8 case, then Sin carpeta, then Casos shows "Cerrado sin carpeta" with no actions
- [x] 10.3 Flow C: F8 case with CodigoF8, then Revertir, then Casos shows only Caja, then Caja, then in the queue
- [x] 10.4 Visual check of the icon buttons (tooltips, hover colors) at desktop and mobile width
- [x] 10.5 Restore the DB; document the scenarios in `specs/caja-actions-flow/reports/`

## 11. Update Technical Documentation (MANDATORY)

- [x] 11.1 `docs/flujo-proceso.md`: add the Paso 6 branches (Caja / Sin carpeta / Revertir F8 / Devolver a casos)
- [x] 11.2 Update the XML doc comments on `CaseDestination.Caja` (no longer automatic on upload) and `SoloCaja`
