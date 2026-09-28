## ADDED Requirements

### Requirement: Register old folders straight into Caja
The manual entry form SHALL offer an option to register old folders directly into the Caja queue. With it, each valid row SHALL be stored as uploaded, sent to the open Caja queue in entry order, and SHALL NOT send any email.

#### Scenario: Old folders go to the queue without email
- **WHEN** the operator enters two people with "Carpeta antigua: enviar directo a Caja" ticked
- **THEN** both cases are in the Caja queue in entry order
- **AND** neither appears in Casos
- **AND** no email is sent

#### Scenario: Without the option nothing changes
- **WHEN** the operator enters a person without ticking the option
- **THEN** the case is created as Pendiente in Casos
