# Tasks: "Yo pido" tab — outbound folder requests

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~750-950 (domain+repo+migration+service+worker+config+templates+2 Razor pages+5 filter sites+CSS+6 test files) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 → PR 2 → PR 3 |
| Delivery strategy | ask-on-risk |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Domain + migration + repository (Phases 1-2) | PR 1 | base = feature/f8-outbound-yo-pido tracker branch; additive, no UI |
| 2 | Routing service + RouterWorker + config + templates (Phases 3-4) | PR 2 | base = PR 1 branch; no UI yet, testable via unit/integration tests |
| 3 | Inbound-leak filters + F8 dashboard UI + verification + docs (Phases 5-8) | PR 3 | base = PR 2 branch; ships the operator-visible feature |

## 0. Setup: Create Feature Branch (MANDATORY — FIRST STEP)

- [ ] 0.1 Create `feature/f8-outbound-yo-pido` from `master`
- [ ] 0.2 Verify current branch is `feature/f8-outbound-yo-pido`

## 1. Domain & Migration (TDD)

- [ ] 1.1 RED: `PersonRequestRepositoryTests` — insert a row via raw SQL without `Direction`, re-run `EnsureSchema`, assert it maps as `Inbound`
- [ ] 1.2 GREEN: `Domain/PersonRequest.cs` — add `RequestDirection{Inbound,Outbound}` enum, `Direction` property (default `Inbound`), append `RequestStatus.Requested`
- [ ] 1.3 GREEN: `Persistence/PersonRequestRepository.cs` — `EnsureColumnExists(connection,"Direction","Direction TEXT NOT NULL DEFAULT 'Inbound'")` after `SinCarpeta`; `Insert`/`Map` read/write `Direction`
- [ ] 1.4 Grep `RequestStatus` across `src/`/`tests/`; confirm every site in design.md's blast-radius audit is safe as documented

## 2. Repository: Outbound-specific operations (TDD)

- [ ] 2.1 RED: assert `SetRequested` only moves a Pending+Outbound row; an Inbound Pending row is untouched
- [ ] 2.2 GREEN: `SetRequested(long id, DateTimeOffset requestedAt, long requestedByUserId)` — `WHERE Status='Pending' AND Direction='Outbound'`
- [ ] 2.3 RED: assert `RevertRequestedToPending` only moves a Requested+Outbound row back to Pending, clearing `ConfirmedAt`/`ConfirmedByUserId`
- [ ] 2.4 GREEN: `RevertRequestedToPending(long id)` on interface + impl — `WHERE Status='Requested' AND Direction='Outbound'`
- [ ] 2.5 RED: seed an Inbound row for a RUT+comuna, assert a new Outbound row for the same RUT+comuna is still created (not blocked by dedupe)
- [ ] 2.6 GREEN: add 3-arg `FindByRutAndComuna(rut, comuna, RequestDirection)` and `FindByFullNameAndComuna` overloads (existing 2-arg overloads byte-identical, untouched)

## 3. Routing Service + Email Templates (TDD)

- [ ] 3.1 RED: `AddressChangeRoutingServiceTests` — `ProcessOutboundRequest` extracts name/RUT/comuna, dedupes by `SourceMessageId` and 3-arg RUT+comuna, inserts `Direction=Outbound, Status=Pending`
- [ ] 3.2 GREEN: `AddressChangeRoutingService.ProcessOutboundRequest(email, contacts)`
- [ ] 3.3 RED: `SendOutboundRequestAsync` sends one email to the comuna contact, transitions Pending→Requested; refuses non-Pending or incomplete-data cases with a reason; refuses unknown comuna
- [ ] 3.4 GREEN: `SendOutboundRequestAsync(id, userId, contacts, ct)` + `Notifications/EmailTemplates.RequestFolderToComuna(fullName, rut)` (SGL wording, formal tone, footer via `AppendFooter`)
- [ ] 3.5 RED: `RectifyOutboundRequestAsync` sends a retraction email and reverts Requested→Pending; refuses non-Requested cases with a reason
- [ ] 3.6 GREEN: `RectifyOutboundRequestAsync(requestId, rectifiedByUserId, contacts, ct)` + `EmailTemplates.OutboundRequestRectification(fullName, rut)`, calling `RevertRequestedToPending`

## 4. RouterWorker Third Loop + Configuration

