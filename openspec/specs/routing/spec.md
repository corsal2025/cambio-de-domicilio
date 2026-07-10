# routing Specification

## Purpose

Tracks folder requests other comunas make to Valparaíso for a contributor's "última carpeta"
(most recent driver's-license folder), from the moment the request email is manually triaged into
"CARP. PARA PEDIR" through to the confirmation email sent back to the requesting comuna. This spec
supersedes the original `add-address-change-routing` design (archived): that design assumed
Valparaíso *initiates* the request; the real business process is the reverse — other comunas
request folders *from* Valparaíso, and Valparaíso only ever replies to confirm an upload, never
sends an outbound request of its own.

## Requirements

### Requirement: Detect incoming requests by folder membership, not by scanning the inbox
The system SHALL treat "CARP. PARA PEDIR" as the sole trigger for new requests. The operator
manually classifies incoming mail by moving it there; the system SHALL NOT scan the mailbox's
Inbox or apply any received-date filter, since the trigger is folder membership at the time of the
poll, not when the message originally arrived.

#### Scenario: Email sits in the source folder regardless of age
- **WHEN** the poller lists "CARP. PARA PEDIR"
- **THEN** every item currently in that folder is a candidate, with no time-window restriction

### Requirement: Resolve the requesting comuna from the sender's domain
The system SHALL resolve the requesting comuna by matching the sender's email domain against the
`ComunaContact` directory (CSV, editable from the dashboard), and SHALL ignore mail from the
organization's own domain (`munivalpo.cl`) as internal correspondence.

#### Scenario: Recognized comuna domain
- **WHEN** a new email arrives from `rfloresc@municatemu.cl` and `municatemu.cl` is a known domain
- **THEN** the system identifies it as a request from comuna `Catemu`

#### Scenario: Own domain is ignored
- **WHEN** a new email arrives from `alguien@munivalpo.cl`
- **THEN** the system does not track it and does not create a Discarded record for it

#### Scenario: Domain shared by more than one comuna (e.g. a generic gmail.com mailbox)
- **WHEN** the sender's domain matches more than one `ComunaContact` entry (several comunas using
  the same free webmail provider)
- **THEN** the system resolves the comuna only if the sender's exact address matches one
  registered `ContactEmail` for that domain; otherwise it does not guess and discards the email for
  manual review, since attributing it to the wrong comuna would misroute an official confirmation

### Requirement: Extract every contributor named in the request
The system SHALL extract full name and RUT for every contributor found in the request — a single
email MAY list more than one person, each becoming its own tracked case sharing the same source
email identity.

#### Scenario: Single contributor, RUT anchored in free-text prose
- **WHEN** the body contains `GUSTAVO ANDRÉS PEÑA CASTRO` adjacent to a valid `RUT: 18.785.387-7`
- **THEN** the system extracts `full_name = "GUSTAVO ANDRÉS PEÑA CASTRO"` and the RUT in canonical
  dotted form, and creates one case

#### Scenario: Multiple contributors listed in the same email
- **WHEN** the body lists two "NOMBRE RUT" lines for two different people
- **THEN** the system creates one case per person, each independently subject to the duplicate-check
  and needs-review rules below — one contributor's data is never mixed into another's

#### Scenario: No RUT anchor found anywhere (body or subject)
- **WHEN** neither the body nor the subject contains a validatable RUT
- **THEN** exactly one case is created with `needs_review = true` and no name/RUT, so the request is
  not silently lost

### Requirement: Fall back to the subject line, but never trust a name found there
Some comunas put the request itself in the subject (e.g. a forwarded email with no usable body).
The system SHALL also search the subject for a RUT when the body yields none, merging by RUT with
whatever the body already found. A name found in the subject SHALL NOT be trusted automatically,
regardless of whether it looks correct — the case is always flagged for manual confirmation.

#### Scenario: Request entirely in the subject
- **WHEN** the body has no valid RUT and the subject reads
  `RV: SOLICITUD ANTECEDENTES <NOMBRE> RUN <RUT>`
- **THEN** the system creates a case using the subject's RUT, `full_name = null`, and
  `needs_review = true`

#### Scenario: Body and subject name different contributors
- **WHEN** the body names one contributor and the subject (a forwarded original) names a different
  one, each with its own valid RUT
- **THEN** both contributors are tracked as separate cases

#### Scenario: Unrelated boilerplate in the subject must never become a "name"
- **WHEN** the subject reads `Fwd: SUBIR CARPETA PLATAFORMA CONASET 14.148.466-4` (an instruction
  phrase with no person name at all, next to a valid RUT)
- **THEN** the system creates a case with the RUT and `full_name = null`, `needs_review = true` —
  it never mistakes the instruction phrase for a name (this exact failure happened once in
  production before the "never trust a subject name" rule was added)

### Requirement: Normalize name word order for known fixed-order sources
The system SHALL reorder a name to given-names-first only when it was extracted via a source whose
word order is deterministically known (Viña del Mar's automated export, which always emits
APELLIDO APELLIDO NOMBRE NOMBRE). It SHALL NOT reorder names found via free-text patterns, since
those are already given-names-first by natural-language convention and guessing would corrupt
already-correct names.

