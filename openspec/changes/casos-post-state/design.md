## Context

Index POST handlers already redirect with `search = SearchQuery` etc., but those properties are only set in `OnGet`; on POST they are null because forms don't send them and they aren't bound. `Message` is a plain property, so it is lost on redirect.

## Decisions

1. **Bind list state on POST**: `[BindProperty(SupportsGet = true, Name = "...")]` on StatusFilter/OnlyNeedsReview/OnlyBounced/SearchQuery, plus a small script that injects hidden `status/needsReview/bounced/search` inputs (from `location.search`) into every POST form on submit. Alternative rejected: editing all 18 forms by hand — error-prone, and new forms would silently regress.
2. **`[TempData]` for Message/MessageIsError**: standard PRG pattern; same-request `return Page()` flows still render it.
3. **Clear FolderNotFound in CloseWithoutFolder**: a closed case is no longer an F8 candidate.

## Risks / Trade-offs

- [Hidden inputs duplicate an existing field, e.g. MarkAllVisible already posts status/search] → the script skips names already present in the form.

## Open Questions

None.
