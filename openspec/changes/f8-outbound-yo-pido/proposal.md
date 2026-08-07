# Proposal: "Yo pido" tab — outbound folder requests in F8

## Why

Today the system models exactly one direction: another comuna requests a person's folder **from** Valparaíso ("Me piden"). The operator also has the reverse need — Valparaíso must request a person's folder **from** another comuna ("Yo pido") — and does it entirely by hand: reading the mail, identifying name/RUT/comuna, looking up the comuna's contact address, and drafting a formal email asking them to upload the folder to the SGL platform. That paperwork is the same paperwork the inbound flow already automates, so it should be automated the same way, without disturbing the inbound flow that is in production.

## What Changes

- **New Outlook folder** `"CARP. PARA PEDIR OTRAS COMUNAS"`, polled by `RouterWorker.RunCycleAsync` as a third loop alongside `SourceFolderName` and `ConfirmationFolderName`; configured by new `RouterOptions.OutboundSourceFolderName` (documented in `appsettings.Example.json`).
- **Direction on the record**: new `RequestDirection { Inbound, Outbound }`, default `Inbound`; persisted as an additive `Direction TEXT NOT NULL DEFAULT 'Inbound'` column via the repository's existing `EnsureColumnExists` migration pattern. Existing rows keep inbound behavior untouched.
- **New status** `RequestStatus.Requested`, used only by outbound. Outbound has **no** `Uploaded` step and **no** PDF/Conaset step — Valparaíso only sends one formal request and waits.
- **New `AddressChangeRoutingService.ProcessOutboundRequest(email, contacts)`**, mirroring `ProcessIncomingRequest`: `SourceMessageId` dedupe, own-domain skip, comuna resolution via `IComunaDirectory`, person extraction reusing `PersonDataExtractor` (body + subject merge), insert as `Status = Pending`, `Direction = Outbound`. **Decision:** per-person dedupe (`FindByRutAndComuna` / `FindByFullNameAndComuna`) is scoped to `Direction = Outbound`, so an unrelated inbound row for the same RUT+comuna never blocks an outbound request.
- **New `AddressChangeRoutingService.SendOutboundRequestAsync(id, userId, contacts, ct)`**: sends one formal email via `IMailSender` to the comuna's `ContactEmail` using a new `EmailTemplates` template asking that comuna to upload the person's folder to the **SGL** platform (their own upload system — explicitly not Conaset), then transitions the record to `Requested` with actor/timestamp audit fields.
- **F8 page gains a tab switcher**: "Me piden" (existing inbound list and behavior, unchanged) and "Yo pido" (outbound list, single action "Enviar solicitud" — no Traspaso, no Marcar subida, no PDF). **Decision:** active tab is carried in the query string (server-side, no client state), so redirects after POST and page refresh preserve the tab. New CSS block in `dashboard.css` — no tab pattern exists today.
- **CSV report excludes outbound rows.** `ReportCsvPath` columns (`fecha_ultima_carpeta`, `status`, `sector`, `confirmed_at`) are inbound-workflow-specific and do not apply.
- Audit all `switch` statements over `RequestStatus` to confirm `Requested` is handled or safely falls through a default arm.

## Impact

- Affected capabilities: `routing` (outbound request detection + outbound request email), `dashboard` (F8 tabs and outbound action). No new capability.
- Files: `Domain/PersonRequest.cs`, `Persistence/PersonRequestRepository.cs`, `Routing/AddressChangeRoutingService.cs`, `RouterWorker.cs`, `Configuration/RouterOptions.cs`, `appsettings.Example.json`, `Notifications/EmailTemplates.cs`, `Dashboard/Pages/F8.cshtml` + `F8.cshtml.cs`, `wwwroot/css/dashboard.css`, `Reporting/CsvReportWriter.cs`.
- Tests: `AddressChangeRoutingServiceTests.cs`, `PersonRequestRepositoryTests.cs`, `RouterWorkerTests.cs`, `F8ModelTests.cs`.
- No new external dependency. Schema change is purely additive; rollback is reverting the code — the extra column and any `Outbound` rows are inert for the inbound flow.

## Success Criteria

- [ ] An email in `"CARP. PARA PEDIR OTRAS COMUNAS"` produces outbound `Pending` rows visible only under "Yo pido".
- [ ] "Enviar solicitud" sends one SGL-request email to the resolved comuna contact and moves the row to `Requested`.
- [ ] "Me piden" tab behavior, existing rows, and the CSV report are byte-for-byte unchanged.
