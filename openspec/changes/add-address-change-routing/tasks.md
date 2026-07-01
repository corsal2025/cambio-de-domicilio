# Tasks: Address Change Routing

## 1. Project scaffold
- [ ] 1.1 Create `.sln` + `src/OutlookComunaRouter` console project (net10.0)
- [ ] 1.2 Add packages: `Microsoft.Graph`, `Azure.Identity`, `Microsoft.Data.Sqlite`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.*`
- [ ] 1.3 `.gitignore`, `appsettings.json` (placeholders) + `appsettings.Example.json`, README with Azure AD app registration steps

## 2. Domain and persistence
- [ ] 2.1 `PersonRequest` and `ComunaContact` records (Domain/)
- [ ] 2.2 SQLite schema creation (migrations-as-code on startup) + repository with idempotent upsert by `source_message_id`
- [ ] 2.3 Unit tests for repository against a temp SQLite file

## 3. Comuna directory import
- [ ] 3.1 CSV parser for comuna → contact_email → domain
- [ ] 3.2 Upsert into `ComunaContact` table on each run
- [ ] 3.3 Unit tests: valid rows, duplicate comuna, malformed row

## 4. Graph integration
- [ ] 4.1 Graph client factory using `ClientSecretCredential` from configuration
- [ ] 4.2 `IEmailReader`: list unprocessed inbox messages since last run
- [ ] 4.3 `IComunaMailSender`: send predefined folder-request email via `sendMail`

## 5. Extraction and routing logic
- [ ] 5.1 Regex-based extractor for `full_name` + `rut` from email body (unit tests against sample bodies)
- [ ] 5.2 Domain-based comuna detection (`muni<comuna>.cl`, excluding own domain) (unit tests)
- [ ] 5.3 Routing service: pending -> sent transition, missing-data/unknown-comuna stays pending

## 6. Reply detection
- [ ] 6.1 Thread-based match (`conversationId`) marks `responded`
- [ ] 6.2 RUT-fallback match for new-thread replies from comuna domains
- [ ] 6.3 Unit tests for both matching paths, including a non-matching case

## 7. Reporting
- [ ] 7.1 CSV export: `full_name`, `rut`, `last_folder_date`
- [ ] 7.2 Unit test: export shape and empty-value handling for not-yet-responded rows

## 8. Orchestration
- [ ] 8.1 `Program.cs` composition root wiring DI, config, and the daily pipeline (read -> extract -> route -> match replies -> export)
- [ ] 8.2 Structured logging without PII (IDs/counts only)
- [ ] 8.3 `dotnet build` + `dotnet test` green; manual end-to-end dry run against a test mailbox
