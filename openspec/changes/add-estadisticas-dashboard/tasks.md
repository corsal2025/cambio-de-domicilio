## 0. Setup: Create Feature Branch (MANDATORY — FIRST STEP)

- [x] 0.1 Create `feature/add-estadisticas-dashboard` from `master`
- [x] 0.2 Verify current branch is `feature/add-estadisticas-dashboard`

## 1. Statistics Service — Simple Aggregations (TDD)

- [x] 1.1 RED: `StatisticsServiceTests` — `GetStatusCounts` returns correct Pending/Uploaded/Confirmed counts over a fixture list of `PersonRequest`
- [x] 1.2 GREEN: `IStatisticsService`/`StatisticsService.GetStatusCounts(IReadOnlyList<PersonRequest>)`
- [x] 1.3 RED: `GetWeeklyIntake` buckets `ReceivedAt` by ISO week, including zero-count weeks between the earliest and latest date
- [x] 1.4 GREEN: `GetWeeklyIntake`
- [x] 1.5 RED: `GetTopComunas` returns up to 10 comunas ordered by count descending; fewer than 10 distinct comunas yields no placeholder entries
- [x] 1.6 GREEN: `GetTopComunas`
- [x] 1.7 RED: `GetDiscardedByReason` groups `DiscardedEmail` rows by reason, stable across repeated calls with unchanged data
- [x] 1.8 GREEN: `GetDiscardedByReason`

## 2. Statistics Service — Derived/Business-Rule Aggregations (TDD)

- [x] 2.1 RED: `GetAverageTurnaroundDays` counts only `Status == Confirmed` cases (both `ReceivedAt`/`ConfirmedAt` present); a mix of Pending/Uploaded/Confirmed cases excludes the non-Confirmed ones from the average
- [x] 2.2 RED: `GetAverageTurnaroundDays` returns an explicit "no data" result (not zero, not a division-by-zero exception) when zero cases are Confirmed
- [x] 2.3 GREEN: `GetAverageTurnaroundDays`
- [x] 2.4 RED: `GetSectorDistribution` counts Archivo vs Oficina43 only among cases with `FechaUltimaCarpeta` set; cases without a fecha are excluded from both buckets
- [x] 2.5 GREEN: `GetSectorDistribution`
- [x] 2.6 RED: `GetF8DeadlineBacklog` buckets `Destination == F8` cases into within-deadline vs past-deadline using `DeadlineCalculator.AddBusinessDays`/`BusinessDaysRemaining` and `RouterOptions.PlazoDiasHabiles` — same rule as the `restantes < 0` badge on `/F8`
- [x] 2.7 GREEN: `GetF8DeadlineBacklog`
- [x] 2.8 RED: `GetF8PdfStatus` splits F8 cases with an assigned sector into `SectorPdfGeneratedAt` set vs not set
- [x] 2.9 GREEN: `GetF8PdfStatus`
- [x] 2.10 RED: `GetCertificadoFolderStatus` splits `Destination == Certificado` cases by `FolderNotFound`
- [x] 2.11 GREEN: `GetCertificadoFolderStatus`
- [x] 2.12 RED: `GetCertificadoNotificationStatus` splits `Destination == Certificado` cases by `CertificadoNotifiedAt` set vs null
- [x] 2.13 GREEN: `GetCertificadoNotificationStatus`

## 3. Dashboard Page: /Estadisticas

- [x] 3.1 RED: `EstadisticasModelTests` — page requires `[Authorize]` (verified via attribute reflection; end-to-end redirect behavior confirmed against the running app in Step 6.2 with a real unauthenticated `curl` request → 302 to `/Login`)
- [x] 3.2 RED: `EstadisticasModelTests.OnGet` populates every DTO from `IStatisticsService` calls, called with `repository.GetAll()`/`discardedRepository.GetAll()`
- [x] 3.3 GREEN: `Dashboard/Pages/Estadisticas.cshtml.cs` — `EstadisticasModel` wiring `IStatisticsService` + both repositories, `[Authorize]` (matches existing page pattern)
- [x] 3.4 GREEN: `Dashboard/Pages/Estadisticas.cshtml` — page shell, header/nav matching existing pages, one `<canvas>` per chart, JSON-serialized DTOs (camelCase) in an inline `<script type="application/json">` block
- [x] 3.5 Vendor `wwwroot/js/vendor/chart.umd.js` (Chart.js 4.4.7 UMD build, MIT license, unminified — no build step in this project, no CDN reference anywhere in the page)
- [x] 3.6 `wwwroot/js/estadisticas.js` — one Chart.js instantiation per chart (donut ×4: status, sector, Certificado-folder, Certificado-notification; bar ×4: top-comunas, discarded-by-reason, F8-deadline, F8-pdf-status; line ×1: weekly intake; bar ×1: turnaround average); reuses `wwwroot/js/scroll-preserve.js`
- [x] 3.7 Add `<a asp-page="/Estadisticas" class="nav-estadisticas">` nav link (own violet pill style, matching the existing Casos/F8/Certificado pattern) to `Index.cshtml`, `F8.cshtml`, `Certificado.cshtml`, `Discarded.cshtml`, `Comunas.cshtml` headers

