## Context

F8 already has two "PDF by group" pages that share one mechanism: `SectorF8Model` (`Dashboard/Pages/SectorF8.cshtml.cs`) filters `PersonRequest` rows by `Destination == F8` and a `FolderSector` (Archivo / Oficina 43), excludes rows already printed via `SectorPdfGeneratedAt is not null`, renders an HTML table styled for `@media print`, and offers `OnPostMarkPrinted` / `OnPostRemoveOne` / `OnPostClearAll` handlers that stamp `SectorPdfGeneratedAt` through `IPersonRequestRepository.SetSectorPdfGenerated`.

We are adding a third PDF view — "Penúltimas Carpetas" — with a fixed filter (no sector parameter) and a reduced column set (name, RUT, folder date only). The user has already decided this tab's "already printed" state must be tracked in its own column, independent of `SectorPdfGeneratedAt`, so that marking a case printed here does not hide it from Archivo/Oficina 43 and vice versa.

`PersonRequestRepository` (`Persistence/PersonRequestRepository.cs`) already has two migration mechanisms:
1. A one-time `PersonRequest_new` table rebuild used historically for larger schema reshaping (line ~150).
2. `EnsureColumnExists(connection, columnName, columnDefinitionSql)` (line ~198+) — checks `PRAGMA table_info(PersonRequest)` and issues `ALTER TABLE PersonRequest ADD COLUMN ...` only if the column is missing. This is the pattern used for `SectorPdfGeneratedAt` itself (line 102) and is the correct tool for a single additive nullable column.

## Goals / Non-Goals

**Goals:**
- Add a `PenultimasCarpetasPdfGeneratedAt DateTimeOffset?` column to `PersonRequest`, migrated additively via `EnsureColumnExists`, mirroring how `SectorPdfGeneratedAt` was added.
- Add `SetPenultimasCarpetasPdfGenerated(long id, DateTimeOffset? value)` to `IPersonRequestRepository` / `PersonRequestRepository`, mirroring `SetSectorPdfGenerated`.
- Add `PenultimasCarpetasF8Model` (`OnGet`, `OnPostMarkPrinted`, `OnPostRemoveOne`, `OnPostClearAll`) reusing the `SectorF8Model` structure, but: no `FolderSector` parameter, filter is `Destination == F8 && Marked == true && FechaUltimaCarpeta is not null && PenultimasCarpetasPdfGeneratedAt is null`, ordered by `MarkedAt`.
- Add `Dashboard/Pages/PenultimasCarpetasF8.cshtml` reusing `SectorF8.cshtml`'s print CSS/table structure, reduced to 3 columns (Nombre, RUT, Fecha última/penúltima carpeta).
- Add the subnav link in `F8.cshtml`.

**Non-Goals:**
- No PDF-generation library — still browser-print based, same as the two existing tabs.
- No change to `FolderSector` assignment logic, `SectorPdfGeneratedAt`, or the `SectorF8` page.
- No change to the PENDIENTE → SUBIDA → CONFIRMADO state machine.
- No re-migration or backfill of existing rows beyond the new column defaulting to `NULL` (which reads as "not yet printed", the correct default).

## Decisions

