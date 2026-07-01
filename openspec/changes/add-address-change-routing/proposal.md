# Proposal: Address Change Routing (OutlookComunaRouter)

## Why

Valparaíso municipality receives "cambio de domicilio" (address change) notifications by email from other comunas' municipal systems (senders on `muni<comuna>.cl` domains, e.g. `rfloresc@municatemu.cl`). For each notified person, staff must manually request that comuna's latest case folder ("última carpeta") for the person, track whether that request was sent, and watch for the reply before continuing the "tramitación". This is done by hand today and does not scale.

## What Changes

- Read a shared/target Outlook mailbox daily via Microsoft Graph and detect address-change notifications by sender domain (`muni<comuna>.cl`, excluding the organization's own `munivalpo.cl` domain).
- Extract `full_name`, `rut`, and `comuna` from each notification body.
- Resolve the comuna's contact email from a directory imported from a CSV/Excel file the user provides.
- Send a predefined "please send latest folder" email to that contact, and record the request as `sent`.
- Detect comuna replies (same thread, or new email from a recognized comuna domain referencing the same RUT) and mark the matching request as `responded`, surfacing it for manual verification.
- Provide a CSV export of tracked people: `full_name`, `rut`, `last_folder_date` (as requested by comunas today).
- Guarantee idempotency: re-running the daily job never re-sends a request for an already-processed source email.

## Impact

- Affected capability: `routing` (new)
- New systems touched: Microsoft Graph (mailbox read/send, application permissions), local SQLite state file, CSV import/export
- No existing code to break — this is a new project (`outlook-comuna-router`, private repo)
