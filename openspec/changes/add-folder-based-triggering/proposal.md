# Proposal: Folder-Based Triggering

## Why

The operator does not want the router acting on every address-change-looking email in the Inbox automatically. Their actual workflow: they manually review incoming mail and **move** the ones that should trigger a folder request into a dedicated Outlook folder ("Para pedir"). A second folder ("Carpetas subidas a Conaset") holds already-completed/archived cases and must not be touched by the router. The current implementation reads the Inbox with a `DateTimeReceived`-based time filter, which is both the wrong source folder and silently breaks for manually-moved mail (`DateTimeReceived` does not change when an item is moved between folders — an item moved in today from three days ago falls outside the lookback window and is never seen).

## What Changes

- The router reads from a **configurable named folder** ("Para pedir") instead of the Inbox.
- Drop the `DateTimeReceived` time filter entirely: each cycle lists everything currently in the source folder (bounded by a max-entries cap) and relies on the existing `InternetMessageId`-based idempotency to skip already-processed items. This is correct for a "operator moves items in" trigger model, where an item's arrival in the folder — not its original receipt time — is what matters.
- The "Carpetas subidas a Conaset" folder is explicitly **not** read or modified by this change — it is out of scope, documented as the operator's own archive of completed cases.
- Folder resolution by display name via EWS `FindFolder` (folders are not exposed as EWS distinguished IDs).

## Impact

- Affected capability: `routing` (modifies email-reading behavior)
- Files: `Ews/EwsMessages.cs`, `Ews/EwsResponseParser.cs`, `Ews/EwsEmailReader.cs`, `Configuration/RouterOptions.cs`, `RouterWorker.cs`
- No new external dependency; still raw SOAP over `HttpClient`
- Breaking behavior change from the archived `add-address-change-routing` design: source is no longer the Inbox, and there is no more time-window filtering — documented here as a deliberate revision, not a regression
