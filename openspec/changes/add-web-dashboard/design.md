# Design: Web Dashboard with Supervised Classification

## Context

The worker service from `add-address-change-routing` gains an embedded web UI. One process, two responsibilities: background polling (unchanged) + Kestrel HTTP listener for the dashboard. Several municipal staff must see the same live state; the operator must supervise mail classification and print reports.

## Decisions

### One process, embedded ASP.NET Core — not a separate desktop app
A native desktop app (WPF/WinForms) would satisfy "click an icon" but not "several people see it in real time" without building a separate server anyway, and it would never run on the future Linux VPS. Embedding Kestrel in the existing host (`WebApplication` builder with the worker registered as hosted service) gives: double-click shortcut → browser opens `http://localhost:5000`; colleagues browse `http://<host-pc>:5000`; the same binary later serves on the VPS. Rejected: Electron/Tauri wrappers (heavier stack, new toolchains, same browser engine underneath).

### Server-rendered Razor Pages + light polling refresh — no SPA framework
The UI is CRUD-lite: lists, filter buttons, a reclassify action, a print view. Razor Pages keeps it in one language/stack (C#), no Node build chain, trivially portable in the single-file publish. "Real time" is implemented as auto-refresh of the list fragment on the polling interval boundary (HTML meta-refresh or a small fetch loop) — SignalR was considered and rejected: WebSockets add moving parts for an update cadence of once per 30 minutes.

### Auth: local user table, PBKDF2 hashes, cookie sessions — no ASP.NET Identity
ASP.NET Identity brings EF Core, migrations and UI scaffolding for what is: a `User` table (username, PBKDF2 hash with per-user salt via `Rfc2898DeriveBytes`, role flag), a login form, and cookie authentication middleware. Users are created by an admin CLI command (`--add-user`), not open registration. Rejected: Windows/AD integrated auth (ties the dashboard to domain-joined machines; the mailbox credential model is already non-domain on the VPS path).

### Classification: rule-based proposal + manual override with precedence
New column `classification` + `classification_source` (`auto` | `manual`) on tracked mail. Auto rules run only when `classification_source != 'manual'`:
1. Reply on a tracked conversation (`ConversationId` match to a sent request) → `OutgoingReply`.
2. Known comuna domain + folder-request keywords in subject/body (configurable list, e.g. "solicita carpeta", "remitir carpeta", "última carpeta") + RUT present → `IncomingRequest`.
3. Known comuna domain + address-change keywords → `AddressChangeNotification`.
4. Otherwise → `Unclassified` (surfaced in the review queue).
The keyword lists live in configuration so the operator can tune them without recompiling. Every auto classification is displayed as "unconfirmed" until a user confirms or changes it; both actions set `classification_source = 'manual'`.

### Incoming requests get their own table, not a status on `PersonRequest`
`PersonRequest` models the outbound lifecycle (pending → sent → responded). An incoming request from another comuna is a different entity with a different lifecycle (received → answered/closed) and different report columns. A shared table with a `direction` flag would overload status semantics. New table `IncomingRequest`: person data (name, RUT), requesting comuna, received date, source message identifiers, classification metadata, answered flag.

### Modular structure for future modules (explicit user requirement)
The system must accept future modules (other municipal workflows) without rework. Concretely, not speculatively:
- **Feature-folder modules**: each capability is a self-contained folder tree (pages, services, its own tables) — `Routing/` and `Dashboard/` today, future modules alongside. No cross-module references; modules talk only to shared infrastructure.
- **Shared kernel via interfaces**: auth/session, SQLite access, the EWS mail client, notification channels and report/print plumbing are exposed as registered interfaces (`IMailSender`, `IEmailReader`, `INotificationChannel`, repositories) that any future module consumes through DI — proven already by the Graph→EWS swap touching nothing outside the transport layer.
- **Navigation as configuration**: the dashboard nav renders from a registered module list, so a new module adds a menu entry by registering itself, not by editing existing pages.
- **Deliberately rejected**: a runtime plugin system (separate assemblies loaded dynamically). Modules arrive by adding code to the repository and recompiling — plugin loaders add versioning and security complexity with no benefit at this scale.

### Printing: browser print with `@media print` CSS
The report view doubles as the printable document: print CSS hides navigation and buttons, leaving a formal document (title, date, table with full name / RUT / last folder date / comuna of origin). Browsers print to paper or PDF natively. Rejected: server-side PDF generation (QuestPDF/wkhtmltopdf) — a heavy dependency to replicate what the browser already does; revisit only if pixel-exact letterhead is required.

### Portability: self-contained single-file publish
`dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true` produces one `.exe` (~80–100 MB) requiring no installed runtime. Data (SQLite, CSV, config) stays in a sibling `data/` folder so copying the folder moves the whole installation. Code-copy protection is explicitly *not* promised beyond compiled distribution + private repository; obfuscators were rejected as snake oil that complicates debugging without stopping a determined decompiler.

### Transport: HTTPS from day one, local certificate
Revised after review: plaintext HTTP would carry both login passwords and PII (names, RUTs) unencrypted across the municipal LAN on every request — not just "data at rest" but credentials in transit, sniffable by anyone else on the same network segment. Kestrel is configured for HTTPS using a certificate generated once (`dotnet dev-certs https` for the host machine, or a self-signed cert with a long validity installed into the Trusted Root store on each viewer PC via the same deployment script that sets up the desktop shortcut). This is a few lines of Kestrel config and one extra deployment step, not a new architecture — no reason to defer it to a later change when the exposure exists from the first login. HTTP is kept only as a redirect-to-HTTPS listener, not a data-serving one.

### Audit trail on classification decisions
Revised after review: `classification_source` alone (`auto`/`manual`) says a human decided, but not *which* human or *when* — insufficient for accountability when several staff share access to personal data and a decision later needs to be justified or traced. Added `classified_by_user_id` (FK to `User`) and `classified_at` alongside `classification_source`, set on every manual confirm/reclassify action. Auto-classifications leave both null.

## Risks / Trade-offs

- **Keyword-based classification is heuristic**: mitigated by design — every item is supervisable, manual decisions are sticky, and `Unclassified` items are surfaced rather than dropped.
- **Single process serves UI and polls mail**: a UI crash takes down polling and vice versa. Accepted for v1 (systemd/Task Scheduler restarts cover it); split into two processes only if real interference appears.
- **PBKDF2 without rate limiting beyond lockout**: login brute force on the LAN is throttled by a fixed small delay on failed logins and lockout after repeated failures, not a full rate-limiter — acceptable for a small internal user base.
- **Self-signed certificate trust is a manual step per viewer PC**: unlike a CA-issued cert, colleagues' browsers will warn until the cert is installed in their Trusted Root store once. Documented in the deployment checklist; revisit with a real internal CA or ACME-issued cert if the LAN grows beyond a handful of machines.
