# Design: "Yo pido" tab — outbound folder requests

Outbound is modeled as a *direction attribute on the existing `PersonRequest` aggregate*, not as a new
entity. Everything already built for Inbound (repository, extractor, comuna directory, mail sender,
F8 page) is reused; the Inbound code paths are never edited, only *narrowed* with an explicit
`Direction == Inbound` filter so new Outbound rows cannot leak into existing screens or the CSV.

## Decisions

### Direction as an additive TEXT column, exactly like `Destination`
`RequestDirection { Inbound, Outbound }` in `Domain/PersonRequest.cs`; `PersonRequest.Direction`
defaults to `Inbound`. Repository migration adds one line to `EnsureSchema()`, after `SinCarpeta`:

```csharp
EnsureColumnExists(connection, "Direction", "Direction TEXT NOT NULL DEFAULT 'Inbound'");
```

`Map()` reads `Direction = Enum.Parse<RequestDirection>(reader.GetString(reader.GetOrdinal("Direction")))`
and `Insert()` adds `Direction` to the column list with `$direction = request.Direction.ToString()` —
identical to how `Status`/`Destination` are already stored. `GetAll()` stays `SELECT *` with no
filtering (this project filters in C#, never in SQL). The `RemoveSourceMessageIdUniqueConstraintIfPresent`
rebuild is *not* touched: it only runs on pre-multi-contributor databases, which predate this column,
and `EnsureColumnExists` runs before the rebuild check would matter — keep the ordering as-is
(EnsureColumnExists calls first, rebuild last, same as today).
**Alternative rejected**: separate `OutboundRequest` table — would duplicate ~30 columns and the whole
dashboard rendering pipeline for a flow that is 90% the same paperwork.

### `Requested` reuses `ConfirmedAt` / `ConfirmedByUserId` — no new audit column
New `RequestStatus.Requested` (appended last in the enum; stored as TEXT so ordinals don't matter).
Repository gains, mirroring `UpdateStatusToConfirmed`'s shape rather than `MarkUploaded`'s:

```csharp
void SetRequested(long id, DateTimeOffset requestedAt, long requestedByUserId);
// UPDATE PersonRequest SET Status='Requested', ConfirmedAt=$requestedAt, ConfirmedByUserId=$requestedByUserId
// WHERE Id = $id AND Status = 'Pending' AND Direction = 'Outbound'
```

Outbound has no Uploaded/Confirmed distinction: `Requested` *is* its terminal confirmed-equivalent
state, and a real email went out attributable to a user — the exact semantics `ConfirmedAt`/
`ConfirmedByUserId` already carry. **Alternative rejected**: `RequestedAt`/`RequestedByUserId` columns —
two more always-NULL-for-Inbound columns plus a second rendering branch, for zero added information.
The `Direction = 'Outbound'` guard in the WHERE clause makes the transition unreachable for Inbound rows.

### Outbound dedupe: filter in C#, no new repository query
Filtering the *result* of the existing `FindByRutAndComuna` / `FindByFullNameAndComuna` in C# is NOT
enough: they return only the newest match (`ORDER BY Id DESC LIMIT 1`), so a newer Inbound row for the
same RUT+comuna would hide an existing Outbound row and let a duplicate through. So add **two
direction-scoped overloads** — `FindByRutAndComuna(string rut, string comuna, RequestDirection direction)`
and the FullName twin — whose SQL is the existing query plus `AND Direction = $direction`. The existing
2-arg overloads stay byte-identical and direction-agnostic, so the Inbound path is provably unchanged;
only `ProcessOutboundRequest` calls the 3-arg ones, passing `Outbound`.
**Alternative rejected**: making the 2-arg overloads direction-aware — silently changes Inbound dedupe.

### Every inbound-only consumer gets an explicit `Direction == Inbound` filter
Outbound rows are inserted with `Destination = None` and `TransferredAt = null`, which is exactly
what Casos (Index) shows — so without this they would appear on the Casos screen. Audit of
`repository.GetAll()` call sites (8 in `src/`):

| Call site | Change |
|---|---|
| `Index.cshtml.cs:366` | add `.Where(c => c.Direction == RequestDirection.Inbound)` — **required, prevents leak** |
| `Sector.cshtml.cs:23,59` | add Inbound filter |
| `SectorF8.cshtml.cs:24,51` | add Inbound filter (belt-and-braces: Outbound is never `Marked`) |
| `Certificado.cshtml.cs:120,169` | add Inbound filter |
| `Discarded.cshtml.cs:14,27` | check at implementation time; add Inbound filter if it surfaces PersonRequest rows |
| `F8.cshtml.cs:176` | splits by tab: Inbound (Destination==F8) vs Outbound |
| `RouterWorker.cs:77` | `reportWriter.Write(repository.GetAll().Where(...).ToList(), ...)` |

### CSV exclusion happens at the RouterWorker call site, not inside `CsvReportWriter`
`CsvReportWriter.Write` takes whatever list it is given and has no filtering responsibility today
(it does not even know about `Destination`). Keep it Direction-unaware; filter in
`RouterWorker.RunCycleAsync`. **Alternative rejected**: filtering inside `Write` — would make the
writer silently drop caller-supplied rows and break its existing tests' contract.

### `RequestStatus.Requested` blast radius
Implementation MUST grep `RequestStatus` across `src/` and `tests/` before finishing. Known sites, all
already safe: badge `switch` expressions in `Index.cshtml:254`, `F8.cshtml:126`, `Certificado.cshtml:132`
(each has a `_ => ("badge-pending", …)` fallback arm — the "Yo pido" tab renders its own badge, so these
stay untouched); `if/else if` chains on `Pending`/`Uploaded`/`Confirmed` in `Index.cshtml:276+`,
`F8.cshtml:157+` (fall through to no action for `Requested`, correct); `Index.cshtml.cs:383`
`Enum.TryParse` status filter (gains a value harmlessly); ordering `.OrderBy(c => c.Status == Confirmed)`
in `Index/F8/Certificado` (Requested sorts with the non-confirmed group — acceptable, Outbound has its own tab).
`CsvReportWriter:38` writes `Status.ToString()` — moot once Outbound is excluded.

### Tab switcher: two `<a>` links + query string, server-renders one table
No `.tab` CSS and no JS framework exists. `F8Model` gains `[BindProperty(SupportsGet = true)] public string Tab { get; set; } = "piden";`
`OnGet` loads `InboundCases` (Destination==F8, Inbound) and `OutboundCases` (Outbound) and the view
renders only the active one. Markup mirrors the existing `app-subnav` convention (`nav-f8 active` in
`F8.cshtml:29-34`):

```html
<div class="page-tabs">
  <a asp-page="/F8" asp-route-tab="piden" class="page-tab @(Model.Tab == "pido" ? "" : "active")">Me piden</a>
  <a asp-page="/F8" asp-route-tab="pido"  class="page-tab @(Model.Tab == "pido" ? "active" : "")">Yo pido</a>
</div>
```

New `.page-tabs` / `.page-tab` block in `dashboard.css` styled after the existing subnav rules.
Every existing `RedirectToPage()` in `F8Model` must become `RedirectToPage(new { tab = Tab })` so an
action doesn't bounce the operator back to the other tab. **Alternative rejected**: JS toggle — breaks
the POST-redirect-GET flow every existing action uses.

### RouterWorker third loop mirrors the first
Same `GetMessagesInFolderAsync` → `foreach` → per-email `try/catch` + `LogError(…"se continúa con el
resto del lote"…)` shape as the `SourceFolderName` loop, calling `routingService.ProcessOutboundRequest(email, contacts)`.
Extend the completion log line with a third count/folder pair. New `RouterOptions.OutboundSourceFolderName`
default `"CARP. PARA PEDIR OTRAS COMUNAS"`, documented in `appsettings.Example.json` (`bin/` copies are
build artifacts — do not edit).

### New email template
```csharp
public static (string Subject, string Body) RequestFolderToComuna(string fullName, string rut)
```
Same `(Subject, Body)` tuple-returning static factory pattern as `UploadConfirmation`. Subject:
`"Solicitud de carpeta – {fullName}, RUT {rut}"`. Body: formal Spanish municipal tone with
"Junto con saludar," opening and "Saluda atentamente, / Municipalidad de Valparaíso" sign-off,
requesting that the comuna upload that contributor's carpeta to the **SGL** platform (never Conaset —
Conaset is the inbound-only wording). Sent via `mailSender.SendAsync(comunaContact.ContactEmail, …,
AppendFooter(body, userId), ct)`, exactly like `SendConfirmationAsync`.

## Data Flow

    "CARP. PARA PEDIR OTRAS COMUNAS" ──→ RouterWorker loop #3
              │                              │
              └─→ ProcessOutboundRequest(email, contacts)
                     dedupe SourceMessageId / own-domain skip / ResolveByDomain
                     PersonDataExtractor body+subject → Insert(Direction=Outbound, Status=Pending)
                                                              │
    F8 ?tab=pido ──→ "Enviar solicitud" ──→ SendOutboundRequestAsync(id, userId, contacts, ct)
                                                 │
                          IMailSender ──→ comuna ContactEmail    SetRequested(id, now, userId)

## File Changes

| File | Action | Change |
|---|---|---|
| `Domain/PersonRequest.cs` | Modify | `RequestDirection` enum; `Direction` property (default Inbound); `RequestStatus.Requested` appended |
| `Persistence/PersonRequestRepository.cs` | Modify | `EnsureColumnExists` Direction; Insert/Map; `SetRequested`; `RevertRequestedToPending`; 3-arg direction-scoped Find overloads (interface + impl) |
| `Routing/AddressChangeRoutingService.cs` | Modify | `ProcessOutboundRequest(email, contacts)`; `SendOutboundRequestAsync(id, userId, contacts, ct)`; `RectifyOutboundRequestAsync(id, userId, contacts, ct)` — all returning `ConfirmationResult` |
| `RouterWorker.cs` | Modify | Third polling loop; Inbound filter on the CSV call; extended log line |
| `Configuration/RouterOptions.cs` | Modify | `OutboundSourceFolderName` |
| `appsettings.Example.json` | Modify | Document new Router key |
| `Notifications/EmailTemplates.cs` | Modify | `RequestFolderToComuna(fullName, rut)`; `OutboundRequestRectification(fullName, rut)` |
| `Dashboard/Pages/F8.cshtml.cs` | Modify | `Tab` bound property; `InboundCases`/`OutboundCases`; `OnPostSendOutboundRequestAsync`; `OnPostRectifyOutboundRequestAsync`; tab-preserving redirects |
| `Dashboard/Pages/F8.cshtml` | Modify | Tab links; second table (Nombre/RUT/Comuna/Recibido/Estado/Acción) with "Enviar solicitud" (Pending rows) and "Rectificar solicitud" (Requested rows) buttons |
| `Dashboard/Pages/Index.cshtml.cs`, `Sector.cshtml.cs`, `SectorF8.cshtml.cs`, `Certificado.cshtml.cs`, `Discarded.cshtml.cs` | Modify | Inbound-only filter |
| `wwwroot/css/dashboard.css` | Modify | `.page-tabs` / `.page-tab` block |
| Tests: `PersonRequestRepositoryTests`, `AddressChangeRoutingServiceTests`, `RouterWorkerTests`, `F8ModelTests`, `IndexModelTests`, `CsvReportWriterTests` | Modify | Direction round-trip + legacy-row default; outbound insert/dedupe-scoping; third loop; tab split; rectify-outbound round trip; Outbound excluded from Casos/CSV |

## Testing Strategy

| Layer | What | How |
|---|---|---|
| Unit | Direction persists; a row inserted before the migration maps as Inbound | `PersonRequestRepositoryTests` — insert via raw SQL without Direction, re-run `EnsureSchema`, assert `Inbound` |
| Unit | `SetRequested` only moves Pending+Outbound rows | assert an Inbound Pending row is untouched |
| Unit | Outbound dedupe is direction-scoped | seed an Inbound row for the same RUT+comuna, assert an Outbound row is still created |
| Integration | Third loop inserts Outbound Pending; failures in one email don't abort the batch | `RouterWorkerTests` with a fake reader |
| Integration | `SendOutboundRequestAsync` sends one SGL email and transitions to Requested | fake `IMailSender`, assert recipient/subject/status |
| Regression | Outbound rows never appear in Casos, Sector, SectorF8, Certificado or the CSV | `IndexModelTests` / `CsvReportWriterTests` |

## Migration / Rollout

Additive only. `EnsureColumnExists` backfills every existing row to `'Inbound'` via the column DEFAULT,
so the production database keeps behaving identically. Rollback = revert the code: the `Direction`
column and any Outbound rows become inert (the old code's `SELECT *` ignores unknown columns), though
Outbound rows would then surface in Casos until deleted — acceptable for a same-day revert.

## Addendum: Rectify a mistaken "Enviar solicitud" (resolved after design review)

Resolves the first Open Question below: **rectify, not delete**, mirroring exactly how Inbound already
handles "operator confirmed by mistake" — no new pattern, no undo-by-deletion.

### `RectifyOutboundRequestAsync` mirrors `RectifyConfirmationAsync`
New method on `AddressChangeRoutingService`:

```csharp
public async Task<ConfirmationResult> RectifyOutboundRequestAsync(long requestId, long rectifiedByUserId, IReadOnlyList<ComunaContact> contacts, CancellationToken cancellationToken)
```

Same shape as `RectifyConfirmationAsync` (lines 313-343 today): `FindById` → guard
`request.Status != RequestStatus.Requested` → guard incomplete Rut/FullName/Comuna → resolve
`comunaContact` by `request.Comuna` → send `EmailTemplates.OutboundRequestRectification(fullName, rut)`
via `mailSender.SendAsync` with `AppendFooter` → call the new repository revert method → return
`ConfirmationResult`. No `Direction` guard needed inside the method body itself: `Requested` is a
status value that only ever exists on Outbound rows (see design body above), so
`Status == Requested` already implies Outbound.

### New repository method, not a reuse of `RevertConfirmedToPending`
`RevertConfirmedToPending` targets `WHERE Status = 'Confirmed'` — wrong status, cannot be reused
as-is, and widening it to accept a status parameter would let it silently rewrite Inbound's
Confirmed→Pending contract. Add a sibling, `RevertRequestedToPending`, same shape:

```csharp
void RevertRequestedToPending(long id);
// UPDATE PersonRequest SET Status='Pending', ConfirmedAt=NULL, ConfirmedByUserId=NULL
// WHERE Id = $id AND Status = 'Requested' AND Direction = 'Outbound'
```

The `Direction = 'Outbound'` guard is defense-in-depth (matching the `SetRequested` guard's
reasoning) even though `Requested` cannot occur on an Inbound row today. No `UploadedAt` in the SET
list — Outbound rows never set that column, unlike the Inbound Confirmed→Pending revert.

### New email template
```csharp
public static (string Subject, string Body) OutboundRequestRectification(string fullName, string rut)
```
Same tuple-factory pattern as `ConfirmationRectification`, formal tone, explicitly retracting the
earlier SGL upload request (not a Conaset upload — Outbound never mentions Conaset). Subject:
`"Rectificación – Solicitud de carpeta – {fullName}, RUT {rut}"`. Body opens "Junto con saludar,",
states the folder request sent earlier for `{fullName}, RUT {rut}` was sent in error and should be
disregarded — no carpeta upload to SGL is being requested at this time — apologizes for the
inconvenience, closes "Saluda atentamente, / Municipalidad de Valparaíso". Written directly by the
implementer following this existing tone; no further sign-off needed (per user decision).

### F8 UI: "Rectificar solicitud" button, Requested rows only
`F8Model` gains:
```csharp
public async Task<IActionResult> OnPostRectifyOutboundRequestAsync(long id)
```
Identical shape to `OnPostRectifyConfirmationAsync` (resolve `userId`, `LoadDirectory()`, call the
new service method, set `Message`/`MessageIsError`, `Load()`, `return Page()`). No `RedirectToPage`
involved, so no tab-preservation change is needed for this handler specifically — but `Load()` must
populate both `InboundCases` and `OutboundCases` and the view must keep rendering whichever tab the
posted-back form's URL still carries (same as every other same-page POST handler on this page).
`F8.cshtml`'s Outbound table gets a second per-row action, alongside "Enviar solicitud", visible only
when `Status == Requested`: a form posting to `OnPostRectifyOutboundRequestAsync` with the row's `id`.
No delete action exists for Outbound rows — rectify is the only correction path.

## Open Questions

- [x] Should an Outbound row be deletable / revertible from `Requested` back to `Pending`? **Resolved**:
  revertible via rectify (see Addendum above), never via deletion — matches Inbound's existing pattern.
- [x] Exact final Spanish wording of `RequestFolderToComuna` (and `OutboundRequestRectification`) —
  **resolved**: written directly by the implementer, following the existing formal municipal tone
  already established in `EmailTemplates.cs`. No operator sign-off gate before implementation.
