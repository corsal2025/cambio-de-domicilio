# Design: Address Change Routing

## Context

Single-tenant batch job, run once daily via Windows Task Scheduler (later cron/systemd on a VPS). No concurrent runs expected; idempotency is handled via a `source_message_id` uniqueness constraint rather than distributed locking.

## Decisions

### Runtime: .NET 10 Worker Service, polling every 30 minutes
Revised after requirements gathering: the user needs to know about comuna replies "al momento" (same working day, near-real-time), which a once-daily batch cannot provide. The process now runs continuously as a `BackgroundService`, polling the mailbox on a configurable interval (default 30 minutes). Rejected: once-daily console job (too slow for the reply-detection requirement); polling every 1-5 minutes (unnecessary API load for a human-paced verification workflow — replies don't need sub-minute reaction).

On Windows (current deployment), it runs as a Scheduled Task at logon that keeps running (or a Windows Service) rather than a one-shot task. On the future Linux VPS, it becomes a systemd service instead of a systemd timer.

### Auth: Azure AD app registration, application permissions
Delegated auth would require an interactive user session at every scheduled run, which is incompatible with unattended execution. Application permissions (`Mail.Read`, `Mail.Send`) with `ClientSecretCredential`, scoped to the single target mailbox via an Exchange application access policy, avoid tenant-wide mail access.

### State: SQLite, no ORM
A single-file embedded DB is sufficient for the expected volume (notifications per comuna per day, not high throughput) and needs zero infrastructure — it moves with the app to the VPS unchanged. Plain parameterized SQL over `Microsoft.Data.Sqlite` avoids pulling in EF Core for what is a handful of simple queries against one table plus a reference table.

### Comuna directory: CSV import, re-run each execution
The user maintains the comuna→email mapping in a spreadsheet. Importing it fresh on each run (upsert by comuna) avoids a separate admin UI; the mapping changes rarely, and CSV keeps the format inspectable/editable without special tooling.

### Reply matching: thread-first, RUT-fallback
`conversationId` matching is reliable and cheap when comunas hit Reply. Confirmed in requirements gathering that some comunas send a new email instead — for that path, the system falls back to scanning the new email's body for a RUT already tracked as `sent` from a domain that is a known comuna's domain. No matching heuristic is 100% precise; ambiguous matches are surfaced for manual verification, not auto-resolved silently.

### Notification of a new response: dual channel, environment-aware
"Avisar para verificar" requires near-real-time delivery. Two channels, both fired on every newly-detected `responded` transition:
- **Windows toast notification** (local, on-PC only) — immediate, visible while the operator is at their desk. Uses a native Windows toast API; has no effect on a headless VPS.
- **Email notification** to a configured address (`raul.salazar1984@gmail.com` for now) via the same Graph `sendMail` capability already used for comuna requests — works identically on PC and on the future headless VPS, so no code change is needed at migration time, only configuration (toast channel can be disabled via config on the VPS).

The CSV export remains as the batch-level source of truth for the full tracked list, independent of the real-time notification channels.

## Risks / Trade-offs

- **RUT-fallback matching is a heuristic**: a reply from a different comuna domain mentioning a coincidentally similar RUT format could mis-match. Mitigated by requiring an exact RUT string match plus a `sent`-status precondition; flagged for manual review either way since responses are always meant to be checked before continuing the "tramitación".
- **CSV-only import/export for v1**: no native `.xlsx` — acceptable since Excel opens CSV natively; revisit only if a real formatting need (multiple sheets, formulas) appears.
