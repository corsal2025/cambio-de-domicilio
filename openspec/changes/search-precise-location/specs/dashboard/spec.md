## ADDED Requirements

### Requirement: Precise Caja location in search
When a Casos search matches cases that are in Caja, the dashboard SHALL show, for each match, the box code, the listing's close date and the case's N° (1-based position) in that listing, or "cola de Caja" with its position. Each entry SHALL link to that listing with the row highlighted and scrolled into view.

#### Scenario: Case in the second listing sharing a code
- **WHEN** two closed listings both have code `A1-CD` and the searched person is N° 2 of the second one
- **THEN** the banner shows `A1-CD`, the second listing's close date, and N° 2
- **AND** its link opens that listing with row N° 2 highlighted

#### Scenario: Case in the open queue
- **WHEN** the searched person is third in the open Caja queue
- **THEN** the banner shows "cola de Caja" and N° 3, linking to the queue with the row highlighted
