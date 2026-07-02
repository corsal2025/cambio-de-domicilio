# Design: Folder-Based Triggering

## Decisions

### Source folder resolved by display name, not a distinguished ID
EWS only exposes well-known folders (`inbox`, `sentitems`, etc.) as `DistinguishedFolderId`. Custom folders like "Para pedir" require `FindFolder` under `msgfolderroot` with a `DisplayName` restriction, `Traversal="Deep"` (the folder may be nested). The resolved `FolderId`+`ChangeKey` is cached in `EwsEmailReader` after first successful resolution — folder identity is stable across a run; re-resolving every 30 minutes for a folder that doesn't move is unnecessary EWS traffic.

### No time-window filter — full folder listing, dedup by InternetMessageId
The trigger event is now "operator moved this item into the folder," not "this item arrived recently." Those are different timestamps in Exchange (`DateTimeReceived` never changes on move), so time-filtering the wrong field would silently drop manually-triaged old mail. Since idempotency is already keyed on `InternetMessageId` (immutable, from the `add-address-change-routing` design), the simplest correct approach is: list everything currently in the folder each cycle (capped at `MaxEntriesReturned`, default 200 — high enough for realistic daily volume, bounded to avoid unbounded response size), and let the existing "already processed?" check in `AddressChangeRoutingService` skip anything already sent/pending. No new state needed.

### "Carpetas subidas a Conaset" is out of scope, not merely unread
Explicitly not resolved, not listed, not written to by this change. It is the operator's manual archive of completed cases. A future change could move processed items there automatically, but that is not requested now and would need its own review (e.g. does "moved to Conaset" mean "close and stop tracking," and who decides that — the classification work in `add-web-dashboard` is a more natural home for that decision).

## Risks / Trade-offs

- **Folder can accumulate old already-processed items**: since nothing auto-removes items from "Para pedir" after sending, the same folder is re-listed in full every cycle indefinitely. Cheap per item (an `EnsureSuccess`/ID check against SQLite), but if the folder grows into the thousands, `MaxEntriesReturned` capping could start silently dropping unprocessed new items behind old ones. Mitigated by sorting `Ascending` on `DateTimeReceived` so the oldest (most likely already-processed) come first and get skipped fast; if this becomes a real problem, the fix is the operator (or a future dashboard action) moving completed items out, not a code change.
- **Folder rename breaks resolution silently until next successful cycle logs a warning**: if "Para pedir" is renamed, `FindFolder` returns no match; the cycle logs a warning and skips reading (does not crash), but new arrivals go unprocessed until the name is fixed in config or the folder is renamed back.
