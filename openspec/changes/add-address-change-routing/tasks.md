# Tasks: Address Change Routing

## 0. Prerequisite (external, blocking real-mailbox testing)
- [ ] 0.1 Coordinate with municipality IT: Azure AD app registration with `Mail.Read`/`Mail.Send` application permissions and admin consent, scoped to `cambiodedomicilio@munivalpo.cl` via an application access policy. Development can proceed against a test mailbox meanwhile.

## 1. Project scaffold
- [x] 1.1 Create `.sln` + `src/OutlookComunaRouter` Worker Service project (net10.0, `Microsoft.Extensions.Hosting` `BackgroundService`)
- [x] 1.2 Add packages: `Microsoft.Graph`, `Azure.Identity`, `Microsoft.Data.Sqlite`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.*`. Toast implemented without a third-party package (native PowerShell/WinRT call) after `Microsoft.Toolkit.Uwp.Notifications` was found to pull a critical CVE (`System.Drawing.Common` 4.7.0) and to force a Windows-only TFM — see `design.md`.
- [x] 1.3 `.gitignore`, `appsettings.json` (placeholders) + `appsettings.Example.json` (poll interval, mailbox, notification email, toast enabled/disabled), README with Azure AD app registration steps and Windows Task Scheduler setup

## 2. Domain and persistence
- [x] 2.1 `PersonRequest` (including `SourceSubject`, `SourceSender`, `NeedsReview`) and `ComunaContact` records (Domain/)
- [x] 2.2 SQLite schema creation (on startup) + repository with idempotent insert-by-`SourceMessageId` (unique constraint), plus a lookup by `(rut, comuna)` for duplicate suppression
- [x] 2.3 Unit tests for repository against a temp SQLite file, including the `(rut, comuna)` duplicate-suppression lookup

## 3. Comuna directory import
- [x] 3.1 CSV parser for comuna → contact_email → domain
- [x] 3.2 Reloaded from CSV on each polling cycle (last-row-wins on duplicate domain)
- [x] 3.3 Unit tests: valid rows, malformed row, missing file

## 4. Graph integration
- [x] 4.1 Graph client factory using `ClientSecretCredential` from configuration
- [x] 4.2 `IEmailReader`: list messages in `cambiodedomicilio@munivalpo.cl` received since the last cycle window
- [x] 4.3 `IMailSender`: send the formal folder-request email (template in `docs/email-templates.md` and `Notifications/EmailTemplates.cs`) via `sendMail`
- [x] 4.4 Reply-notification email sent via the same `IMailSender` (`EmailNotificationChannel`)

## 5. Extraction and routing logic
- [x] 5.1 Regex-based extractor for `full_name` (case-insensitive) + `rut` (with/without dots, normalized to canonical form, validated against the Chilean RUT check-digit algorithm) — unit tests against sample bodies in both formats, including an invalid check-digit case
- [x] 5.2 Directory-based comuna detection: sender domain matched against `ComunaContact.Domain`, excluding own domain (unit tests)
- [x] 5.3 Duplicate suppression: before sending, look up existing `(rut, comuna)` request with `status IN (Sent, Responded)`; link instead of re-sending
- [x] 5.4 Routing service: pending -> sent transition, missing-data/unknown-comuna/duplicate stays pending or linked, `NeedsReview` flag set accordingly

## 6. Reply detection and notification
- [x] 6.1 Thread-based match (`conversationId`) marks `Responded`
- [x] 6.2 RUT-fallback match for new-thread replies from comuna domains (normalized RUT comparison)
- [x] 6.3 Unit tests for both matching paths (`AddressChangeRoutingServiceTests`): same-thread match, RUT-fallback on a new thread, no-match case, unknown-domain case
- [x] 6.4 On-screen notification on `Responded` transition (guarded by config flag, no-op on non-Windows, best-effort — never throws into the pipeline)
- [x] 6.5 Email notification on `Responded` transition, always fired regardless of toast availability

## 7. Reporting
- [x] 7.1 CSV writer that rewrites the report at a fixed configured path (`full_name`, `rut`, `comuna`, `status`, `last_folder_date`, `Requiere revisión`) on every polling cycle, atomically (temp file + move)
- [x] 7.2 Unit tests: `Requiere revisión = Sí` for needs-review rows, `last_folder_date` present for responded rows

## 8. Orchestration
- [x] 8.1 `RouterWorker : BackgroundService` composition root: on each interval tick, run read -> route (extract/dedupe/send) -> match replies -> notify -> refresh report; guard against overlapping cycles via a `SemaphoreSlim` (skips the tick instead of running concurrently)
- [x] 8.2 Configurable poll interval (default 30 min) via `appsettings.json`
- [x] 8.3 Structured logging without PII (IDs/status/counts only, no names/RUTs in log messages)
- [x] 8.4 Retry-with-backoff for transient Graph failures: `GraphRetryPolicy` retries HTTP 401/429/500/502/503/504 with exponential backoff (honors `Retry-After` when present), applied to both `EmailReader` and `MailSender`. Non-transient errors (other 4xx) propagate immediately, still caught at cycle level so the service never crashes.
- [x] 8.5 `dotnet build` + `dotnet test` green (29/29 passing, 0 warnings, no known vulnerabilities). Manual end-to-end dry run against a real/test mailbox still pending task 0.1 (Azure AD access — external, not something this implementation can unblock).