#### Scenario: Viña del Mar's fixed-order export
- **WHEN** a name is found via the space-separated-check-digit pattern and has exactly four words
- **THEN** the first two words (surnames) and last two words (given names) are swapped so the
  stored name reads given-names-first

#### Scenario: Free-text prose is left as-is
- **WHEN** a name is found via a prefixed-RUT or bare-RUT pattern in ordinary sentence text
- **THEN** the extracted word order is preserved unchanged, even if it happens to be four words

### Requirement: Prevent duplicate tracking for the same person and comuna
The system SHALL NOT create a second case for a `(rut, comuna)` pair that is already tracked,
even when the new mention arrives via a different source email (a resend).

#### Scenario: Same person resent under a new message
- **WHEN** a new source email extracts a RUT+comuna pair that already has a tracked case
- **THEN** no second case is created

### Requirement: Idempotent processing per source message
The system SHALL NOT re-create cases for a source email already tracked, across polling cycles.
Because one email can yield several contributors, this is a per-case-existence check
(`SourceMessageId` is not a unique key), not a single-row constraint.

#### Scenario: Two consecutive polling cycles with no folder change
- **WHEN** the poller runs again with the same items still in "CARP. PARA PEDIR"
- **THEN** no duplicate cases are created for messages that already have at least one tracked case

### Requirement: Continuous polling
The system SHALL run as a long-lived background process that polls both folders on a configurable
interval (default: 30 minutes).

#### Scenario: Default interval
- **WHEN** no interval is configured
- **THEN** the system polls every 30 minutes

### Requirement: Detect uploads by folder membership, never send automatically
The system SHALL mark a Pending case as Uploaded when the operator moves its source email to
"CARP. YA SUBIDAS". Moving the email SHALL NOT by itself send any email to the comuna.

#### Scenario: Operator moves the email after uploading to Conaset
- **WHEN** a Pending case's source email is found in "CARP. YA SUBIDAS"
- **THEN** the case transitions to Uploaded and no email is sent

### Requirement: Send the confirmation only on an explicit operator action
The system SHALL send the confirmation email to the requesting comuna only when the operator
explicitly triggers it (either the two-step "Enviar confirmación" button on an Uploaded case, or the
one-click "Marcar subida" action described below), and SHALL record who confirmed and when.

#### Scenario: Explicit confirmation of an Uploaded case
- **WHEN** the operator presses "Enviar confirmación" on an Uploaded, review-complete case
- **THEN** the system sends the standard confirmation email to the comuna's contact address,
  transitions the case to Confirmed, and records `confirmed_by_user_id` and `confirmed_at`

#### Scenario: Cannot confirm an incomplete or wrong-state case
- **WHEN** the operator attempts to confirm a case that is Pending, already Confirmed, or still
  `needs_review`
- **THEN** the system refuses and returns the reason, even if the requesting page was stale

### Requirement: One-click "Marcar subida" collapses upload + move + confirmation
The system SHALL offer a one-click action, for a Pending review-complete case, that (1) locates the
source email in "CARP. PARA PEDIR" by its Internet Message-Id, (2) moves it to
"CARP. YA SUBIDAS" and marks it unread there, (3) transitions the case to Uploaded, and
(4) immediately sends the confirmation email — an explicit, deliberate operator action (not a
background/automatic one), collapsing what would otherwise be a manual Outlook drag plus a separate
button click.

#### Scenario: Source email still present and movable
- **WHEN** the operator triggers "Marcar subida" on a Pending, review-complete case
- **THEN** the email is moved and marked unread, the case becomes Uploaded then Confirmed in one
  step, and the confirmation email is sent

#### Scenario: Source email already moved or missing
- **WHEN** the source email can't be found in "CARP. PARA PEDIR" (e.g. already moved manually, or a
  transient mailbox issue)
- **THEN** the case still transitions to Uploaded and Confirmed — a mailbox-side inconsistency must
  not block a case the operator explicitly asked to advance

### Requirement: Stale Discarded records are cleaned up once resolved
When a previously-discarded email later resolves to a known comuna (the directory was updated, or
an exact address was registered for a shared domain), the system SHALL remove its stale Discarded
record so it doesn't linger next to the now-tracked case.

#### Scenario: Comuna added to the directory after an email was discarded
- **WHEN** an email was discarded for an unrecognized domain, and that domain is later added to the
  directory
- **THEN** the next cycle creates the case and deletes the old Discarded record for that message

### Requirement: Continuously-refreshed tracking report
The system SHALL rewrite a CSV report to a fixed local path on every polling cycle, including a
`Requiere revisión` column for cases missing extractable data.

#### Scenario: Report refreshed each cycle
- **WHEN** a polling cycle completes
- **THEN** the CSV at the configured path is rewritten with one row per case: full name, RUT,
  comuna, status, última-carpeta date, sector, confirmation date, and `Requiere revisión`
