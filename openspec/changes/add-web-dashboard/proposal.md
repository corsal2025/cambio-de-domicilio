# Proposal: Web Dashboard with Supervised Classification

## Why

The routing service (change `add-address-change-routing`) runs headless: its only outputs are a CSV file and notifications. The operator now needs to (a) work with the data interactively from a clickable app on their PC, (b) let several colleagues on the municipal network see the same state in real time, (c) track the **inverse flow** — folder requests that *other* comunas send to Valparaíso — separately from the requests Valparaíso sends out, and (d) print formal report documents. A headless CSV cannot serve any of that.

## What Changes

- Add an embedded **web dashboard** (ASP.NET Core, same process as the worker): the service keeps polling in the background and additionally serves a local web UI. Desktop shortcut opens it in the browser; colleagues on the municipal LAN reach the same URL and see live state.
- Add **per-user authentication**: local user accounts (SQLite-backed, salted password hashes, cookie sessions). No anonymous access — the dashboard displays personal data (names, RUTs).
- Add **bidirectional mail classification** with supervision:
  - Every mail from a known comuna domain is auto-classified as one of: `OutgoingReply` (a reply to a request Valparaíso sent), `IncomingRequest` (another comuna asks Valparaíso for a folder), `AddressChangeNotification`, or `Unclassified`.
  - The dashboard shows the proposed classification and lets an authenticated user **confirm or reclassify** any item (human-in-the-loop). Manual decisions are stored and never overwritten by the auto-classifier.
- Add **two separate report views**, switchable by button: "Solicitudes enviadas" (requests Valparaíso sent, with their reply status) and "Solicitudes recibidas" (requests other comunas made to Valparaíso).
- Add a **printable document** view (browser print → paper or PDF): full name, RUT, date of last folder, and — when the case originates in an address change from another comuna — which comuna it is.
- Package as a **portable self-contained single-file executable** (`dotnet publish`), copyable to another PC without installing the .NET runtime.
- Structure the codebase as **feature modules over a shared kernel** (auth, storage, mail transport, notifications behind interfaces; navigation driven by a module registry) so future municipal workflow modules attach without touching existing ones.
- Source-code protection expectation set honestly: distribution is compiled binaries and the repository stays private; absolute anti-copy protection of code does not exist and is not promised.

## Impact

- Affected capability: `dashboard` (new), extends `routing` (classification field on tracked mail)
- New systems touched: ASP.NET Core (Kestrel) listener on the LAN, local user store in SQLite, browser print CSS
- Depends on: `add-address-change-routing` (EWS integration and SQLite store must exist first)
- Security posture: personal data becomes visible over the network → authentication is mandatory, the listener serves only over HTTPS on the LAN (plain HTTP redirects, never serves data), the print/report views are behind the same session, and every manual classification decision is attributed to the user and timestamp that made it
