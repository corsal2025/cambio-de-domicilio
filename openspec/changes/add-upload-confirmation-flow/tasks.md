# Tasks: Upload Confirmation Flow

Revised 2026-07-03 after walking the full business process with the operator (see `docs/flujo-proceso.md`): confirmation is now **button-driven**, never automatic; grace period dropped (the button replaces it); added manually-entered última-carpeta date and derived sector (Archivo / Oficina 43).

## 1. Data model
- [x] 1.1 `RequestStatus` = `Pending` → `Uploaded` → `Confirmed`; `PersonRequest` gains `FechaUltimaCarpeta` (manual), `UploadedAt`, `ConfirmedAt`, computed `Sector` (before 2023-07-01 → Archivo, else Oficina 43)
- [x] 1.2 Repository: `MarkUploaded` (only from Pending), `SetFechaUltimaCarpeta`, `UpdateStatusToConfirmed`, `FindById`; dedupe lookup `FindByRutAndComuna` matches any existing record
- [x] 1.3 Repository unit tests including sector derivation on both sides of the July-2023 boundary

## 2. Folder-based reading, generalized to two folders
- [x] 2.1 `IEmailReader.GetMessagesInFolderAsync(folderDisplayName)`; per-folder `EwsFolderRef` cache
- [x] 2.2 `RouterOptions.SourceFolderName = "CARP. PARA PEDIR"`, `ConfirmationFolderName = "CARP. YA PEDIDAS"` (exact names from the operator's Outlook)
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
- [ ] 5.3 README refresh (pending — will do together with the dashboard change, which changes usage substantially)

## 6. Pending / next change (dashboard)
- [ ] 6.1 UI: editable fecha-última-carpeta per case, "Enviar confirmación" button wired to `SendConfirmationAsync`, PDF generation per sector — belongs to `add-web-dashboard`
- [ ] 6.2 Live end-to-end verification against the real mailbox
