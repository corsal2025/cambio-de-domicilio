# Tasks: Address Change Routing

## 0. Prerequisite (external, blocking real-mailbox testing)
- [ ] 0.1 Coordinate with municipality IT: Azure AD app registration with `Mail.Read`/`Mail.Send` application permissions and admin consent, scoped to `cambiodedomicilio@munivalpo.cl` via an application access policy. Development can proceed against a test mailbox meanwhile.

## 1. Project scaffold
- [ ] 1.1 Create `.sln` + `src/OutlookComunaRouter` Worker Service project (net10.0, `Microsoft.Extensions.Hosting` `BackgroundService`)
- [ ] 1.2 Add packages: `Microsoft.Graph`, `Azure.Identity`, `Microsoft.Data.Sqlite`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.*`, Windows toast notification package (e.g. `CommunityToolkit.WinUI.Notifications` or `Microsoft.Toolkit.Uwp.Notifications`)
- [ ] 1.3 `.gitignore`, `appsettings.json` (placeholders) + `appsettings.Example.json` (poll interval, mailbox, notification email, toast enabled/disabled), README with Azure AD app registration steps and Windows Task Scheduler / Windows Service setup

## 2. Domain and persistence
- [ ] 2.1 `PersonRequest` and `ComunaContact` records (Domain/)
- [ ] 2.2 SQLite schema creation (migrations-as-code on startup) + repository with idempotent upsert by `source_message_id`
- [ ] 2.3 Unit tests for repository against a temp SQLite file

## 3. Comuna directory import
- [ ] 3.1 CSV parser for comuna → contact_email → domain
- [ ] 3.2 Upsert into `ComunaContact` table on each polling cycle
- [ ] 3.3 Unit tests: valid rows, duplicate comuna, malformed row

## 4. Graph integration
- [ ] 4.1 Graph client factory using `ClientSecretCredential` from configuration
- [ ] 4.2 `IEmailReader`: list unprocessed messages in `cambiodedomicilio@munivalpo.cl` since last cycle
- [ ] 4.3 `IComunaMailSender`: send the formal folder-request email (template in `docs/email-templates.md`) via `sendMail`
- [ ] 4.4 `INotificationSender`: send the reply-notification email via the same Graph client

## 5. Extraction and routing logic
- [ ] 5.1 Regex-based extractor for `full_name` (case-insensitive) + `rut` (with/without dots, normalized to canonical form) from email body (unit tests against sample bodies in both formats)
- [ ] 5.2 Domain-based comuna detection (`muni<comuna>.cl`, excluding own domain) (unit tests)
- [ ] 5.3 Routing service: pending -> sent transition, missing-data/unknown-comuna stays pending

## 6. Reply detection and notification
- [ ] 6.1 Thread-based match (`conversationId`) marks `responded`
- [ ] 6.2 RUT-fallback match for new-thread replies from comuna domains (normalized RUT comparison)
- [ ] 6.3 Unit tests for both matching paths, including a non-matching case
- [ ] 6.4 Windows toast notification on `responded` transition (guarded by config flag, no-op on non-Windows/headless)
- [ ] 6.5 Email notification on `responded` transition, always fired regardless of toast availability

## 7. Reporting
- [ ] 7.1 CSV export: `full_name`, `rut`, `last_folder_date`
- [ ] 7.2 Unit test: export shape and empty-value handling for not-yet-responded rows

## 8. Orchestration
- [ ] 8.1 `Worker : BackgroundService` composition root: on each interval tick, run read -> extract -> route -> match replies -> notify -> export
- [ ] 8.2 Configurable poll interval (default 30 min) via `appsettings.json`
- [ ] 8.3 Structured logging without PII (IDs/counts only)
- [ ] 8.4 `dotnet build` + `dotnet test` green; manual end-to-end dry run against a test mailbox (once task 0.1 unblocks real-mailbox access)
