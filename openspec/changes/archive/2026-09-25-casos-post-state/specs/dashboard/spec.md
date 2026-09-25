## ADDED Requirements

### Requirement: Casos keeps list state across actions
After any POST action in Casos, the dashboard SHALL redirect back to the same list state (status filter, needsReview, bounced, search) and SHALL show the action's result message once.

#### Scenario: Search kept after Caja
- **WHEN** the operator searches "17.143.599-4" and presses Caja on a result
- **THEN** the page reloads with search "17.143.599-4" still applied
- **AND** the message "Carpeta de … enviada a Caja" is shown

#### Scenario: Filter kept after checkbox toggle
- **WHEN** the operator is on the "Pendientes" filter and ticks Marcar on a row
- **THEN** the page reloads still filtered by Pendientes
