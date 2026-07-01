# Design: Address Change Routing

## Context

Single-tenant, long-lived background process, polling on a configurable interval (default 30 min). Each polling cycle must not overlap the previous one (guarded in-process) and idempotency is handled via a `source_message_id` uniqueness constraint rather than distributed locking, since only one instance runs at a time.

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

### Notification-source detection: directory-driven, not a guessed domain pattern
Earlier draft assumed all comuna domains follow `muni<comuna>.cl` — not verified, and fragile for comunas whose name doesn't map cleanly to a domain fragment (accents, multi-word names). Detection now checks the sender domain against the `domain` column already present in `ComunaContact` (the directory the user imports), which is the same source of truth used later to resolve where to send the request. A domain not in the directory is not treated as a candidate notification, but does not error — it's simply not routing-relevant mail.

### Duplicate prevention: unique on (rut, comuna) for active requests
A `source_message_id` uniqueness constraint only prevents reprocessing the exact same email twice. It does not stop two different emails (e.g. a resend) from creating two outgoing requests for the same person. Added a check before sending: if a `PersonRequest` with the same normalized `rut` + `comuna` already has `status IN (sent, responded)`, the new source email is linked to the existing record (its `source_message_id` recorded in a secondary table) instead of creating a new outgoing request.

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
- **PII at rest, unencrypted**: the SQLite file and the continuously-refreshed CSV report both contain full names and RUTs in plain text on a work laptop. This relies on the machine's disk encryption (BitLocker) and normal account access control — the application does not add its own encryption layer in v1. Flagged as an operational dependency, not solved in code: confirm BitLocker (or equivalent) is enabled on the machine this runs on, and treat the report file path as not-shareable outside authorized staff.
- **Overlap guard is in-process only**: if the machine sleeps/hibernates mid-cycle or the process crashes mid-write, there's no external lock — acceptable for a single-machine deployment, but would need a real lock (e.g. a lease row in SQLite) if ever run from two machines against the same mailbox.
