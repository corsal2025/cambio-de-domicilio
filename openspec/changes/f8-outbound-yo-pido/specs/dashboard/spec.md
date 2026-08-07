# Spec: Dashboard (delta)

## ADDED Requirements

### Requirement: F8 tab switcher for inbound vs outbound cases
The F8 dashboard page SHALL provide a tab switcher with two tabs: "Me piden" (existing Inbound cases, list and actions unchanged from today) and "Yo pido" (Outbound cases). The active tab SHALL be carried via query string so it survives page refresh and redirects after a POST action.

#### Scenario: Default tab on first visit
- **WHEN** an authenticated user opens the F8 page with no tab specified in the query string
- **THEN** the "Me piden" tab is shown, listing exactly today's Inbound cases with unchanged behavior

#### Scenario: Switching to "Yo pido"
- **WHEN** the user selects the "Yo pido" tab
- **THEN** the query string reflects the selected tab and only `Direction = Outbound` cases are listed

#### Scenario: Tab persists across a POST action
- **WHEN** the user performs an action on the "Yo pido" tab and the page reloads
- **THEN** the reloaded page still shows the "Yo pido" tab, not "Me piden"

### Requirement: "Yo pido" tab shows a single "Enviar solicitud" action
For each Outbound case in the "Yo pido" tab, the dashboard SHALL show only one action, "Enviar solicitud", available on `Pending` and review-complete cases. It SHALL NOT show "Traspaso", "Marcar subida y confirmar", or any PDF-related action for Outbound cases, since those steps do not exist in the outbound workflow.

#### Scenario: Eligible Pending outbound case
- **WHEN** a Pending, review-complete Outbound case is listed
- **THEN** its row shows only the "Enviar solicitud" button and no other action controls

#### Scenario: Sending the request
- **WHEN** the user triggers "Enviar solicitud" on an eligible case
- **THEN** the case transitions to `Requested`, the sending user and timestamp are recorded, and the row updates accordingly with no PDF/Conaset element rendered

### Requirement: Existing Inbound behavior on F8 remains unchanged
The "Me piden" tab SHALL reproduce today's F8 page behavior exactly — case list, filters, countdown, "Enviar confirmación", "Marcar subida", and manual data entry — with no visible or functional difference introduced by the tab switcher.

#### Scenario: Inbound case list and actions
- **WHEN** an authenticated user views the "Me piden" tab
- **THEN** every existing action and displayed field for Inbound cases behaves exactly as it did before this change
