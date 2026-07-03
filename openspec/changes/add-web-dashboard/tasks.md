# Tasks: Web Dashboard for the Upload-Confirmation Flow

Depends on: `add-address-change-routing` (EWS) and `add-upload-confirmation-flow` (Pending/Uploaded/Confirmed lifecycle, `SendConfirmationAsync`, sector derivation) both complete.

## 1. Host restructuring
- [ ] 1.1 Convert the host from `Host.CreateApplicationBuilder` to `WebApplication.CreateBuilder`, keeping `RouterWorker` as hosted service; Kestrel bound to `https://0.0.0.0:5001` (configurable) with a plain `http://0.0.0.0:5000` listener that only redirects to HTTPS (`UseHttpsRedirection`), never serves data
- [ ] 1.2 Feature-folder layout: `Routing/` (existing services relocated), `Dashboard/` (new); shared kernel (`Domain/`, `Persistence/`, `Ews/`, `Notifications/`, `Configuration/`) consumed only via DI interfaces
- [ ] 1.3 Module registry: nav items render from registered module descriptors
- [ ] 1.4 Certificate setup: `dotnet dev-certs https` on the host machine; deployment script documents installing it into the Trusted Root store on colleagues' viewer PCs (`deploy/README.md`)

## 2. Authentication
- [ ] 2.1 `User` table (username, PBKDF2 hash + per-user salt, iterations, created_at) + repository
- [ ] 2.2 Cookie auth middleware, login/logout pages, `[Authorize]` as global default policy
- [ ] 2.3 CLI verbs: `--add-user <name>` (prompts for password, no echo), `--remove-user <name>`
- [ ] 2.4 Failed-login delay + lockout after repeated failures
- [ ] 2.5 Unit tests: hash/verify roundtrip, lockout counter

## 3. Case list and data entry
- [ ] 3.1 Schema: `ConfirmedByUserId` (FK to `User`) added to `PersonRequest`
- [ ] 3.2 Case list page: table over `PersonRequestRepository.GetAll()`, filters by `Status` and `NeedsReview`
- [ ] 3.3 Editable `fecha_ultima_carpeta` per case (POST handler calling the existing `SetFechaUltimaCarpeta`), sector recomputed and re-rendered immediately
- [ ] 3.4 Unit tests: list rendering with filters, date-set round trip

## 4. Confirmation action
- [ ] 4.1 "Enviar confirmación" button on `Uploaded` cases, POST handler calling `SendConfirmationAsync`; server-side eligibility check independent of what the page shows
- [ ] 4.2 Record `ConfirmedByUserId` from the authenticated session alongside the existing `ConfirmedAt` on success
- [ ] 4.3 Surface refusal reasons (not `Uploaded`, already `Confirmed`, missing data) back to the page
- [ ] 4.4 Unit tests: happy path with attribution, refusal paths, double-click/already-confirmed no-op

## 5. Sector PDF
- [ ] 5.1 Per-sector print view (Archivo / Oficina 43): title, generation date, table of `full_name`/`rut`/`comuna`/`fecha_ultima_carpeta`, `@media print` CSS hiding chrome
- [ ] 5.2 Unit test: view model filters correctly by derived sector

## 6. Portability and packaging
- [ ] 6.1 Publish profile: `win-x64`, self-contained, single file; data folder resolved relative to the executable
- [ ] 6.2 Desktop shortcut script (`deploy/`): starts the exe if not running and opens the browser at the dashboard URL
- [ ] 6.3 Verify copy-to-second-PC scenario (docs + manual checklist in `deploy/README.md`)

## 7. Verification
- [ ] 7.1 `dotnet build` + full test suite green
- [ ] 7.2 Live end-to-end: login over HTTPS, plain-HTTP request confirmed redirected, enter a última-carpeta date, send a confirmation and verify attribution, generate both sector documents, second browser session sees updates
- [ ] 7.3 Update root `README.md` (dashboard usage, user management, LAN access, portability)
