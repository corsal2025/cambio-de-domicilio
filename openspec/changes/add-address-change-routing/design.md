# Design: Address Change Routing

## Context

Single-tenant batch job, run once daily via Windows Task Scheduler (later cron/systemd on a VPS). No concurrent runs expected; idempotency is handled via a `source_message_id` uniqueness constraint rather than distributed locking.

## Decisions

### Runtime: .NET 10 console app, not a Worker Service
The job runs once and exits per invocation — a long-lived polling Worker Service would add complexity (hosting, shutdown handling) with no benefit for a once-daily batch. Rejected: ASP.NET Worker Service (over-engineered for this cadence).

### Auth: Azure AD app registration, application permissions
Delegated auth would require an interactive user session at every scheduled run, which is incompatible with unattended execution. Application permissions (`Mail.Read`, `Mail.Send`) with `ClientSecretCredential`, scoped to the single target mailbox via an Exchange application access policy, avoid tenant-wide mail access.

### State: SQLite, no ORM
A single-file embedded DB is sufficient for the expected volume (notifications per comuna per day, not high throughput) and needs zero infrastructure — it moves with the app to the VPS unchanged. Plain parameterized SQL over `Microsoft.Data.Sqlite` avoids pulling in EF Core for what is a handful of simple queries against one table plus a reference table.

### Comuna directory: CSV import, re-run each execution
The user maintains the comuna→email mapping in a spreadsheet. Importing it fresh on each run (upsert by comuna) avoids a separate admin UI; the mapping changes rarely, and CSV keeps the format inspectable/editable without special tooling.

### Reply matching: thread-first, RUT-fallback
`conversationId` matching is reliable and cheap when comunas hit Reply. Confirmed in requirements gathering that some comunas send a new email instead — for that path, the system falls back to scanning the new email's body for a RUT already tracked as `sent` from a domain that is a known comuna's domain. No matching heuristic is 100% precise; ambiguous matches are surfaced for manual verification, not auto-resolved silently.

### Notification of a new response: report-based, not push
Given the batch nature (daily run, no long-lived process), "avisar para verificar" is satisfied by the `responded` status being visible in the exported CSV each run, rather than an active push notification. If real-time alerting is needed later, revisit as a separate change (e.g., emailing a daily summary to staff).

## Risks / Trade-offs

- **RUT-fallback matching is a heuristic**: a reply from a different comuna domain mentioning a coincidentally similar RUT format could mis-match. Mitigated by requiring an exact RUT string match plus a `sent`-status precondition; flagged for manual review either way since responses are always meant to be checked before continuing the "tramitación".
- **CSV-only import/export for v1**: no native `.xlsx` — acceptable since Excel opens CSV natively; revisit only if a real formatting need (multiple sheets, formulas) appears.
