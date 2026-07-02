# Design: Address Change Routing

## Context

Single-tenant, long-lived background process, polling on a configurable interval (default 30 min). Each polling cycle must not overlap the previous one (guarded in-process) and idempotency is handled via a `source_message_id` uniqueness constraint rather than distributed locking, since only one instance runs at a time.

## Decisions

### Runtime: .NET 10 Worker Service, polling every 30 minutes
Revised after requirements gathering: the user needs to know about comuna replies "al momento" (same working day, near-real-time), which a once-daily batch cannot provide. The process now runs continuously as a `BackgroundService`, polling the mailbox on a configurable interval (default 30 minutes). Rejected: once-daily console job (too slow for the reply-detection requirement); polling every 1-5 minutes (unnecessary API load for a human-paced verification workflow — replies don't need sub-minute reaction).

On Windows (current deployment), it runs as a Scheduled Task at logon that keeps running (or a Windows Service) rather than a one-shot task. On the future Linux VPS, it becomes a systemd service instead of a systemd timer.

### Mail integration: on-premises Exchange 2016 via EWS (supersedes the Azure AD / Graph decision)
The original design assumed the mailbox lived in Exchange Online and required an Azure AD app registration with application permissions — an external blocker, since the operator has no tenant admin access. Live verification during implementation disproved the assumption:

- `munivalpo.cl` MX points to Exchange Online Protection, **but** the target mailbox `cambiodedomicilio@munivalpo.cl` does not exist as an Entra ID sign-in (`GetCredentialType` → `IfExistsResult=1` for every plausible UPN variant): it is a hybrid deployment and this mailbox lives **on-premises**.
- `webmail.munivalpo.cl` is an on-prem **Exchange Server 2016** (IIS 10, `X-FEServer` header, `/owa/` redirect) with a valid TLS certificate issued to the municipality (expires 2027-02-23).
- Its EWS endpoint (`/EWS/Exchange.asmx`) is exposed and accepts **Basic and NTLM** auth; the mailbox's own AD credentials (`servervalpo\cambiodedomicilio`) authenticate successfully, and a `GetFolder(inbox)` SOAP call returns the real mailbox (verified live).

Consequences:
- Microsoft Graph **cannot** reach this mailbox (Graph only serves Exchange Online mailboxes; hybrid REST access was retired). The Graph integration layer is replaced by an EWS SOAP client.
- **No Azure AD registration, no admin consent, no IT dependency** — the external blocker (former task 0.1) disappears entirely. Auth is the mailbox's own AD credentials over Basic auth on TLS (Basic chosen over NTLM as primary: guaranteed cross-platform for the future Linux VPS without GSSAPI/NTLM native libraries; the TLS channel and single-purpose credential bound the exposure).
- EWS is implemented as **raw SOAP over `HttpClient`** (`FindItem` + `GetItem` for reading, `CreateItem` with `SendAndSaveCopy` for sending) instead of an EWS client package: the official `Microsoft.Exchange.WebServices` package is .NET Framework-only and unmaintained, and community .NET Standard forks are low-governance dependencies for what is three well-documented SOAP operations.
- **Idempotency key changes from Graph message ID to `InternetMessageId`** (the RFC 5322 `Message-ID` header): EWS `ItemId` is not stable (it changes if an item is moved between folders), while `InternetMessageId` is immutable and globally unique. `ConversationId` remains available in EWS (Exchange 2010+) so thread-based reply matching is unchanged.
- Credentials live in `appsettings.Development.json` (git-ignored) / environment variables in production, same handling the client secret would have had.
- Deployment reach: the EWS endpoint resolves publicly and is reachable from inside the municipal network (verified); reachability from an external VPS must be re-verified at migration time — if it is firewalled externally, the VPS needs a VPN/tunnel into the municipal network or the service stays on an in-network machine.

### State: SQLite, no ORM
A single-file embedded DB is sufficient for the expected volume (notifications per comuna per day, not high throughput) and needs zero infrastructure — it moves with the app to the VPS unchanged. Plain parameterized SQL over `Microsoft.Data.Sqlite` avoids pulling in EF Core for what is a handful of simple queries against one table plus a reference table.

### Comuna directory: CSV import, re-run each execution
The user maintains the comuna→email mapping in a spreadsheet. Importing it fresh on each run (upsert by comuna) avoids a separate admin UI; the mapping changes rarely, and CSV keeps the format inspectable/editable without special tooling.

### Notification-source detection: directory-driven, not a guessed domain pattern
Earlier draft assumed all comuna domains follow `muni<comuna>.cl` — not verified, and fragile for comunas whose name doesn't map cleanly to a domain fragment (accents, multi-word names). Detection now checks the sender domain against the `domain` column already present in `ComunaContact` (the directory the user imports), which is the same source of truth used later to resolve where to send the request. A domain not in the directory is not treated as a candidate notification, but does not error — it's simply not routing-relevant mail.

### Duplicate prevention: unique on (rut, comuna) for active requests
A `source_message_id` uniqueness constraint only prevents reprocessing the exact same email twice. It does not stop two different emails (e.g. a resend) from creating two outgoing requests for the same person. Added a check before sending: if a `PersonRequest` with the same normalized `rut` + `comuna` already has `status IN (sent, responded)`, the new source email is linked to the existing record (its `source_message_id` recorded in a secondary table) instead of creating a new outgoing request.

### Reply matching: thread-first, RUT-fallback
`conversationId` matching is reliable and cheap when comunas hit Reply. Confirmed in requirements gathering that some comunas send a new email instead — for that path, the system falls back to scanning the new email's body for a RUT already tracked as `sent` from a domain that is a known comuna's domain. No matching heuristic is 100% precise; ambiguous matches are surfaced for manual verification, not auto-resolved silently.

### Notification of a new response: dual channel, environment-aware
"Avisar para verificar" requires near-real-time delivery. Two channels, both fired on every newly-detected `responded` transition:
- **Windows toast notification** (local, on-PC only) — immediate, visible while the operator is at their desk. Implemented via a direct PowerShell/WinRT call (`Windows.UI.Notifications`) rather than the `Microsoft.Toolkit.Uwp.Notifications` NuGet package: that package pulled a **critical-severity CVE** (`System.Drawing.Common` 4.7.0, GHSA-rxg9-xrhp-64gj) and would have forced the whole project onto a Windows-only target framework (`net10.0-windows`), contradicting the planned Linux VPS migration. The native call keeps the main project cross-platform-buildable; the channel is still a runtime no-op on non-Windows and best-effort (wrapped in try/catch, never fails the polling cycle).
- **Email notification** to a configured address (`raul.salazar1984@gmail.com` for now) via the same mail-sending capability already used for comuna requests (EWS `CreateItem`) — works identically on PC and on the future headless VPS, so no code change is needed at migration time, only configuration (toast channel can be disabled via config on the VPS).

The CSV export remains as the batch-level source of truth for the full tracked list, independent of the real-time notification channels.

## Risks / Trade-offs

- **RUT-fallback matching is a heuristic**: a reply from a different comuna domain mentioning a coincidentally similar RUT format could mis-match. Mitigated by requiring an exact RUT string match plus a `sent`-status precondition; flagged for manual review either way since responses are always meant to be checked before continuing the "tramitación".
- **CSV-only import/export for v1**: no native `.xlsx` — acceptable since Excel opens CSV natively; revisit only if a real formatting need (multiple sheets, formulas) appears.
- **PII at rest, unencrypted**: the SQLite file and the continuously-refreshed CSV report both contain full names and RUTs in plain text on a work laptop. This relies on the machine's disk encryption (BitLocker) and normal account access control — the application does not add its own encryption layer in v1. Flagged as an operational dependency, not solved in code: confirm BitLocker (or equivalent) is enabled on the machine this runs on, and treat the report file path as not-shareable outside authorized staff.
- **Overlap guard is in-process only**: if the machine sleeps/hibernates mid-cycle or the process crashes mid-write, there's no external lock — acceptable for a single-machine deployment, but would need a real lock (e.g. a lease row in SQLite) if ever run from two machines against the same mailbox.
