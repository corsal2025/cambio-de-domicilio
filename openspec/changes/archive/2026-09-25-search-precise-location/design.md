## Decisions

1. **Position = 1-based index in the same ordered list the Caja page renders** (`GetCasesByBoxId` / `GetCajaQueue`, both ordered by TransferredAt), so the banner's N° always matches the printed N°.
2. **Link by box Id, not code**: codes can repeat (operator decision), Ids can't. The Caja page takes `?highlightId=`, adds `row-highlight-target` to the row, and calls `scrollIntoView({block:'center'})`.
