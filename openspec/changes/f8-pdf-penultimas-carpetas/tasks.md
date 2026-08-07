## 1. Schema migration — PenultimasCarpetasPdfGeneratedAt

- [ ] 1.1 Write a failing test in `PersonRequestRepositoryTests` asserting a fresh/legacy SQLite database gets a `PenultimasCarpetasPdfGeneratedAt` column via the additive migration (mirror the existing `SectorPdfGeneratedAt` migration test, if present).
- [ ] 1.2 Add `EnsureColumnExists(connection, "PenultimasCarpetasPdfGeneratedAt", "PenultimasCarpetasPdfGeneratedAt TEXT NULL")` alongside the existing `SectorPdfGeneratedAt` call in `Persistence/PersonRequestRepository.cs`.
- [ ] 1.3 Add `PenultimasCarpetasPdfGeneratedAt` to `PersonRequest_new`'s column list and the `INSERT INTO ... SELECT` column lists, so a full rebuild (if ever triggered) preserves the column too.
- [ ] 1.4 Add `PenultimasCarpetasPdfGeneratedAt` to the `PersonRequest` domain model / read DTO, mirroring `SectorPdfGeneratedAt`.
- [ ] 1.5 Map `PenultimasCarpetasPdfGeneratedAt` in the row-reading code (mirror line ~583's `SectorPdfGeneratedAt` mapping).
- [ ] 1.6 Run the test from 1.1 and confirm it passes.

## 2. Repository setter — SetPenultimasCarpetasPdfGenerated

- [ ] 2.1 Write a failing test asserting `SetPenultimasCarpetasPdfGenerated(id, value)` persists the given `DateTimeOffset?` and leaves `SectorPdfGeneratedAt` untouched (and vice versa for the existing setter), covering the independence requirement from `specs/f8-penultimas-carpetas-pdf/spec.md`.
- [ ] 2.2 Add `SetPenultimasCarpetasPdfGenerated(long id, DateTimeOffset? value)` to `IPersonRequestRepository`.
- [ ] 2.3 Implement `SetPenultimasCarpetasPdfGenerated` in `PersonRequestRepository`, mirroring `SetSectorPdfGenerated`'s `UPDATE ... WHERE Id = $id` pattern.
- [ ] 2.4 Decide and implement whether re-ticking `Marked` also clears `PenultimasCarpetasPdfGeneratedAt` (see design.md Open Questions) — write the test first, then update the existing `Marked` setter accordingly.
- [ ] 2.5 Run the tests from 2.1 and 2.4 and confirm they pass.

## 3. Filter and ordering logic (case selection)

- [ ] 3.1 Write a failing test for the Penúltimas Carpetas selection query/LINQ filter: `Destination == F8 && Marked == true && FechaUltimaCarpeta is not null && PenultimasCarpetasPdfGeneratedAt is null`, ordered by `MarkedAt`.
- [ ] 3.2 Write failing tests for exclusion cases: unmarked case with a folder date is excluded; marked case without a folder date (S/C) is excluded; a case with `PenultimasCarpetasPdfGeneratedAt` already set is excluded.
- [ ] 3.3 Implement the filter (in `PenultimasCarpetasF8Model.OnGet`, following task group 4) so the tests from 3.1-3.2 pass.

## 4. PenultimasCarpetasF8 page

- [ ] 4.1 Write failing tests for `PenultimasCarpetasF8Model` (`OnGet`, `OnPostMarkPrinted`, `OnPostRemoveOne`, `OnPostClearAll`), mirroring `SectorF8ModelTests`, asserting: no sector parameter, correct filter/order per group 3, and that the post handlers call `SetPenultimasCarpetasPdfGenerated` (not `SetSectorPdfGenerated`).
- [ ] 4.2 Create `Dashboard/Pages/PenultimasCarpetasF8.cshtml.cs` with `PenultimasCarpetasF8Model : PageModel`, `OnGet()` (no `FolderSector` param), `OnPostMarkPrinted()`, `OnPostRemoveOne(long id)`, `OnPostClearAll()`, using the filter from group 3 and `SetPenultimasCarpetasPdfGenerated`.
- [ ] 4.3 Create `Dashboard/Pages/PenultimasCarpetasF8.cshtml`, adapting `SectorF8.cshtml`'s table/`@media print`/`window.print()` markup to render only Nombre completo, RUT, and Fecha última/penúltima carpeta columns.
- [ ] 4.4 Run the tests from 4.1 and confirm they pass.

## 5. F8 subnav integration

- [ ] 5.1 Write a failing test (or extend an existing `F8ModelTests`/integration test, if the subnav is covered by tests) asserting the F8 page exposes a link to `/PenultimasCarpetasF8`.
- [ ] 5.2 Add `<a asp-page="/PenultimasCarpetasF8">PDF Penúltimas Carpetas</a>` to the subnav in `Dashboard/Pages/F8.cshtml` near the existing "PDF Archivo" / "PDF Oficina 43" links (lines ~33-34), following the same styling/color convention used for the other two links.
- [ ] 5.3 Run the test from 5.1 and confirm it passes.

## 6. End-of-month pending-print warning

- [ ] 6.1 Write failing tests for a `PendingPenultimasCarpetasMonthsQuery` (or similar service) that, given "today", returns the list of calendar months (e.g. `2026-07`) with at least one F8 case where `UploadedAt` falls in that month, `Marked = true`, `FechaUltimaCarpeta` is not null, and `PenultimasCarpetasPdfGeneratedAt` is null — covering: single pending month, multiple pending months, no pending months, and a month where all eligible cases are already printed.
- [ ] 6.2 Implement the query/service in the appropriate layer (e.g. `Statistics` or a new small service alongside `IPersonRequestRepository`), following existing repository query patterns.
- [ ] 6.3 Write failing tests for a shared `PendingPrintWarningViewComponent`/partial (or layout-level code-behind) that renders the banner text listing pending months in Spanish (e.g. "Debe imprimir las Penúltimas Carpetas de julio") when the query returns any month, and renders nothing when empty.
- [ ] 6.4 Wire the banner into `Dashboard/Pages/Shared/_Layout.cshtml` (or equivalent shared layout) so it appears on every authenticated dashboard page.
- [ ] 6.5 Run the tests from 6.1 and 6.3 and confirm they pass.

## 7. Full verification

- [ ] 7.1 Run the full test suite and confirm all 347+ existing tests plus the new tests added in groups 1-6 pass.
- [ ] 7.2 Manually verify (or via a Playwright/integration test if applicable) that printing on Penúltimas Carpetas does not remove cases from Archivo/Oficina 43 PDFs and vice versa.
- [ ] 7.3 Manually verify the end-of-month banner appears/disappears correctly by seeding test data with `UploadedAt` in a past month.
- [ ] 7.4 Update `docs/flujo-proceso.md` Paso 5 description to mention the third, sector-independent PDF button and the end-of-month warning, per the proposal's flujo mapping.
