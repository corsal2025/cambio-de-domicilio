## 1. Old folders to Caja (TDD)

- [x] 1.1 RED: `OnPostAddManualCases(..., directToCaja: true)` puts the rows in the Caja queue in order, not in Casos, with no email sent
- [x] 1.2 RED: without the flag the case stays Pendiente in Casos
- [x] 1.3 GREEN: handler flag plus the form checkbox and its message

## 2. Housekeeping

- [x] 2.1 `docs/presentacion/build_deck.py`: repo-relative path; README link to the standalone presentation

## 3. Verification (MANDATORY - AGENT MUST EXECUTE)

- [x] 3.1 `dotnet test`
- [x] 3.2 Dev instance: enter an old folder with the option; it appears in the Caja queue
- [x] 3.3 Deploy with backup; PR; merge
