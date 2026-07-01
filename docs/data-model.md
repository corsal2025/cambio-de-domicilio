# Data Model Documentation

This document describes the data model for **OutlookComunaRouter**, a batch job that reads "cambio de domicilio" (address change) notifications in an Outlook mailbox, requests the latest case folder ("última carpeta") for each person from the relevant comuna (municipality), and tracks the request/response lifecycle.

Storage: SQLite, single file, no ORM.

## Model Descriptions

### 1. PersonRequest

Represents one detected address-change notification for one person, and the lifecycle of the folder request sent to their comuna.

**Fields:**
- `id`: Primary key (integer, autoincrement)
- `full_name`: Full name as extracted from the source email (e.g. `GUSTAVO ANDRÉS PEÑA CASTRO`)
- `rut`: Chilean RUT as extracted (e.g. `18.785.387-7`)
- `comuna`: Comuna name, derived from the sender domain (e.g. `Catemu` from `municatemu.cl`)
- `source_message_id`: Graph message ID of the original address-change notification email
- `source_conversation_id`: Graph conversation ID of the original email
- `status`: One of `pending`, `sent`, `responded` (see lifecycle below)
- `request_sent_at`: Timestamp when the folder-request email was sent to the comuna (nullable until sent)
- `request_message_id`: Graph message ID of the outgoing request email (nullable until sent)
- `response_received_at`: Timestamp when a matching reply from the comuna was detected (nullable until responded)
- `response_message_id`: Graph message ID of the comuna's reply (nullable until responded)
- `last_folder_date`: "Fecha de última carpeta" reported by the comuna in their reply, once parsed (nullable until responded)
- `created_at`: When this record was first detected

**Validation Rules:**
- `full_name` and `rut` are required to move a record from `pending` to `sent` — if either can't be extracted from the source email, the record stays `pending` and is flagged for manual review (not sent automatically).
- `comuna` must resolve to a known entry in `ComunaContact`; if it doesn't, the record stays `pending` (unknown comuna, no destination to send to).
- A given `source_message_id` is only ever processed once (idempotency key), so re-running the daily job does not duplicate outgoing requests.

**Status Lifecycle:**
```
pending -> sent -> responded
```
- `pending`: notification detected, not yet sent (missing data, unknown comuna, or not processed yet)
- `sent`: folder request emailed to the comuna's contact address
- `responded`: a reply matching this request (same thread, or new email from a `muni<comuna>.cl` domain referencing the same RUT/name) was detected

### 2. ComunaContact

Reference directory mapping a comuna to its municipal contact email, imported from a CSV/Excel file provided by the user.

**Fields:**
- `comuna`: Comuna name (unique, normalized: uppercase, no accents, for matching)
- `contact_email`: Municipal contact address for that comuna (e.g. `rfloresc@municatemu.cl`)
- `domain`: Email domain associated with the comuna (e.g. `municatemu.cl`), used to recognize both outgoing sends and incoming replies
- `imported_at`: When this row was last (re)imported

**Validation Rules:**
- `domain` must never equal the organization's own domain (`munivalpo.cl`) — that domain identifies internally-generated mail, not a comuna response.

## Entity Relationship

```
ComunaContact (1) ----< (N) PersonRequest
   via comuna/domain match
```

## Identification Rules (business logic, not schema)

- **Address-change source detection**: an email is a candidate address-change notification if its sender domain matches `muni<comuna>.cl` and is **not** the organization's own domain (`munivalpo.cl`).
- **Reply matching**: a comuna's response is matched to a `PersonRequest` first by `source_conversation_id` / `In-Reply-To` (same thread), falling back to RUT match in the new email's body when it arrives as a new thread from a recognized comuna domain.
