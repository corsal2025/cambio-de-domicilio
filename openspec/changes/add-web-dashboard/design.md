# Design: Web Dashboard for the Upload-Confirmation Flow

## Context

The worker service gains an embedded web UI. One process, two responsibilities: background polling (unchanged) + Kestrel HTTP listener for the dashboard. The case lifecycle (`Pending`/`Uploaded`/`Confirmed`) and the confirmation-sending logic (`SendConfirmationAsync`) already exist in `AddressChangeRoutingService` — this change is a UI over existing, tested logic, not new business rules.

## Decisions

### One process, embedded ASP.NET Core — not a separate desktop app
A native desktop app (WPF/WinForms) would satisfy "click an icon" but not "several people see it in real time" without building a separate server anyway, and it would never run on the future Linux VPS. Embedding Kestrel in the existing host (`WebApplication` builder with `RouterWorker` registered as hosted service) gives: double-click shortcut → browser opens `https://localhost:5001`; colleagues browse `https://<host-pc>:5001`; the same binary later serves on the VPS. Rejected: Electron/Tauri wrappers (heavier stack, new toolchains, same browser engine underneath).

### Server-rendered Razor Pages + light polling refresh — no SPA framework
The UI is CRUD-lite: a case list, an editable date field, two buttons (send confirmation, generate PDF). Razor Pages keeps it in one language/stack (C#), no Node build chain, trivially portable in the single-file publish. "Real time" is auto-refresh of the list fragment on the polling interval boundary — SignalR was considered and rejected: WebSockets add moving parts for an update cadence of once per 30 minutes.

### Auth: local user table, PBKDF2 hashes, cookie sessions — no ASP.NET Identity
ASP.NET Identity brings EF Core, migrations and UI scaffolding for what is: a `User` table (username, PBKDF2 hash with per-user salt via `Rfc2898DeriveBytes`, role flag), a login form, and cookie authentication middleware. Users are created by an admin CLI command (`--add-user`), not open registration. Rejected: Windows/AD integrated auth (ties the dashboard to domain-joined machines; the mailbox credential model is already non-domain on the VPS path).

### No mail classification — the case list is a direct view of PersonRequest
The original draft of this change assumed ambiguous incoming mail needing human classification into multiple types. That assumption is gone: folder membership already determines everything deterministically (an email in "CARP. PARA PEDIR" is a `Pending` request; moving it to "CARP. YA PEDIDAS" makes it `Uploaded`). The dashboard's case list is a straightforward table over `PersonRequest`, filterable by status and by "Requiere revisión" — no classification engine, no confirm/reclassify workflow to build.

### "Enviar confirmación" is a thin UI wrapper over the existing service method
`SendConfirmationAsync(id)` in `AddressChangeRoutingService` already refuses non-`Uploaded`/incomplete cases with a reason (built and tested in `add-upload-confirmation-flow`). The dashboard button calls it and surfaces the result (sent / refused-with-reason) — no new business logic, just wiring and an HTML button that's disabled client-side for cases that are not `Uploaded`, backed by the same server-side guard so a stale page can't bypass it.

### Confirmation attribution: who clicked, not just when
Since a real email now goes out to another municipality on a manual click, `PersonRequest` gains `ConfirmedByUserId` (FK to `User`), set alongside the existing `ConfirmedAt` when `SendConfirmationAsync` succeeds. This is new to the domain model (the button didn't exist yet when `ConfirmedAt` was first added) — accountability for who sent an official communication matters here the same way it did for the classification-audit concern raised earlier for a different (now-superseded) design.

### Sector PDF: browser print with `@media print` CSS, not server-side PDF generation
A per-sector view (Archivo / Oficina 43) lists `full_name`, `rut`, `comuna`, `fecha_ultima_carpeta` for cases in that sector; print CSS hides navigation and buttons, leaving a formal document. Browsers print to paper or PDF natively. Rejected: server-side PDF generation (QuestPDF/wkhtmltopdf) — a heavy dependency to replicate what the browser already does; revisit only if pixel-exact letterhead is required.

### Modular structure for future modules (explicit user requirement)
- **Feature-folder modules**: `Routing/` (existing) and `Dashboard/` (new) as self-contained folder trees; no cross-module references.
- **Shared kernel via interfaces**: auth/session, SQLite access, the EWS mail client, notification channels are exposed as registered interfaces consumed via DI — already proven by the Graph→EWS swap touching nothing outside the transport layer.
- **Navigation as configuration**: nav renders from a registered module list.
- **Deliberately rejected**: a runtime plugin system — modules arrive by adding code and recompiling, not dynamic assembly loading.

### Portability: self-contained single-file publish
`dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true` produces one `.exe` requiring no installed runtime. Data (SQLite, CSV, config) stays in a sibling `data/` folder so copying the folder moves the whole installation.

## Risks / Trade-offs

- **HTTPS with a locally-trusted certificate**: session cookies and personal data must not travel in clear text on the municipal LAN, and neither should login passwords. Kestrel serves HTTPS only; plain HTTP redirects and never serves data. Certificate: `dotnet dev-certs https` on the host machine; colleagues' browsers need the cert imported once (documented in the deployment checklist) or will show a one-time trust warning.
- **Keyword/domain-based case detection remains a routing-layer concern, not a dashboard one**: already covered by existing tests in `add-address-change-routing`/`add-upload-confirmation-flow`; the dashboard does not re-implement or second-guess it.
- **Single process serves UI and polls mail**: a UI crash takes down polling and vice versa. Accepted for v1 (systemd/Task Scheduler restarts cover it).
- **PBKDF2 without rate limiting beyond lockout**: a fixed small delay on failed logins and lockout after repeated failures, not a full rate-limiter — acceptable for a small internal user base.
- **Self-signed certificate trust is a manual step per viewer PC**: documented in the deployment checklist.
