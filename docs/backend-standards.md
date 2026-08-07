---
description: Backend development standards for the CambioDeDomicilio .NET console application (Microsoft Graph, SQLite, scheduled batch job)
globs: ["src/**/*.cs", "**/*.csproj", "**/*.sln"]
alwaysApply: true
---

# Backend Project Standards and Best Practices

## Overview

CambioDeDomicilio is a **.NET 10 console application** that runs as a **scheduled task** (Windows Task Scheduler locally, cron/systemd timer on a future Linux VPS). It has no HTTP API surface and no frontend. Each run processes a batch of emails and exits.

## Technology Stack

- **Runtime**: .NET 10 (console app, no ASP.NET)
- **Email/Identity**: `Microsoft.Graph` SDK + `Azure.Identity` (`ClientSecretCredential` for unattended app-only auth)
- **State storage**: SQLite (`Microsoft.Data.Sqlite`), single file DB, no ORM — plain parameterized SQL
- **Configuration**: `Microsoft.Extensions.Configuration` (appsettings.json + environment variables + user-secrets in development). No secrets ever committed to git.
- **Logging**: `Microsoft.Extensions.Logging` console provider. Never log PII (full name, RUT, email body) — log only internal IDs and counts.
- **Import/Export**: CSV (comuna→contact directory import, tracking report export)

## Architecture

Simple layered structure, no over-engineering (this is a batch job, not a service):

```
src/CambioDeDomicilio/
  Graph/            # Graph client factory + auth
  Domain/           # PersonRecord, ComunaContact, RequestStatus
  Processing/       # email filtering, data extraction (regex), comuna matching
  Repositories/     # SQLite access (tracking table)
  Reporting/        # CSV export
  Configuration/     # strongly-typed options bound from appsettings
  Program.cs        # composition root, orchestrates the daily run
```

## Coding Standards

- Nullable reference types enabled; no `null` leaks across layer boundaries without explicit handling.
- Prefer records for immutable domain data (`PersonRecord`, `ComunaContact`).
- Interfaces for anything that talks to Graph or SQLite, so the extraction/matching logic can be unit tested without live I/O.
- No PII in exceptions messages that could end up in logs.

## Testing Standards

- Unit test the regex-based extractor (`Nombre`, `RUT`, `Comuna`) against representative sample email bodies.
- Unit test comuna-domain matching (`muni<comuna>.cl` pattern, excluding own domain).
- Integration-level tests for SQLite repository against a temp DB file — no mocking the DB, per project convention: real SQLite in tests, not an in-memory fake.

## Security

- No tenant ID / client ID / client secret hardcoded anywhere. All from configuration, `appsettings.Development.json` and `.env` are gitignored.
- Least-privilege Graph permissions: `Mail.Read` and `Mail.Send` (Application permissions, admin consent) on the specific mailbox only, via application access policy — do not grant tenant-wide mail access.
- Treat RUT and full names as personal data: no PII in logs, no PII in unencrypted exports left in shared folders without access control.
