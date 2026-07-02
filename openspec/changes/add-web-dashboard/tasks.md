# Tasks: Web Dashboard with Supervised Classification

Depends on: `add-address-change-routing` tasks 4.5–4.11 (EWS integration) being complete.

## 1. Host restructuring
- [ ] 1.1 Convert the host from `Host.CreateApplicationBuilder` to `WebApplication.CreateBuilder`, keeping `RouterWorker` as hosted service; Kestrel bound to `https://0.0.0.0:5001` (configurable) with a plain `http://0.0.0.0:5000` listener that only redirects to HTTPS (`UseHttpsRedirection`), never serves data
- [ ] 1.2 Feature-folder layout: `Routing/` (existing services relocated), `Dashboard/` (new); shared kernel (`Domain/`, `Persistence/`, `Graph→Ews/`, `Notifications/`, `Configuration/`) consumed only via DI interfaces
- [ ] 1.3 Module registry: nav items render from registered module descriptors
- [ ] 1.4 Certificate setup: `dotnet dev-certs https` on the host machine for local access; deployment script exports the cert and documents installing it into the Trusted Root store on colleagues' viewer PCs (`deploy/README.md`)

## 2. Authentication
- [ ] 2.1 `User` table (username, PBKDF2 hash + per-user salt, iterations, created_at) + repository
- [ ] 2.2 Cookie auth middleware, login/logout pages, `[Authorize]` as global default policy
- [ ] 2.3 CLI verbs: `--add-user <name>` (prompts for password, no echo), `--remove-user <name>`
- [ ] 2.4 Failed-login delay + lockout after repeated failures
- [ ] 2.5 Unit tests: hash/verify roundtrip, lockout counter

## 3. Classification
- [ ] 3.1 Schema: `classification`, `classification_source`, `classified_by_user_id` (FK to `User`), `classified_at` on tracked mail; new `IncomingRequest` table
- [ ] 3.2 Rule-based classifier (conversation match → OutgoingReply; keyword lists from config for IncomingRequest / AddressChangeNotification; fallback Unclassified), skipping items with `classification_source = 'manual'`
- [ ] 3.3 Ingestion pipeline stores every known-comuna mail with its proposed classification (not only address-change notifications)
- [ ] 3.4 Reclassify action (POST, requires authenticated user) marks item manual, stamps `classified_by_user_id`/`classified_at`, and re-routes it (e.g. manual `AddressChangeNotification` enters the outbound request flow)
- [ ] 3.5 Unit tests: each rule, manual-precedence, keyword configurability, audit fields set on manual action and left null on auto

## 4. Dashboard UI
- [ ] 4.1 Layout + login page + nav (module registry) with print-hidden chrome
- [ ] 4.2 Review queue: latest classified mail with unconfirmed markers, confirm/reclassify buttons, and who/when for already-confirmed items
- [ ] 4.3 "Solicitudes enviadas" view: outbound requests with status, filterable
- [ ] 4.4 "Solicitudes recibidas" view: `IncomingRequest` rows, answered flag toggle
- [ ] 4.5 Auto-refresh of list fragments on the polling cadence
- [ ] 4.6 Print view: formal document (title, generation date, table: full name, RUT, last folder date, comuna of origin when applicable) with `@media print` CSS

## 5. Portability and packaging
- [ ] 5.1 Publish profile: `win-x64`, self-contained, single file; data folder resolved relative to the executable
- [ ] 5.2 Desktop shortcut script (`deploy/`): starts the exe if not running and opens the browser at the dashboard URL
- [ ] 5.3 Verify copy-to-second-PC scenario (docs + manual checklist in `deploy/README.md`)

## 6. Verification
- [ ] 6.1 `dotnet build` + full test suite green
- [ ] 6.2 Live end-to-end: login over HTTPS, plain-HTTP request confirmed redirected, review queue shows real classified mail with attribution, both reports render, print preview correct, second browser session sees updates
- [ ] 6.3 Update root `README.md` (dashboard usage, user management, LAN access, portability)
