# Proposal: Address Change Routing (CambioDeDomicilio)

## Why

Valparaíso municipality receives "cambio de domicilio" (address change) notifications by email from other comunas' municipal systems (senders on `muni<comuna>.cl` domains, e.g. `rfloresc@municatemu.cl`). For each notified person, staff must manually request that comuna's latest case folder ("última carpeta") for the person, track whether that request was sent, and watch for the reply before continuing the "tramitación". This is done by hand today and does not scale.

## What Changes

- Run as a long-lived background process that polls the mailbox `cambiodedomicilio@munivalpo.cl` via Microsoft Graph every 30 minutes (configurable) and detects address-change notifications by sender domain (`muni<comuna>.cl`, excluding the organization's own `munivalpo.cl` domain).
- Extract `full_name` (case-insensitive) and `rut` (with or without dots, normalized) and `comuna` from each notification body.
- Resolve the comuna's contact email from a directory imported from a CSV/Excel file the user provides.
- Send a predefined, formally-worded "please send latest folder" email to that contact, and record the request as `sent`.
- Detect comuna replies (same thread, or new email from a recognized comuna domain referencing the same RUT) and mark the matching request as `responded`.
- On every new `responded` transition, notify the operator via two channels: a Windows toast (on-PC only) and an email to a configured address (works on PC and future VPS alike).
- Provide a CSV export of tracked people: `full_name`, `rut`, `last_folder_date` (as requested by comunas today).
- Guarantee idempotency: polling cycles never re-send a request for an already-processed source email.

## Impact

- Affected capability: `routing` (new)
- New systems touched: Microsoft Graph (mailbox read/send, application permissions), local SQLite state file, CSV import/export, Windows toast notifications
- No existing code to break — this is a new project (`outlook-comuna-router`, private repo)
- **External dependency / blocker**: requires an Azure AD app registration with `Mail.Read`/`Mail.Send` application permissions and admin consent, scoped to `cambiodedomicilio@munivalpo.cl`. The user does not have Azure AD admin access — this must be coordinated with the municipality's IT team before the app can run against the real mailbox. Development/testing can proceed against a test mailbox in the meantime.
