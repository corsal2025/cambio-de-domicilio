# Endpoint (curl) and E2E Verification

- Date: 2026-09-24
- Change: caja-actions-flow
- Environment: an isolated instance of the published build on `https://127.0.0.1:5091`, running on a **copy** of the production DB (`scratchpad/e2e/data/router.db`).
  - EWS was pointed to an invalid URL (`Router__Ews__Url`) and polling disabled, so no mailbox access and no real email.
  - The production app was stopped during the test (the app has a global single-instance mutex) and restarted afterwards with the new build.

## Endpoint checks
| Request | Expected | Result |
|---|---|---|
| `curl -sk https://127.0.0.1:5091/` | 200 | 200 |
| `curl -sk https://127.0.0.1:5091/F8` | 200 | 200 |
| `curl -sk https://127.0.0.1:5091/Caja` | 200 | 200 |
| POST `/F8?handler=SendToCaja` id=192 (closed without folder), with antiforgery token | no state change | redirect; the row is still `Destination=None`, closed |
| POST `/F8?handler=SendToCaja` id=999999 (does not exist) | no state change | redirect; no row created |
| POST `/F8?handler=CloseWithoutFolder` id=999999 | no state change | redirect; no row created |
| POST `/?handler=SubirACaja` id=999999 | error message | 200, "El caso no existe." |
| POST `/F8?handler=SendToCaja` without an antiforgery token | rejected | 400 |

## E2E flows (built-in browser)
- **Flow A**: case 1 (Confirmed) in Casos, press **Caja**. The case leaves Casos, the search shows "Carpeta física ubicada en cola de Caja", and it appears in the Caja queue. Then **Devolver a casos**: it is back in Casos with the Caja + Rectificar actions. PASS
- **Flow B**: case 192 in F8 shows the actions Caja / Sin carpeta / Revertir / delete. Press **Sin carpeta**: Casos shows the "Cerrado sin carpeta" badge with only the delete action. PASS
- **Flow C**: case 177 in F8 with CodigoF8=20582927 (Confirmed), press **Revertir**. Casos shows only Caja + delete. Press **Caja**: the case is in the queue (`Destination=Caja`, `CodigoF8=NULL`, `SoloCaja=0`). PASS
- **Visual**: icon buttons render with the per-type colors and a tooltip (`title` + `aria-label`). At mobile width (375px) there is no page-level horizontal overflow; the table scrolls inside its card. PASS
- **F8 search bar**: restored, it filters, and the cross-screen banner to Casos works. PASS

## Cleanup
- The test DB copy is disposable (scratchpad); the production DB was never written by the tests.
- The viewport emulation was reset.

## Known limitations (pre-existing, not introduced by this change)
- After a POST on Casos, the redirect drops the active search, because `SearchQuery` is not bound on POST. The success message is also not shown after the redirect (no TempData).
