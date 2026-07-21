# Tasks: Upload Confirmation Flow

Revised 2026-07-03 after walking the full business process with the operator (see `docs/flujo-proceso.md`): confirmation is now **button-driven**, never automatic; grace period dropped (the button replaces it); added manually-entered última-carpeta date and derived sector (Archivo / Oficina 43).

## 1. Data model
- [x] 1.1 `RequestStatus` = `Pending` → `Uploaded` → `Confirmed`; `PersonRequest` gains `FechaUltimaCarpeta` (manual), `UploadedAt`, `ConfirmedAt`, computed `Sector` (before 2023-07-01 → Archivo, else Oficina 43)
- [x] 1.2 Repository: `MarkUploaded` (only from Pending), `SetFechaUltimaCarpeta`, `UpdateStatusToConfirmed`, `FindById`; dedupe lookup `FindByRutAndComuna` matches any existing record
- [x] 1.3 Repository unit tests including sector derivation on both sides of the July-2023 boundary

## 2. Folder-based reading, generalized to two folders
- [x] 2.1 `IEmailReader.GetMessagesInFolderAsync(folderDisplayName)`; per-folder `EwsFolderRef` cache
- [x] 2.2 `RouterOptions.SourceFolderName = "CARP. PARA PEDIR"`, `ConfirmationFolderName = "CARP. YA SUBIDAS"` (exact names from the operator's Outlook)
- [x] 2.3 Unit tests: independent per-folder resolution and caching

## 3. Routing service
- [x] 3.1 `ProcessIncomingRequest`: extract name/RUT/comuna, dedupe by message id and (rut, comuna), insert `Pending`, never send
- [x] 3.2 `ProcessUploadedCase`: folder move marks `Uploaded` only — no email from the move itself
- [x] 3.3 `SendConfirmationAsync(id)`: operator-triggered send; refuses non-`Uploaded` or incomplete cases with a reason; notifies operator (toast + email) on success
- [x] 3.4 `EmailTemplates.UploadConfirmation` replaces the removed outbound folder-request template
- [x] 3.5 Unit tests: detection, dedup, upload marking, button send happy path, refuse-pending, no-double-send, unknown case

## 4. Reporting
- [x] 4.1 CSV columns: `full_name, rut, comuna, status, fecha_ultima_carpeta, sector, confirmed_at, Requiere revisión` — no duplicate person+comuna rows
- [x] 4.2 Unit tests for both sector values and confirmed rows

## 5. Docs
- [x] 5.1 `docs/flujo-proceso.md`: step-by-step business-process diagram agreed with the operator
- [x] 5.2 `docs/email-templates.md`: upload-confirmation + operator-notification templates
- [x] 5.3 README refresh (kept brief; the operator-facing UI itself remains scoped to add-web-dashboard)

## 6. Extraction hardening (added 2026-07-03 after live calibration against 38 real messages)
- [x] 6.1 Strip the Exchange "CORREO EXTERNO" banner before extraction (it was being matched as the person's name in every case)
- [x] 6.2 RUT patterns: prefixed (`RUT`/`R.U.T.`/`RUN`/`R.U.N.`), bare dotted (`12.345.678-9`), bare undotted (`12345678-9`) — in that priority order, all check-digit-validated
- [x] 6.3 Name from the capitalized-word window adjacent to the RUT match (before, falling back to after), honorifics stripped; no RUT → no name (needs_review)
- [x] 6.4 Unit tests for each real-world format found in the calibration (banner, prefix variants, bare forms, attachment-only bodies)
- [x] 6.5 Re-run verified live: 16/24 tracked cases fully extracted (vs 5/24 before); the remaining 8 have their data in attachments/empty forwards and stay in manual review by design

## 7. Pending / next change (dashboard)
- [x] 7.1 UI: editable fecha-última-carpeta per case, "Enviar confirmación" button wired to `SendConfirmationAsync`, PDF generation per sector — delivered under `add-web-dashboard` (archived alongside this change)
- [x] 7.2 Live end-to-end verification against the real mailbox — confirmed live in production (operator-verified, 2026-07-20)
