# Step 7 Report - E2E Verification (Playwright)

- Date: 2026-07-28
- Change: add-estadisticas-dashboard
- Agent: Claude Sonnet 5 (Claude Code)

## Scenarios Executed

1. **Unauthenticated access**: `curl -sk https://localhost:5001/Estadisticas` (no cookie) → `302` redirect to `/Login?ReturnUrl=%2FEstadisticas`.
2. **Authenticated access (curl)**: real login as `operador` via `/Login` form POST (CSRF token extracted from the page), then `GET /Estadisticas` with the session cookie → `200`, response body contains all 10 expected `<canvas id="...">` elements.
3. **E2E render (Playwright, chromium headless)**: real login through the UI, navigate to `/Estadisticas`, wait for network idle:
   - 10/10 canvases present, zero console/page errors.
   - Screenshot confirmed every chart renders with real production data (not blank) after fixing a casing bug (see below).
4. **Nav link from every existing screen**: clicked the "Estadísticas" link from `/Index`, `/F8`, `/Certificado`, `/Discarded`, `/Comunas` — each navigated to `/Estadisticas` correctly.
5. **Visual polish pass** (user feedback mid-session): the nav pill initially rendered as a bare blue underlined link — added a dedicated `.nav-estadisticas` violet pill style (distinct from Casos/F8/Certificado's colors) matching the existing pill pattern. The "Correos descartados por motivo" chart's long domain-name labels were clipped in a one-third-width card — made it `grid-column: 1 / -1` (full width) with a taller canvas so every label and value is fully legible.

## Bug found and fixed during E2E

- **Symptom**: donut charts (Casos por estado, Sector, Certificado ×2) rendered with a legend but no visible ring; some bar charts appeared to have zero-looking axes.
- **Root cause**: `System.Text.Json.JsonSerializer.Serialize` defaults to PascalCase property names (`Pending`, `Uploaded`, ...), but `estadisticas.js` reads camelCase (`data.status.pending`) — a silent `undefined` per property, not an exception, so nothing errored but every chart got `NaN`/`undefined` data.
- **Fix**: `JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }` on the serialization call in `Estadisticas.cshtml`. Re-verified with a fresh screenshot — every chart now shows real data (e.g., 34 Pending / 97 Confirmed, weekly trend, top comunas, 14.5-day average turnaround, 105/24 sector split, 9/0 F8 deadline split, 0/7 F8 PDF split, discard reasons).

## Data Persistence / Mutation

- This screen performs zero writes — `EstadisticasModel.OnGet` only reads via `IPersonRequestRepository.GetAll()` / `IDiscardedEmailRepository.GetAll()`. No database state was created, changed, or needed restoring at any point in this verification.

## Outcome

- Step 7 status: PASS
- Blocking issues: none (one bug found and fixed during this same step, documented above)
