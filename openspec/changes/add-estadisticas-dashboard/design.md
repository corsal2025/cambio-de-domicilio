## Context

The dashboard is four independent Razor Pages screens (Casos/Index, F8, Certificado, Discarded) plus Comunas, each computing its own view over the same `PersonRequest`/`DiscardedEmail` tables via `IPersonRequestRepository.GetAll()` / `IDiscardedEmailRepository`. There is no existing aggregation layer, no charting library, and no client-side JS framework anywhere in the project — every page is server-rendered Razor with small vanilla-JS enhancements (`scroll-preserve.js`, RUT auto-formatting, auto-refresh). Data volumes are small (hundreds to low thousands of rows), so every existing page already loads the full table into memory and filters with LINQ; this change follows that same pattern rather than introducing SQL-side aggregation.

The F8 screen already computes a per-row "días hábiles restantes" via `DeadlineCalculator.AddBusinessDays`/`BusinessDaysRemaining` (`F8.cshtml.cs:170-171`, `PlazoDiasHabiles` from `RouterOptions`). The statistics tab's F8-backlog chart reuses this exact calculator instead of reimplementing deadline math, so a future change to the deadline policy only has one place to update.

## Goals / Non-Goals

**Goals:**
- One new screen, `/Estadisticas`, showing the charts listed in `proposal.md`, built only from data already in SQLite.
- Aggregation logic covered by unit tests before any Razor/JS is written (TDD, per project standard).
- Charting with zero new build tooling and no internet dependency (LAN-only deployment).

**Non-Goals:**
- No filtering/date-range UI, no drill-down from a chart back to the underlying rows — v1 is all-time, read-only, no interaction beyond hover tooltips.
- No new repository interfaces per chart — aggregations live in one new service so the repositories stay focused on CRUD.

## Decisions

**1. New `IStatisticsService` / `StatisticsService`, not new repository methods.**
Alternative considered: add ~10 new methods directly to `IPersonRequestRepository`. Rejected — that interface is already large (CRUD + status transitions + F8/Certificado-specific setters) and none of its existing consumers need aggregation; a dedicated service keeps `PersonRequestRepository` focused and makes the aggregation logic independently unit-testable against a fixture list of `PersonRequest`, without needing a real SQLite connection for most tests (repository-level tests can still cover the two calls that hit `GetAll()`/`DiscardedEmailRepository.GetAll()`).

**2. Aggregation happens server-side in `EstadisticasModel.OnGet`, serialized to JSON for the view.**
Chart.js needs plain arrays of labels/values. `OnGet` calls `StatisticsService`, builds a small set of DTOs (e.g., `record StatusCounts(int Pending, int Uploaded, int Confirmed)`), and the Razor view serializes them with `System.Text.Json` into inline `<script>` blocks consumed by per-chart JS, same shape as how F8's existing pages already pass small bits of server state into inline scripts.

**3. Chart.js vendored as a single UMD file, not a CDN link, not a NuGet/npm package.**
Alternatives considered:
- *CDN `<script src="https://cdn.jsdelivr.net/...">`*: rejected — the dashboard runs on an isolated municipal LAN (see `reporte-tecnico.md` §6); a CDN dependency would silently fail offline or wherever outbound internet is firewalled.
- *Hand-rolled SVG/Canvas bar/donut renderer*: rejected for v1 — more code to own and test for marginal benefit; Chart.js's UMD build is a single ~200KB file, MIT-licensed, with no runtime dependencies, and gives responsive canvases, tooltips, and legends for free. If a future need arises to trim the dependency, the DTOs this design produces are chart-library-agnostic.
- *npm-managed via a build step*: rejected — the project has no npm/bundler pipeline today (`wwwroot` is served as-is via ASP.NET Core static web assets); introducing one for a single vendored file would be disproportionate. The file is committed directly to `wwwroot/js/vendor/chart.umd.min.js`, same as any other static asset in this repo.

**4. F8 backlog chart reuses `DeadlineCalculator`, does not reimplement deadline math.**
`StatisticsService` takes a dependency on `RouterOptions.PlazoDiasHabiles` and calls the same `DeadlineCalculator.AddBusinessDays`/`BusinessDaysRemaining` static methods F8 already uses, bucketing each F8-destined case (`Destination == CaseDestination.F8`) into within-deadline vs past-deadline by the same `restantes < 0` rule the F8 view already renders per row.

**5. Turnaround-time metric only counts fully Confirmed cases.**
Average days from `ReceivedAt` to `ConfirmedAt` is computed only over `Status == Confirmed` rows (both timestamps guaranteed non-null). Pending/Uploaded cases have no `ConfirmedAt` yet and are excluded rather than estimated — an in-progress case's "time so far" is a different metric than a closed case's turnaround, and conflating them would understate true turnaround as more open cases pile in.

**6. Nav link added to every existing dashboard page's header, not just a new isolated route.**
Consistent with how F8/Certificado/Comunas/Discarded are already cross-linked from each other's `<nav class="app-nav">`/`.app-subnav` — Estadísticas needs the same visibility to be discoverable, so all five existing `.cshtml` headers get one new `<a asp-page="/Estadisticas">` link.

## Risks / Trade-offs

- **[Risk] All-time aggregation becomes less useful as the dataset grows across years, with no way to focus on "this month" or "this quarter".** → Mitigation: explicitly deferred as a Non-Goal; the DTOs and service methods are structured so a later change can add an optional date-range parameter without breaking the v1 contract.
- **[Risk] Chart.js adds a ~200KB static asset to the app, first one of its kind in this project.** → Mitigation: it's committed once, served locally (no network round-trip beyond the LAN), and only loaded on `/Estadisticas` — every other page is unaffected.
- **[Risk] In-memory LINQ aggregation over `GetAll()` doesn't scale indefinitely.** → Mitigation: matches the existing pattern used by every other screen in this app; at current and foreseeable municipal case volumes (hundreds/low-thousands of rows) this is not a real bottleneck. Revisit only if row counts grow by an order of magnitude.
- **[Trade-off] No drill-down from a chart to filtered rows in v1** — accepted to ship a focused first version and validate which charts the operator actually finds useful before investing in interactivity.

## Migration Plan

No data migration. Deploy is: ship the new page, service, and vendored JS file in the next regular build/publish cycle used for this app (self-contained single-file `dotnet publish`, Windows Scheduled Task restart). Rollback is reverting the commit — no schema or data changes to undo.

## Open Questions

- Exact chart types per metric (donut vs stacked bar, etc.) are proposed in `proposal.md`/the prior chat plan; final pixel-level layout is decided during implementation, not fixed here.
- Whether "top comunas" should cap at a fixed N (e.g., top 10) or be scrollable — default to top 10 unless the operator asks for more once they see it.
