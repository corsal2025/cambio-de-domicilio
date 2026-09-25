## 0. Setup

- [x] 0.1 Branch `fix/casos-post-state` from master

## 1. Repository: CloseWithoutFolder clears F8 flag (TDD)

- [x] 1.1 RED: extend `CloseWithoutFolder_F8Case_ClosesAndReturnsToCasos` to assert `FolderNotFound == false`
- [x] 1.2 GREEN: set `FolderNotFound = 0` in `CloseWithoutFolder`

## 2. Casos list state across POST (TDD)

- [x] 2.1 RED: test that the filter properties are bound on POST (`BindProperty(SupportsGet = true)`) and that `Message` is `[TempData]`
- [x] 2.2 RED: test that `OnPostSubirACaja` redirects with the current search/status route values
- [x] 2.3 GREEN: attributes, plus a script that injects the list state into POST forms

## 3. Verification (MANDATORY - AGENT MUST EXECUTE)

- [x] 3.1 Full `dotnet test`
- [x] 3.2 Browser check in the live app (read-only): search is kept after a harmless POST (toggle Marcar on/off on one row, then restore it)
- [x] 3.3 Deploy with DB backup; commit, push, PR, merge
