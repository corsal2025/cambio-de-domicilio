## Context

Uncommitted work on this branch already provides part of the Caja flow: `Destination.Caja`, `BoxId`, `SoloCaja`, `SendToCaja`, `RevertF8AndReturnToCasos`, `OnPostSubirACaja` (Index, no button wired), and `OnPostUndoSingle`/`OnPostUndoBatch` (Caja). F8 has two revert handlers (`UndoTransfer` = only clears destination; `RevertToCasos` = resets state but keeps `CodigoF8`). Casos hides any case with `TransferredAt` set. `SinCarpeta` already means "operator typed S/C instead of a date" and filters Caja queries.

## Goals / Non-Goals

**Goals:** explicit Caja action in Casos and F8; "Sin carpeta" closure; one F8 revert that clears all F8 data; "Devolver a casos" in Caja; icon-based action buttons.

**Non-Goals:** email changes, audit trail of reverts, box close/reopen changes, new filters.

## Decisions

1. **New field `ClosedWithoutFolderAt` (nullable timestamp), not reuse `SinCarpeta`.**
   `SinCarpeta` has existing meaning and data; reusing it would retroactively mark S/C cases as closed and they are already excluded from Caja by `SinCarpeta = 0`. Timestamp over bool: same cost, gives when-closed for statistics. Alternative rejected: new `RequestStatus.ClosedWithoutFolder` — would ripple into statistics, filters and routing switch statements.

2. **`SendToCaja` guard extended**: allow `Destination = F8` cases, deny `ClosedWithoutFolderAt IS NOT NULL`. Keeps one entry point for all three sources (Casos uploaded, Casos SoloCaja, F8). Status for F8 cases: set to `Confirmed` like SoloCaja today, so a case returned from Caja shows the Caja action again (Uploaded/Confirmed rule).

3. **`RevertF8AndReturnToCasos` becomes the unified revert** and additionally clears `CodigoF8`, `UploadedAt`. `ClearFechaUltimaCarpeta` not needed: F8 transfer already cleared it. Remove `OnPostUndoTransfer` and its button. Keep repository method name to limit churn.

4. **`CloseWithoutFolder(id, closedAt)`** repository method: sets `ClosedWithoutFolderAt`, `Destination = None`, `TransferredAt = NULL`. Case reappears in Casos because Casos filters on `TransferredAt`.

5. **"Devolver a casos" reuses `OnPostUndoSingle`** (already `ClearDestination`); only relabel/restyle the button.

6. **Icons: inline SVG in a shared partial `_ActionIcon.cshtml`** (param: icon name). No CDN, works offline on municipal LAN, single place to change. CSS classes `btn-action btn-action--upload|caja|sin-carpeta|revert|delete` in `dashboard.css`.

## Risks / Trade-offs

- [Revert wipes `CodigoF8` irreversibly] → confirm() dialog on the button, stated in text.
- [Schema change on production SQLite] → `EnsureColumnExists` pattern already used; additive nullable column, no data migration.
- [Existing uncommitted changes unreviewed] → implement on top; first task runs full test suite as baseline.

## Migration Plan

Additive column via `EnsureColumnExists`. Rollback: old binary ignores the column.

## Open Questions

None.
