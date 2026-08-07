## ADDED Requirements

### Requirement: F8 subnav link to Penúltimas Carpetas PDF
The F8 dashboard page SHALL show a third subnav link, "PDF Penúltimas Carpetas", alongside the existing "PDF Archivo" and "PDF Oficina 43" links, navigating to the Penúltimas Carpetas PDF document.

#### Scenario: Operator navigates to the new tab from F8
- **WHEN** an authenticated user on the F8 page clicks "PDF Penúltimas Carpetas"
- **THEN** the user is taken to the Penúltimas Carpetas PDF document for F8 cases

### Requirement: Dashboard-wide pending-print warning banner
Every authenticated dashboard page SHALL render a shared layout-level warning banner component that shows the end-of-month Penúltimas Carpetas pending-print warning (see `f8-penultimas-carpetas-pdf` capability) when applicable, so the operator sees it regardless of which page they land on.

#### Scenario: Banner shown from the shared layout
- **GIVEN** at least one pending Penúltimas Carpetas month exists
- **WHEN** the operator loads Estadisticas, Casos, F8, or any other authenticated dashboard page
- **THEN** the shared layout renders the warning banner on that page
