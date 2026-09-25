## Why

The Casos search says a person's folder is "in Caja A1-CD" but not which listing (several closes share the code A1-CD) nor where in it, so the operator can't go straight to the physical folder.

## What Changes

- The search banner lists every Caja location of the person: the box code, the listing's close date, and the exact N° (position) inside that listing, or "cola de Caja" plus its position.
- Each location links to that listing (or the queue) and highlights and scrolls to the exact row.

Mapped to docs/flujo-proceso.md Paso 7b (carpeta física a Caja).

## Non-goals

- No change to how boxes are closed or numbered.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `dashboard`: cross-screen search reports the precise Caja listing and position and links to the highlighted row.

## Impact

- `Dashboard/Pages/Index.cshtml(.cs)`: `CajaMatches` locations and banner.
- `Dashboard/Pages/Caja.cshtml(.cs)`: `highlightId` parameter, row anchors, highlight and scroll.
- Tests: `IndexModelTests`, `CajaModelTests`.
