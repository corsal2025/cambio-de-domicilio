# extraction Specification

## Purpose

Extracts a contributor's full name and RUT (Chilean national ID) from a request email's body or
subject. Calibrated against 38 real production messages (2026-07-03) plus specific production
failures found afterward; the guiding principle throughout is: **never invent or guess a name or
RUT** — an unrecognized shape means the case is flagged for a human, not silently wrong data going
into official inter-municipal correspondence.

## Requirements

### Requirement: Anchor extraction to a validated RUT, never to name-shaped text alone
The system SHALL only extract a name when it can anchor to a RUT that passes the Chilean check-digit
algorithm. Matching "capitalized words" without a RUT anchor is NOT viable — it would capture
Exchange's "CORREO EXTERNO" banner text or forwarded-header sender names as if they were the
contributor.

#### Scenario: RUT present and valid
- **WHEN** the text contains a RUT matching one of the recognized shapes and it passes the check
  digit
- **THEN** the system searches for a name in the text adjacent to it

#### Scenario: No valid RUT anywhere
- **WHEN** no RUT in the text passes the check digit (including a wrong check digit on an otherwise
  RUT-shaped string)
- **THEN** the system returns no data for that email/subject — the case is flagged for review with no
  name (see the `routing` spec's needs-review rule)

### Requirement: Try RUT shapes in a fixed priority order
The system SHALL try, in this order, until one yields a validated match: (1) prefixed —
`RUT:` / `R.U.T.` / `RUN` / `R.U.N.`, dots and spacing optional; (2) bare with dots
(`12.345.678-9`); (3) bare without dots (`12345678-9`); (4) check digit separated only by
whitespace (`12345678        9`, Viña del Mar's system export format).

#### Scenario: Prefixed RUT takes priority when multiple shapes could match
- **WHEN** the text contains both a prefixed RUT and, elsewhere, an unrelated bare-shaped number
  sequence
- **THEN** the prefixed RUT is used

### Requirement: Strip Exchange's external-sender banner before extracting
The system SHALL remove Exchange's "CORREO EXTERNO" warning banner from the body before running any
extraction pattern, since the banner's own text can otherwise be captured as if it were the
contributor's name.

#### Scenario: Banner precedes the real request text
- **WHEN** the body starts with the "CORREO EXTERNO" warning followed by the actual request
- **THEN** the extracted name never contains banner text ("EXTERNO", the warning sentence, etc.)

### Requirement: Find the name in the text window adjacent to the RUT
The system SHALL search for a 2-to-5-word capitalized sequence within a bounded window of the RUT —
first immediately before it (the common case: name ends where the RUT begins), falling back to
immediately after it if nothing usable precedes it. Honorifics and connectors (`DON`, `DOÑA`, `SR`,
`SRA`, `SEÑOR`, `SEÑORA`, `DE`, `DEL`, `RUT`, `RUN`, `CI`, `CÉDULA`, `QUIEN`) SHALL be stripped from
the edges of the matched candidate.

#### Scenario: Name immediately precedes the RUT
- **WHEN** the text reads `... de GUSTAVO ANDRÉS PEÑA CASTRO RUT: 18.785.387-7 por cambio ...`
- **THEN** the extracted name is `GUSTAVO ANDRÉS PEÑA CASTRO` (honorific/connector-free, RUT
  excluded)

#### Scenario: Name follows the RUT instead
- **WHEN** the text reads `... con RUT: 18.785.387-7 GUSTAVO ANDRÉS PEÑA CASTRO.` (no usable name
  before the RUT)
- **THEN** the extracted name is found in the text after the RUT

#### Scenario: Honorific stripped from the edge
- **WHEN** the matched candidate is `Don Gustavo Peña`
- **THEN** the stored name is `Gustavo Peña`

### Requirement: Extract every contributor in a multi-person email without cross-contamination
The system SHALL find every valid RUT in the body (not just the first) and, for each, search for its
name bounded by its immediate neighbors — the end of the previous contributor's matched span (RUT
and, if applicable, the name found after it) and the start of the next RUT — so one person's name
can never bleed into an adjacent contributor's record.

#### Scenario: Two contributors, name-then-RUT layout
- **WHEN** the body lists `EDGARD ORLANDO PACHECO CARRASCO 18.552.843-K` followed by
  `JUAN CARLOS LORENZO PATIÑO GAMONAL 15.409.979-4` on the next line
- **THEN** two entries are produced, each with its own correct name and RUT

#### Scenario: Two contributors, RUT-then-name layout (chained)
- **WHEN** the body lists `RUT1 NOMBRE1` immediately followed by `RUT2 NOMBRE2` (Viña-style export,
  name follows its own RUT)
- **THEN** the first contributor's name-window search for the second RUT is bounded to start only
  after the first contributor's own name ends, not merely after the first RUT — otherwise the first
  name would be incorrectly re-matched as the second contributor's name too

### Requirement: Reorder to given-names-first only for the one source with a known fixed order
See the `routing` spec's "Normalize name word order" requirement — this extractor is the component
that performs the swap, gated on which RUT pattern matched (only the space-separated / Viña
pattern), never on word count alone.

### Requirement: Subject-derived extraction never yields a trusted name
When falling back to the subject line (see `routing` spec), the system SHALL strip Outlook's
reply/forward prefixes (`RV:` / `RE:` / `FW:` / `FWD:` / `RES:` / `ENV:`, possibly chained) and a
small set of known request-boilerplate words (`SOLICITUD`, `ANTECEDENTES`, `PETICIÓN`, `CARPETA`)
before running the same RUT+name search as the body — but SHALL always discard whatever name it
finds, returning only the RUT.

#### Scenario: Reply prefix and boilerplate stripped before matching
- **WHEN** the subject is `RV: SOLICITUD ANTECEDENTES SOLANGE KATHERINE ARRIAGADA FERNÁNDEZ RUN
  16.353.860-1`
- **THEN** the RUT `16.353.860-1` is returned; the name is always `null` regardless of what text
  surrounded the RUT

#### Scenario: Boilerplate not in the known list must still never leak into the name
- **WHEN** the subject contains instruction words that are not in the stripped list (e.g.
  `SUBIR CARPETA PLATAFORMA CONASET`) next to a valid RUT and no real person name at all
- **THEN** the RUT is returned and the name is `null` — this is enforced by never trusting a
  subject-derived name at all, not by trying to enumerate every possible boilerplate phrase (subject
  wording is far less predictable per-comuna than the body, which was calibrated against real
  samples)
