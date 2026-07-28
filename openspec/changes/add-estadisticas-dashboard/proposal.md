## Why

The operator and any manager who needs to report on this process today has no aggregate view of it — only row-by-row tables across four separate screens (Casos, F8, Certificado, Discarded). Answering "how many requests came in this month", "how much of F8's backlog is past its 15-business-day deadline", or "which comunas are being dropped because they're not in the directory" currently means counting rows by hand or exporting the CSV to a spreadsheet. A read-only statistics tab, built entirely from data the system already stores, closes that gap with no new data collection and no schema change.

## What Changes

- New Razor Pages screen `/Estadisticas`, reachable from the same top nav as Casos/F8/Certificado/Discarded/Comunas.
- New aggregation queries (in-memory LINQ over the existing repositories, same pattern `Index`/`F8` already use — no new SQL, no new tables):
  - Case counts by `Status` (Pending/Uploaded/Confirmed).
  - Weekly intake trend from `ReceivedAt`.
  - Top comunas by request volume.
  - Average days from `ReceivedAt`/registration to `ConfirmedAt` (turnaround time).
  - Sector split (Archivo vs Oficina43) among cases with a `FechaUltimaCarpeta`.
  - F8 backlog: within-deadline vs past the 15-business-day deadline (mirrors the deadline logic `F8.cshtml.cs` already renders per row — reused, not reimplemented).
  - F8 sector PDFs generated vs pending (`SectorPdfGeneratedAt`).
  - Certificado: `FolderNotFound` vs found; notified (`CertificadoNotifiedAt`) vs pending.
  - Discarded emails by reason/unrecognized domain — the actionable list for growing `data/comunas.csv`.
- Chart rendering via a single vendored file, `wwwroot/js/vendor/chart.umd.min.js` (Chart.js UMD build) — no CDN (the dashboard runs on an isolated municipal LAN), no build step (the project has none today).
- `EstadisticasModel.OnGet` computes every aggregate server-side and serializes them to the view as JSON for Chart.js to render — no client-side computation of business data.
- Reuses `wwwroot/js/scroll-preserve.js` for consistency with the other four screens.

## Non-goals

- No new data fields, no schema migration — every metric is derived from columns that already exist on `PersonRequest`/`DiscardedEmail`.
- No date-range filtering, export, or drill-down in this first version — all charts cover all-time data. Filtering is a natural follow-up once the base tab is validated with the operator.
- No changes to any existing screen's behavior, routing logic, or email sending — this is a read-only, additive reporting surface.
- No general-purpose charting framework or client-side state library — one vendored charting library, nothing else.

## Capabilities

### New Capabilities
- `statistics`: read-only aggregation and visualization of existing case/discarded-email data across all four operator-facing processes (Casos, F8, Certificado, Discarded).

### Modified Capabilities
(none — no existing capability's requirements change; `dashboard` capability is extended with a new screen but its existing requirements are untouched)

## Impact

- Affected code: new `Dashboard/Pages/Estadisticas.cshtml` + `.cshtml.cs`; new aggregation methods likely added to `IPersonRequestRepository`/`IDiscardedEmailRepository` or a small dedicated `StatisticsService`; nav links added to every existing dashboard page's header (`Index`, `F8`, `Certificado`, `Discarded`, `Comunas`); new vendored `wwwroot/js/vendor/chart.umd.min.js`.
- No new external dependency beyond the one vendored JS file (no NuGet package, no npm).
- No database migration — purely additive read queries.
- Tests: new unit tests for each aggregation (`StatisticsServiceTests` or repository-level tests), plus an `EstadisticasModelTests` for `OnGet` wiring.
- Maps to `docs/flujo-proceso.md`: extends the flow with a reporting step after PASO 7 ("Confirmación") — a new, non-blocking observability layer over the same PENDIENTE → SUBIDA → CONFIRMADO lifecycle, plus the F8/Certificado/Discarded tracks that already exist in the running system but predate that document's last update.
