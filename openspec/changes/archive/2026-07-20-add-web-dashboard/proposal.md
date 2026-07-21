# Proposal: Web Dashboard for the Upload-Confirmation Flow

## Why

The routing service (`add-address-change-routing`, later revised by `add-upload-confirmation-flow`) runs headless: its only outputs are a CSV file, a toast, and an email. Three things now require a real UI, not a background service:

1. **Entering the "fecha de última carpeta" per case** — this is manual data the operator types in, there is no email or automated source for it.
2. **Triggering the confirmation email deliberately** — `SendConfirmationAsync` exists in code but has no caller; the operator decides *when* to press "send," and that decision needs a UI action, not a timer.
3. **Generating the sector PDF** — a printable list of cases (Archivo or Oficina 43) to take when physically retrieving folders.

A CSV file cannot serve any of these — they are actions, not just data to view.

## What Changes

- Add an embedded **web dashboard** (ASP.NET Core, same process as the worker): the service keeps polling in the background and additionally serves a local web UI. Desktop shortcut opens it in the browser; colleagues on the municipal LAN reach the same URL and see live state.
- Add **per-user authentication**: local user accounts (SQLite-backed, salted password hashes, cookie sessions). No anonymous access — the dashboard displays personal data (names, RUTs).
- Add a **single case list view** reflecting the real lifecycle (`Pending` → `Uploaded` → `Confirmed`), with:
  - An editable "fecha de última carpeta" field per case (the only manual data entry point in the whole system).
  - An "Enviar confirmación" button on `Uploaded` cases, wired to the existing `SendConfirmationAsync` — disabled/hidden for cases that are not eligible (still `Pending`, already `Confirmed`, or missing data), matching the guard already implemented in the service.
  - A "Requiere revisión" filter for cases with incomplete extracted data.
- Add **sector PDF generation**: a button per sector (Archivo / Oficina 43) that renders a printable document (full name, RUT, comuna, fecha de última carpeta) via the browser's print-to-PDF, for the cases in that sector.
- **Attribute every confirmation send** to the logged-in user and timestamp — this is a real send of an official email to another municipality; who pressed the button must be recorded.
- Package as a **portable self-contained single-file executable** (`dotnet publish`), copyable to another PC without installing the .NET runtime.
- Structure the codebase as **feature modules over a shared kernel** (auth, storage, mail transport, notifications behind interfaces; navigation driven by a module registry) so future municipal workflow modules attach without touching existing ones.
- Source-code protection expectation set honestly: distribution is compiled binaries and the repository stays private; absolute anti-copy protection of code does not exist and is not promised.

## Impact

- Affected capability: `dashboard` (new), extends `routing` (adds `confirmed_by_user_id` attribution to `PersonRequest`)
- New systems touched: ASP.NET Core (Kestrel) listener on the LAN, local user store in SQLite, browser print CSS
- Depends on: `add-address-change-routing` (EWS integration, SQLite store) and `add-upload-confirmation-flow` (the `Pending`/`Uploaded`/`Confirmed` lifecycle, `SendConfirmationAsync`, sector derivation) both being in place
- Security posture: personal data becomes visible over the network → authentication is mandatory, the listener serves only over HTTPS on the LAN (plain HTTP redirects, never serves data), the print views are behind the same session, and every confirmation send is attributed to the user who triggered it
- Supersedes the bidirectional-classification design from the original draft of this change (`OutgoingReply`/`IncomingRequest`/`AddressChangeNotification`/`Unclassified`, two report views) — that no longer matches the flow: there is only one direction (comunas request folders from Valparaíso) and no ambiguity to classify, since folder membership (`CARP. PARA PEDIR` vs `CARP. YA SUBIDAS`) already determines the state deterministically