**1. Independent tracking column vs. reusing `SectorPdfGeneratedAt`.**
Reusing the existing column would mean printing this tab hides cases from Archivo/Oficina 43 (and vice versa) — explicitly rejected by the user, since the three tabs serve different physical workflows (retrieving the folder vs. requesting a penultimate-folder reprint) that can be in-flight independently for the same case. Decision: separate nullable `DateTimeOffset?` column, same type/semantics as `SectorPdfGeneratedAt` (null = not printed, timestamp = printed at that time, cleared back to null on `OnPostRemoveOne`'s inverse or when `Marked` is re-ticked — see decision 3).

**2. Migration mechanism: `EnsureColumnExists` (`ALTER TABLE ADD COLUMN`) vs. `PersonRequest_new` rebuild.**
The `_new`-table rebuild in this file is reserved for migrations that needed to reshape multiple columns/constraints at once (see the comment "Additive migration for databases created before multiple contributors per email"). A single new nullable column has no such requirement — SQLite supports `ALTER TABLE ... ADD COLUMN` for nullable columns natively, and `EnsureColumnExists` already implements the idempotent guard (`PRAGMA table_info` check) other single-column additions use (`SectorPdfGeneratedAt` itself was added this way). Decision: use `EnsureColumnExists`, no rebuild.

**3. Filter includes `PenultimasCarpetasPdfGeneratedAt is null`.**
The proposal's stated filter is `Destination == F8 && Marked == true && FechaUltimaCarpeta is not null`. To make the "independent print tracking" meaningful (mirroring `SectorF8Model`, which excludes `SectorPdfGeneratedAt is not null` rows from view), the page's `OnGet` filter also excludes rows where `PenultimasCarpetasPdfGeneratedAt is not null` — otherwise the new column would be written but never affect what's displayed, making it dead state. This mirrors the existing tab's UX: printed cases disappear from the list until re-marked. Re-ticking "Marcar" already clears `SectorPdfGeneratedAt` per the existing `Marked` setter (`Persistence/PersonRequestRepository.cs` line ~455); that setter is NOT touched by this change, so `PenultimasCarpetasPdfGeneratedAt` is only ever cleared explicitly (there is no existing hook to also null it on re-Marked without touching shared code shared by all three tabs) — flagged as an open question below.

**4. Column set reduction is view-only.**
No change to the `PersonRequest` domain/read model beyond the new tracking column — Comuna, F8 code, Sector, etc. still exist on the entity; `PenultimasCarpetasF8.cshtml` simply doesn't render them, same approach `SectorF8.cshtml` uses for its own column subset today (verify against current markup during implementation).

## Risks / Trade-offs

- [Risk] Three independent "printed" booleans on the same row could confuse operators about why a case appears on one PDF tab but not another → Mitigation: none needed at UI level per user's explicit decision; document this in the page's operator-facing label if ambiguity arises during review.
- [Risk] Additive `ALTER TABLE ADD COLUMN` on SQLite requires no default expression conflicts (nullable, no default) — safe, but must be verified against the exact syntax `EnsureColumnExists` builds for `SectorPdfGeneratedAt` to keep the two columns' SQL type consistent (`TEXT NULL`, since `DateTimeOffset` is stored as ISO-8601 text elsewhere in this repository).
- [Trade-off] Reusing the `SectorF8Model` structure duplicates action-handler boilerplate (`OnPostMarkPrinted`/`OnPostRemoveOne`/`OnPostClearAll`) rather than extracting a shared base class — consistent with the existing choice not to share code between `SectorModel` and `SectorF8Model`; kept for symmetry, not revisited in this change.

## Migration Plan

1. Add `PenultimasCarpetasPdfGeneratedAt TEXT NULL` to the `EnsureColumnExists` calls run at repository startup (same call site as `SectorPdfGeneratedAt`, `Persistence/PersonRequestRepository.cs` ~line 102).
2. No backfill needed — existing rows read `NULL` (not printed), which is the correct default for a column added after those rows were created.
3. Rollback: dropping a SQLite column requires a table rebuild; since the column is purely additive and unused if the feature is reverted, rollback is simply "stop reading/writing the column" (leave it in place) — no destructive rollback step planned.

## Open Questions

- Should re-ticking "Marcar" on a case also clear `PenultimasCarpetasPdfGeneratedAt` (mirroring how it already clears `SectorPdfGeneratedAt`)? The proposal's filter requires `Marked == true`, so if an operator un-marks and re-marks a case that was already printed on this tab, leaving `PenultimasCarpetasPdfGeneratedAt` set would hide it again with no path back except `OnPostRemoveOne`'s not-yet-defined inverse. Resolve during `sdd-tasks`/implementation — default assumption unless the user says otherwise: yes, mirror the existing `Marked` setter's clearing behavior for consistency, applied to the new column as well.
