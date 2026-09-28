## ADDED Requirements

### Requirement: Authorship mark
Every dashboard screen SHALL show a footer crediting Raúl Salazar as the creator of the model and the system. The footer SHALL NOT appear in printed documents.

#### Scenario: Footer on screen
- **WHEN** the operator opens Casos, F8, Caja, Estadísticas, Comunas or Descartados
- **THEN** the page ends with "Modelo y sistema creados por Raúl Salazar"

#### Scenario: Not printed
- **WHEN** a sector or box listing is printed
- **THEN** the authorship footer is not part of the printout
