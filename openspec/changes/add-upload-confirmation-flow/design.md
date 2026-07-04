# Design: Upload Confirmation Flow

## Decisions

### Confirmation trigger: the moved email itself, matched by InternetMessageId
The operator already moves completed cases into "CARP. YA SUBIDAS" as part of their existing routine — that observation (from requirements gathering) is the whole design: no new command, no dashboard action needed for v1. Because `InternetMessageId` is immutable across a move (unlike EWS `ItemId`, already a documented decision from the archived change), the item that shows up in the confirmation folder can be matched directly to its `Pending` record by the same ID it had when it was read out of "CARP. PARA PEDIR". No fuzzy matching, no RUT-fallback heuristics needed here — this is exact-match by design, simpler than the reply-matching problem the old design had to solve.

### Two folders read per cycle, not one
`IEmailReader` is generalized from a single hardcoded source folder to `GetMessagesInFolderAsync(string folderDisplayName, CancellationToken)`, with per-folder-name caching of the resolved `EwsFolderRef` (a `Dictionary<string, EwsFolderRef>` instead of a single field). `RouterWorker` calls it twice per cycle: once for the source folder (new pending cases) and once for the confirmation folder (uploads to acknowledge). Both listings are unfiltered by time, same reasoning as `add-folder-based-triggering`: the trigger is folder membership, not receipt date.

### Data model: two states, not four
The old `RequestStatus` (`Pending → Sent → Responded`) modeled "we asked, they answered." The real lifecycle is "they asked, we uploaded": `Pending → Confirmed`. Dropped `RequestSentAt`/`RequestMessageId`/`ResponseReceivedAt`/`ResponseMessageId`/`LastFolderDate` — none of them describe anything that happens in this flow. Added `ConfirmedAt`. No live production data exists yet (the mailbox integration was only smoke-tested read-only), so this is a clean schema change, not a migration.

### Duplicate handling: any existing record for (rut, comuna) blocks re-insertion, not just "active" ones
The old dedup rule only blocked re-sending if a request was already `Sent`/`Responded`. Now there's nothing to "re-send" — a second email for the same person+comuna should just link to the existing `Pending`/`Confirmed` record rather than create a second tracked case, because the operator only needs to see it once regardless of how many comunas or resends reference the same contributor.

### Confirmation email: still a fixed template, still not hardcoded in source
Same pattern as the (now removed) request template: subject/body live in `Notifications/EmailTemplates.cs` and are mirrored in `docs/email-templates.md` for the operator to edit without touching code.

### Operator notification kept, repurposed
The dual-channel notification (Windows toast + email to the operator) still fires, but now on "confirmation email successfully sent" rather than "comuna replied" — this gives the operator positive feedback that the automated email actually went out after they did their manual upload step, which matters because sending is fire-and-forget from their point of view (they moved a file, not visibly triggered an email).

## Risks / Trade-offs

- **A rename/move within "CARP. PARA PEDIR" to a *different* folder that isn't the confirmation folder** (e.g. operator manually filing something elsewhere) does not confirm anything — by design; only the specific confirmation folder counts. Worth restating to the operator once live, since it's the one part of the flow that depends on them being consistent about where they move things.
- **If the same physical email is somehow duplicated into the confirmation folder twice** (rare, but Outlook copy/forward could do it), the second occurrence has an already-`Confirmed` record and is a no-op — no double confirmation email, since matching by `InternetMessageId` only fires the send on the `Pending → Confirmed` transition.
