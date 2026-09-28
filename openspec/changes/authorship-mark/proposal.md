## Why

The domain model and the system were created by Raúl Salazar; the author asked for a visible creation mark in the app, the repository, and the model documents.

## What Changes

- A small authorship footer on every dashboard screen ("Modelo y sistema creados por Raúl Salazar"), hidden when printing so official listings stay unchanged.
- `Authors`/`Copyright` metadata in the project file, an `AUTHORS.md`, and an "Autoría" section in the README.
- Interactive domain-model diagram (`docs/modelo-dominio.html`) and presentation sources (`docs/presentacion/`), both signed.

Mapped to docs/flujo-proceso.md: Programa en red (dashboard).

## Non-goals

- No change to printed sector or box listings.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `dashboard`: every screen shows the authorship footer.

## Impact

- `Dashboard/Pages/Shared/_AuthorFooter.cshtml` + all pages, `dashboard.css`, `CambioDeDomicilio.csproj`, `README.md`, `AUTHORS.md`, `docs/`.
