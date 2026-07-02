# Tasks: Folder-Based Triggering

## 1. EWS folder resolution
- [x] 1.1 `EwsMessages.BuildFindFolderRequest(displayName)`: `FindFolder` under `msgfolderroot`, `Traversal=Deep`, `DisplayName` restriction
- [x] 1.2 `EwsResponseParser.ParseFindFolderResponse`: returns the first matching `FolderId`+`ChangeKey`, or null
- [x] 1.3 Unit tests: found, not found

## 2. Read from the resolved folder, drop the time filter
- [x] 2.1 `EwsMessages.BuildFindItemRequest`: accept a folder reference (`EwsFolderRef`: distinguished or resolved `FolderId`/`ChangeKey`) instead of hardcoding `inbox`; removed the `DateTimeReceived` restriction, kept `MaxEntriesReturned` cap (default 200) and ascending sort
- [x] 2.2 `EwsEmailReader`: resolves "Para pedir" once (cached in an instance field), passes its folder reference to `FindItem`; `IEmailReader.GetRecentMessagesAsync` dropped the `since` parameter
- [x] 2.3 `RouterWorker`: updated the call site (no more `since` computation); `Program.cs --smoke-test` mode updated too
- [x] 2.4 Unit tests: folder resolved once and cached across two calls (`EwsEmailReaderTests`); graceful empty-result no-op (no `FindItem` call) when resolution fails

## 3. Configuration
- [x] 3.1 `RouterOptions.SourceFolderName` (default `"Para pedir"`), reflected in `appsettings.json`/`appsettings.Example.json`
- [x] 3.2 README note: "Carpetas subidas a Conaset" is intentionally out of scope

## 4. Verification
- [x] 4.1 `dotnet build` + `dotnet test` green (44/44 passing)
- [ ] 4.2 Manual verification against the real mailbox: move an old email into "Para pedir", confirm it is picked up next cycle — pending, requires live access to the production mailbox