- [ ] 4.1 RED: `RouterWorkerTests` — third loop inserts Outbound Pending rows via `GetMessagesInFolderAsync`; a failure on one email logs and continues the batch (same shape as loop #1)
- [ ] 4.2 GREEN: `RouterWorker.cs` — third polling loop calling `ProcessOutboundRequest`; extend completion log line with the third count/folder pair
- [ ] 4.3 `Configuration/RouterOptions.cs` — add `OutboundSourceFolderName` default `"CARP. PARA PEDIR OTRAS COMUNAS"`; document in `appsettings.Example.json` (not `bin/` copies)

## 5. Inbound-Only Filter (leak prevention) + Regression Tests

- [ ] 5.1 RED: `IndexModelTests`/`CsvReportWriterTests` — an Outbound row never appears in Casos, Sector, SectorF8, Discarded, or the CSV
- [ ] 5.2 GREEN: add `.Where(c => c.Direction == RequestDirection.Inbound)` at `Index.cshtml.cs:366`, `Sector.cshtml.cs:23,59`, `SectorF8.cshtml.cs:24,51` (the `/Certificado` screen was removed)
- [ ] 5.3 Check `Discarded.cshtml.cs:14,27` at implementation time; add the Inbound filter if it surfaces `PersonRequest` rows
- [ ] 5.4 GREEN: `RouterWorker.cs:77` — filter to Inbound at the `CsvReportWriter.Write` call site (writer itself stays Direction-unaware)

## 6. F8 Dashboard UI: "Yo pido" Tab (TDD)

- [ ] 6.1 RED: `F8ModelTests` — `OnGet` splits into `InboundCases` (Destination==F8, Inbound) and `OutboundCases` (Outbound)
- [ ] 6.2 GREEN: `F8.cshtml.cs` — `[BindProperty(SupportsGet=true)] Tab="piden"`; `InboundCases`/`OutboundCases` properties; every existing `RedirectToPage()` becomes `RedirectToPage(new{tab=Tab})`
- [ ] 6.3 RED: `OnPostSendOutboundRequestAsync` calls `SendOutboundRequestAsync` and reloads both case lists
- [ ] 6.4 GREEN: implement `OnPostSendOutboundRequestAsync(id)` mirroring `OnPostConfirmAsync`'s shape
- [ ] 6.5 RED: `OnPostRectifyOutboundRequestAsync` calls `RectifyOutboundRequestAsync`, only enabled for Requested rows
- [ ] 6.6 GREEN: implement `OnPostRectifyOutboundRequestAsync(id)` mirroring `OnPostRectifyConfirmationAsync`
- [ ] 6.7 `F8.cshtml` — add `.page-tabs` links ("Me piden" / "Yo pido"); second table (Nombre/RUT/Comuna/Recibido/Estado/Acción) with "Enviar solicitud" (Pending rows) and "Rectificar solicitud" (Requested rows only) buttons
- [ ] 6.8 `wwwroot/css/dashboard.css` — `.page-tabs`/`.page-tab` block styled after the existing `app-subnav` rules

## 7. Mandatory Verification

- [ ] 7.1 Review and update existing unit tests touched by the Inbound-filter change (Phase 5) for regressions
- [ ] 7.2 Run targeted unit tests for `Persistence`, `Routing`, `RouterWorker`, `Dashboard.Pages.F8` — capture pass/fail counts
- [ ] 7.3 Run full test suite; verify no SQLite test-db mutation leaks between runs; report results in `openspec/changes/f8-outbound-yo-pido/reports/YYYY-MM-DD-unit-test-and-db-verification.md`
- [ ] 7.4 E2E (Playwright MCP, AGENT MUST EXECUTE): start dashboard locally, log in, navigate to `/F8`, switch to "Yo pido" tab, click "Enviar solicitud" on a seeded Pending row, verify it moves to Requested, click "Rectificar solicitud", verify it reverts to Pending; restore seeded test data afterward

## 8. Documentation

- [ ] 8.1 Update `docs/flujo-proceso.md` with the outbound "Yo pido" step and the rectify sub-flow
- [ ] 8.2 Update `docs/data-model.md` for the `Direction` column and `Requested` status
- [ ] 8.3 README/email-templates doc refresh: `RequestFolderToComuna` and `OutboundRequestRectification` wording