## 4. Backend: Review and Update Existing Unit Tests (MANDATORY)

- [x] 4.1 Reviewed existing repository/service test fixtures — no changes needed; new DTOs' edge cases (empty repository, single row, all-Pending, all-Confirmed, zero-Confirmed) are covered by new `StatisticsServiceTests` cases directly
- [x] 4.2 Confirmed no existing test's behavior changed as a side effect of this change — purely additive, zero regressions (328/328 passing, up from 314 pre-existing)

## 5. Backend: Run Unit Tests and Verify Database State (MANDATORY — AGENT MUST EXECUTE)

- [x] 5.1 Confirmed all new tests use in-memory fixtures or temp SQLite files, never `data/router.db`
- [x] 5.2 Ran targeted unit tests: `dotnet test --filter "FullyQualifiedName~StatisticsService"` — 12 passed
- [x] 5.3 Ran full suite: `dotnet test` — 328 passed, 0 failed
- [x] 5.4 Verified no test touches or mutates `data/router.db` (grepped test fixtures for hardcoded prod paths — none found)
- [x] 5.5 Created report `openspec/changes/add-estadisticas-dashboard/reports/2026-07-28-step-5-unit-test-and-db-verification.md`
- [x] 5.6 Step marked complete — tests pass, report exists

## 6. Manual Verification with curl (MANDATORY — AGENT MUST EXECUTE)

- [x] 6.1 Ran the built app locally (Development environment, matching `deploy/abrir-dashboard-dev.ps1`'s launch pattern)
- [x] 6.2 `curl -sk https://localhost:5001/Estadisticas` without a session cookie → confirmed `302` redirect to `/Login?ReturnUrl=%2FEstadisticas`
- [x] 6.3 Authenticated via real `/Login` form POST (CSRF token extracted from the page, cookie jar) then `GET /Estadisticas` → confirmed `200` and all 10 expected `<canvas id="...">` elements present in the response body
- [x] 6.4 Documented both curl commands and responses in the Step 5/7 reports (read-only screen — no POST/PUT/DELETE endpoints, nothing to restore)

## 7. E2E Testing with Playwright (MANDATORY — AGENT MUST EXECUTE)

- [x] 7.1 Reused the Playwright script pattern from the earlier scroll-jump fix session (`chromium.launch` + `ignoreHTTPSErrors`, real operator login)
- [x] 7.2 Navigated to `/Estadisticas`, waited for network idle — confirmed zero console/page errors
- [x] 7.3 Screenshotted the full page — found and fixed a real bug (PascalCase/camelCase JSON mismatch left every chart blank), re-verified with a second screenshot showing all 10 charts rendering real data
- [x] 7.4 Clicked the `/Estadisticas` nav link from Index, F8, Certificado, Discarded, and Comunas — confirmed correct navigation from each
- [x] 7.5 Confirmed no data mutation occurs on this screen — documented explicitly in the E2E report
- [x] 7.6 Documented scenarios and outcomes in `openspec/changes/add-estadisticas-dashboard/reports/2026-07-28-step-7-e2e-verification.md`, including two user-requested visual-polish fixes (nav pill styling, full-width chart for long discard-reason labels)

## 8. Documentation (MANDATORY)

- [x] 8.1 Added `/Estadisticas` to `docs/flujo-proceso.md` as PASO 8, a non-blocking reporting layer over the existing PENDIENTE→SUBIDA→CONFIRMADO flow
- [x] 8.2 Added a "Read-only aggregation (Estadísticas)" section to `docs/data-model.md`
- [x] 8.3 Updated `docs/reporte-tecnico.md` §7 (components table) to list the Estadísticas screen and the vendored Chart.js dependency
- [x] 8.4 Added a 6th Mermaid diagram to `docs/diagrama-sistema.md` showing the statistics screen's data sources across all four processes
