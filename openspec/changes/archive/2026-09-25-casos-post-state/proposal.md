## Why

After any action in Casos (Caja, Marcar subida, checkboxes, delete…) the page redirects without the active search/filter and the success message is lost, so an operator working by RUT must retype the search after every click. Separately, a case closed with "Sin carpeta" keeps its F8 checkbox ticked, which reads as if it were still an F8 case.

## What Changes

- Casos POST actions return to the same filtered/searched list (status, needsReview, bounced, search preserved).
- The action's result message survives the redirect and is shown once.
- "Sin carpeta" clears the F8 flag (`FolderNotFound`) when closing the case.

Mapped to docs/flujo-proceso.md: Programa en red (dashboard) and Paso 7b.

## Non-goals

- No change to F8 or Caja page redirects (they already keep search/highlight).
- No new filters.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `dashboard`: Casos keeps list state and shows result messages across POST actions.
- `caja-flow`: closing without folder also clears the F8 flag.

## Impact

- `Dashboard/Pages/Index.cshtml(.cs)`: bound filter properties, TempData message, hidden state inputs on POST forms.
- `Persistence/PersonRequestRepository.cs`: `CloseWithoutFolder` clears `FolderNotFound`.
- Tests: `IndexModelTests`, `PersonRequestRepositoryTests`.
