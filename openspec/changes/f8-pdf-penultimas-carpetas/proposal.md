## Why

The operator currently generates PDFs for F8 cases grouped by sector (Archivo, Oficina 43), but there is no way to produce a printable list of cases whose folder ("carpeta") has already been marked and dated — independent of the sector split — for cases that need a penultimate-folder-date reference. Operators need a third, narrower PDF view limited to name, RUT, and folder date, without disturbing the print/hide state already tracked for the Archivo and Oficina 43 tabs.

## What Changes

- Add a new "PDF Penúltimas Carpetas" tab in the F8 subnav, alongside "PDF Archivo" and "PDF Oficina 43".
- Filter: `Destination == F8 && Marked == true && FechaUltimaCarpeta is not null`, ordered by `MarkedAt`. Excludes S/C (no-folder) and unmarked cases.
- Display/print only three columns: full name, RUT, folder date (no Comuna, F8 code, or Sector columns).
- **BREAKING (data)**: introduce a new nullable column `PenultimasCarpetasPdfGeneratedAt` on `PersonRequest`, tracked independently from the existing `SectorPdfGeneratedAt`, so printing this tab does not affect the "already printed" state used by Archivo/Oficina 43, and vice versa.
- Add `SetPenultimasCarpetasPdfGenerated(long id, DateTimeOffset? value)` to `IPersonRequestRepository` and its SQLite implementation.
- Add a new Razor page `Dashboard/Pages/PenultimasCarpetasF8.cshtml` + `PenultimasCarpetasF8Model`, following the existing `SectorF8`/`SectorF8Model` pattern (HTML table + `@media print` CSS + `window.print()`), but with a fixed filter (no sector parameter) and the reduced column set.
- Add a subnav link `<a asp-page="/PenultimasCarpetasF8">PDF Penúltimas Carpetas</a>` in `F8.cshtml`.
- Add an end-of-month pending-print warning: on the 1st of a new calendar month, if any F8 case uploaded (`UploadedAt`) in the immediately preceding month is `Marked = true`, has a `FechaUltimaCarpeta`, and has `PenultimasCarpetasPdfGeneratedAt = null`, show a persistent warning banner on every authenticated dashboard page naming the pending month(s). The banner only clears once every such case for that month has been printed (`PenultimasCarpetasPdfGeneratedAt` set); no dismiss/snooze action is provided.

## Non-goals

- No real PDF-generation library (QuestPDF, iText, etc.) is introduced — this follows the existing "print via browser" pattern used by Archivo/Oficina 43.
- No changes to the existing `SectorPdfGeneratedAt` tracking, `SectorF8` page, or the Archivo/Oficina 43 sector-assignment logic.
- No changes to the PENDIENTE → SUBIDA → CONFIRMADO case-state machine.
- No new confirmation-email behavior.

## Capabilities

### New Capabilities
- `f8-penultimas-carpetas-pdf`: printable list of marked F8 cases with a folder date, limited to name/RUT/date, tracked with its own independent print-state column; plus the end-of-month pending-print detection rule.

### Modified Capabilities
- `dashboard`: F8 subnav gains a third PDF tab; `PersonRequest` schema and `IPersonRequestRepository` gain an independent print-tracking field/setter for this new tab; shared layout gains a warning-banner slot fed by the pending-print check.

## Impact

- **Schema**: `PersonRequest` table gains `PenultimasCarpetasPdfGeneratedAt TEXT NULL`, added via the existing additive `EnsureColumnExists` (`PRAGMA table_info` + `ALTER TABLE ... ADD COLUMN`) migration mechanism in `Persistence/PersonRequestRepository.cs` — no `PersonRequest_new` table rebuild needed since it's a single nullable additive column.
- **Repository**: `IPersonRequestRepository` and `PersonRequestRepository` gain `SetPenultimasCarpetasPdfGenerated(long id, DateTimeOffset? value)`, mirroring the existing `SetSectorPdfGenerated`.
- **Domain model**: `PersonRequest` (or its read DTO) gains a `PenultimasCarpetasPdfGeneratedAt` property, mirroring `SectorPdfGeneratedAt`.
- **UI**: new files `Dashboard/Pages/PenultimasCarpetasF8.cshtml` and `.cshtml.cs`; `Dashboard/Pages/F8.cshtml` subnav updated (near lines 33-34).
- **Tests**: implementation phase (not this change's planning artifacts) must add tests for: the new filter (marked + FechaUltimaCarpeta not null, ordered by MarkedAt), the new repository setter, and the additive column migration, per this project's mandatory TDD.

## Flujo del proceso mapping

This change extends **Paso 5 — PDF por sector** in `docs/flujo-proceso.md`: today that step covers only the ARCHIVO/OFICINA 43 sector split. This adds a third, sector-independent PDF button ("Penúltimas Carpetas") usable once a case has both `Marked = true` and a folder date, without altering the ARCHIVO/OFICINA 43 sector assignment or its own "already printed" state.
