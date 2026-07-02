# Tasks: Address Change Routing

## 0. Prerequisite (external, blocking real-mailbox testing)
- [x] 0.1 ~~Coordinate with municipality IT: Azure AD app registration~~ **OBSOLETE — no longer needed.** Live verification showed the mailbox lives on the municipality's on-premises Exchange 2016 (`webmail.munivalpo.cl`), not in Exchange Online. Access is via EWS with the mailbox's own AD credentials (`servervalpo\cambiodedomicilio`), which the operator already holds. Authentication and a real `GetFolder(inbox)` call were verified live (2026-07-02). See the superseding decision in `design.md`.

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

## 4. Mail server integration (Graph implementation superseded by EWS — see design.md)
- [x] 4.1 ~~Graph client factory~~ (implemented, then superseded: the mailbox is on-prem, unreachable by Graph)
- [x] 4.2 ~~Graph `IEmailReader`~~ (implemented, then superseded)
- [x] 4.3 ~~Graph `IMailSender`~~ (implemented, then superseded)
- [x] 4.4 Reply-notification email sent via the same `IMailSender` (`EmailNotificationChannel`) — interface-level, unaffected by the transport swap
- [x] 4.5 `EwsClient`: raw SOAP over `HttpClient`, Basic auth over TLS against `https://webmail.munivalpo.cl/EWS/Exchange.asmx`, credentials from configuration
- [x] 4.6 `EwsEmailReader : IEmailReader`: `FindItem` (inbox, `DateTimeReceived >= since`) + `GetItem` (text body, `InternetMessageId`, `ConversationId`, sender)
- [x] 4.7 `EwsMailSender : IMailSender`: `CreateItem` with `MessageDisposition="SendAndSaveCopy"`
- [x] 4.8 Switch idempotency key to `InternetMessageId` (EWS `ItemId` is not move-stable); `ConversationId` keeps thread matching working
- [x] 4.9 Remove `Microsoft.Graph` and `Azure.Identity` packages and the Graph-specific classes; update DI in `Program.cs`
- [x] 4.10 Unit tests for EWS SOAP request building and response parsing (recorded XML fixtures, no live server in tests)
- [x] 4.11 Live smoke test against the real mailbox (read-only: counts and domains only) — 16 messages read through the full EWS pipeline on 2026-07-02; sender domains confirmed the directory-driven detection decision (e.g. `colina.cl`, `municipalidadcasablanca.cl` do not follow the `muni<comuna>.cl` pattern). Endpoint switched to `mail.munivalpo.cl` because the TLS certificate SANs do not cover `webmail.munivalpo.cl`.

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
