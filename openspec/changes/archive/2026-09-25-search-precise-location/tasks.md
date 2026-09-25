## 1. Index: precise locations (TDD)

- [x] 1.1 RED: a case in the second of two `A1-CD` listings reports that BoxId, its close date and position 2
- [x] 1.2 RED: a case in the queue reports "cola de Caja" and its queue position
- [x] 1.3 GREEN: `CajaMatches` (record CajaLocation), and the banner lists every location with a link

## 2. Caja: highlight target row (TDD)

- [x] 2.1 RED: `CajaModel.OnGet(boxId, highlightId)` exposes `HighlightId`
- [x] 2.2 GREEN: row anchors, highlight class, scroll script (listing and queue)

## 3. Verification (MANDATORY - AGENT MUST EXECUTE)

- [x] 3.1 Full `dotnet test`
- [x] 3.2 Live check (read-only): search a boxed RUT, open the link, confirm the highlighted row and N°
- [x] 3.3 Deploy with DB backup; PR; merge
