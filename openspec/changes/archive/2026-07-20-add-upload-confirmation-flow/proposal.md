# Proposal: Upload Confirmation Flow (supersedes outbound folder-request)

## Why

The operational workflow changed from what `add-address-change-routing` assumed. It is not Valparaíso *requesting* folders from other comunas — it is other comunas *requesting* folders from Valparaíso. The operator manually finds each contributor's file, scans it, and uploads it to Conaset's system; that physical/manual work cannot be automated. What can and should be automated is the paperwork around it: extracting who was requested and by which comuna, and — once the operator has uploaded a file — notifying that comuna it's done, without the operator having to draft that email by hand.

## What Changes

- **Remove** the automatic outbound "please send the last folder" email (`EmailTemplates.FolderRequest`, the `Sent`/reply-matching lifecycle). It no longer reflects what happens: nobody needs to be asked, the request already arrived by mail.
- **Source folder renamed/corrected**: reads `"CARP. PARA PEDIR"` (previously configured as `"Para pedir"` — exact name confirmed from the operator's Outlook).
- For every new email in that folder from a known comuna domain: extract `full_name`, `rut`, and the **requesting comuna** (same domain-based detection as before), and record it as `Pending` — no email sent at this point.
- **New signal**: the operator's own existing habit of moving a completed case's email into `"CARP. YA SUBIDAS"` (their archive folder) is read as the "this was uploaded" signal — no new UI, no command the operator has to remember, just their existing workflow.
- When an email that matches a `Pending` record (by `InternetMessageId` — it's the same email, only moved) appears in `"CARP. YA SUBIDAS"`, the system sends a standard confirmation email to that comuna's contact address ("ya se subió la carpeta") and marks the record `Confirmed`.
- Simplify the data model accordingly: two states (`Pending` → `Confirmed`), dropping the request/response fields that modeled the old outbound-then-reply lifecycle.
- The CSV report and dual notification channels (toast + email to the operator) are kept, repurposed to reflect confirmation events instead of reply events.

## Impact

- Affected capability: `routing` (replaces the "send request" and "detect reply" requirements with "detect incoming request" and "detect upload confirmation")
- Files: `Domain/PersonRequest.cs`, `Persistence/PersonRequestRepository.cs`, `Routing/AddressChangeRoutingService.cs`, `Notifications/EmailTemplates.cs`, `Reporting/CsvReportWriter.cs`, `Mail/IEmailReader.cs`, `Ews/EwsEmailReader.cs`, `RouterWorker.cs`, `Configuration/RouterOptions.cs`
- No new external dependency
- This is a behavioral rewrite of the archived `add-address-change-routing` design, not an additive change — documented here rather than silently editing the archive, since the "why" fundamentally changed (direction of the request flow was backwards in the original design)
