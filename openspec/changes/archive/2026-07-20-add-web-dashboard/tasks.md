# Tasks: Web Dashboard for the Upload-Confirmation Flow

Depends on: `add-address-change-routing` (EWS) and `add-upload-confirmation-flow` (Pending/Uploaded/Confirmed lifecycle, `SendConfirmationAsync`, sector derivation) both complete.

## 1. Host restructuring
- [x] 1.1 Converted the host from `Host.CreateApplicationBuilder` to `WebApplication.CreateBuilder` (SDK switched to `Microsoft.NET.Sdk.Web`), keeping `RouterWorker` as hosted service; Kestrel bound to `https://0.0.0.0:5001` (configurable via `Kestrel:Endpoints` in appsettings) with a plain `http://0.0.0.0:5000` listener that only redirects to HTTPS (`UseHttpsRedirection`), never serves data
- [x] 1.2 Feature-folder layout: `Routing/` (existing services, untouched) and `Dashboard/` (new, with its own `Auth/` and `Pages/` subfolders); shared kernel (`Domain/`, `Persistence/`, `Ews/`, `Notifications/`, `Configuration/`) consumed only via DI interfaces
- [ ] 1.3 Module registry: **deliberately deferred** — there is exactly one module today (routing/dashboard aren't really two separate modules yet, just one app). A registry to route nav items would be speculative infrastructure for a second module that doesn't exist. Nav in `Index.cshtml` is a hardcoded list for now; revisit if/when a second workflow module is actually added.
- [x] 1.4 Certificate setup: `dotnet dev-certs https --trust` documented as the first deployment step in `deploy/README.md`, including exporting/importing the cert on colleagues' viewer PCs

## 2. Authentication
- [x] 2.1 `DashboardUser` table (username, PBKDF2 hash + per-user salt, iterations, failed-attempt counter, lockout timestamp, created_at) + repository
- [x] 2.2 Cookie auth middleware (`CookieAuthenticationDefaults`), `/Login` and `/Logout` pages, `[Authorize]` on `IndexModel`/`SectorModel`
- [x] 2.3 CLI verbs: `--add-user <name>` (prompts for password, masked input, no echo), `--remove-user <name>`
- [x] 2.4 Failed-login delay (1s) + lockout after 5 failed attempts (15 min)
- [x] 2.5 Unit tests: hash/verify roundtrip, distinct salts, repository CRUD, lockout counter and correct-password-still-locked behavior

## 3. Case list and data entry
- [x] 3.1 Schema: `ConfirmedByUserId` (FK to `DashboardUser`, nullable) added to `PersonRequest` and `UpdateStatusToConfirmed`
- [x] 3.2 Case list page (`/Index`): table over `PersonRequestRepository.GetAll()`, filters by `Status` (query string) and `NeedsReview`
- [x] 3.3 Editable `fecha_ultima_carpeta` per case (inline `<input type="date">` auto-submitting to `OnPostSetFecha`), sector recomputed on reload (it's a computed property, always current)
- [x] 3.4 `IndexModelTests`: no-filter listing, status filter, needs-review filter, date-set round trip, confirm-attribution round trip

## 4. Confirmation action
- [x] 4.1 "Enviar confirmación" button shown only for `Uploaded` + complete-data cases; `OnPostConfirmAsync` calls `SendConfirmationAsync`, which independently re-checks eligibility server-side
- [x] 4.2 `ConfirmedByUserId` taken from the authenticated session's `NameIdentifier` claim, recorded alongside `ConfirmedAt`
- [x] 4.3 Refusal reasons surfaced via `Message` on the page
- [x] 4.4 Covered by existing `AddressChangeRoutingServiceTests` (happy path with attribution, refusal paths, double-send no-op) — these were written for `SendConfirmationAsync` directly, which is exactly what the button calls

## 5. Sector PDF
- [x] 5.1 `/Sector/{sector}` print view: title, generation date, table of `full_name`/`rut`/`comuna`/`fecha_ultima_carpeta`, `@media print` CSS hiding the back-link/print button
- [x] 5.2 `SectorModelTests`: filters correctly by derived sector, excludes cases without a fecha

## 6. Portability and packaging
- [x] 6.1 Publish profile verified for real: `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true` produces a working single ~100MB exe; fixed a real leak found in the process — `appsettings.Development.json` (real EWS password) was being copied into the publish output by default, now excluded via `CopyToPublishDirectory="Never"` in the csproj. Ran the published exe standalone (`--smoke-test`) and confirmed it starts, loads config, and reaches the EWS endpoint.
- [x] 6.2 `deploy/create-desktop-shortcut.ps1`: creates a Desktop shortcut that starts the exe if not already running and opens the dashboard URL in the browser
- [x] 6.3 Copy-to-second-PC scenario — confirmed live in production (operator-verified, 2026-07-20)

## 7. Verification
- [x] 7.1 `dotnet build` + full test suite green (105/105)
- [x] 7.2 Live end-to-end verification — confirmed live in production (operator-verified, real mailbox, real dashboard users, 2026-07-20)
- [x] 7.3 Root `README.md` — dashboard usage section added ("## Dashboard web": login, filters, editable date, confirmation button with attribution, sector PDF)

## 8. Visual design (added after first live review with the operator)
- [x] 8.1 `wwwroot/css/dashboard.css`: institutional color palette via CSS custom properties, status badges (Pendiente/Subido/Confirmado colored distinctly), zebra-striped table, consistent spacing/typography — shared by `Login` and `Index`; `Sector` keeps its separate minimal print stylesheet
- [x] 8.2 Verify visually in the browser against real data — confirmed by operator, 2026-07-20

## 10. Manual person-data entry (added 2026-07-03, operator request)
- [x] 10.1 `PersonRequestRepository.SetPersonData(id, fullName, rut)`: stores name + normalized RUT, clears `NeedsReview`
- [x] 10.2 Index page: on `needs_review` rows, inline name + RUT inputs with save; RUT check-digit-validated server-side, rejection message surfaced
- [x] 10.3 Unit tests: valid entry clears review flag, invalid RUT rejected, normalization applied
- [x] 10.4 Center table headers (operator visual request)
- [x] 10.5 Fecha última carpeta as free-text Spanish entry ("15 marzo 2024"), no calendar picker; `SpanishDate` parser/formatter (accepts "de"/"del" connectors, setiembre variant), rejection message on unparseable input; same format rendered in Index and the Sector print view

## 11. Legal-deadline countdown (added 2026-07-03, operator request; plazo = 15 días hábiles)
- [x] 11.1 `PersonRequest.ReceivedAt` (email's DateTimeReceived) stored at detection; schema + insert/map
- [x] 11.2 `DeadlineCalculator`: add/count business days (Mon–Fri), deadline = received + 15 hábiles (configurable `PlazoDiasHabiles`)
- [x] 11.3 Index: "Recibido" and "Plazo" columns — countdown badge from day one, amber ≤7, red ≤3/overdue, hidden once Uploaded/Confirmed
- [x] 11.4 CSV report gains `fecha_recibido`
- [x] 11.5 Unit tests: business-day math (weekends, exact boundary), badge thresholds, uploaded cases excluded

## 9. Comuna directory management (added 2026-07-03, operator request)
- [x] 9.1 `ComunaDirectory.UpdateContactEmail(csvPath, comuna, newEmail)`: atomic CSV rewrite (temp + move), preserving all other rows
- [x] 9.2 `/Comunas` page: table of comuna/email/domain with inline email edit per row, `[Authorize]`, nav link from the header
- [x] 9.3 Server-side email-shape validation, rejection message surfaced on the page
- [x] 9.4 Unit tests: update round-trip, unknown comuna no-op, invalid email rejected, other rows untouched
