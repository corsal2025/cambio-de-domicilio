## Why

Today a case only reaches the Caja screen implicitly, and the Casos/F8 action columns don't reflect the physical-folder lifecycle: an uploaded folder has no explicit "send to Caja" action, F8 cases with a found folder have no way to reach Caja, F8 cases without a folder have no way to close, and F8 exposes two different revert actions (`UndoTransfer`, `RevertToCasos`) with different side effects. Operators need one clear, explicit path from upload or F8 to the physical box, plus a clean way to close a case that has no folder.

## What Changes

- **Casos action column**: an uploaded or confirmed case with a physical folder shows a **Caja** action. Pressing it moves the case to the Caja queue and removes it from Casos. (flujo-proceso Paso 6 → Paso 7, physical folder archiving after upload)
- **Casos action column**: a case reincorporated from F8 (`SoloCaja`) shows **only** the Caja action: no "Marcar subida", no confirmation, no email. (Paso 6)
- **F8 action column**: every F8 case shows two actions:
  - **Caja**: the folder was found, so the case goes straight to the Caja queue (same effect as the Casos Caja action).
  - **Sin carpeta**: the process is closed without a folder. The case returns to Casos with status "Cerrado sin carpeta", visible and without actions. (Paso 6, alternative branch)
- **F8 revert, unified**: a single **Revertir** action replaces `UndoTransfer` and `RevertToCasos`. Whether or not the F8 was already uploaded, it clears all F8 data (`CodigoF8`, F8 dates, F8 flags) and returns the case to Casos showing only the Caja action. **BREAKING** (UI): the separate "Deshacer traspaso" action is removed.
- **Caja screen**: every queued case has a **Devolver a casos** action. The case goes back to Casos with its Caja action available again.
- **Visual redesign** of the action buttons on Casos, F8 and Caja: inline SVG icons, one color per action type (upload blue, Caja green, Sin carpeta amber, delete red on hover), and tooltips with accessible labels.

## Non-goals

- No confirmation-email changes. Paso 7 stays exactly as it is.
- No audit trail for F8 reversions. The operator explicitly chose to clear the F8 data completely.
- No changes to closing, reopening or numbering boxes on the Caja screen.
- No new filter for "Cerrado sin carpeta". It is shown inline in Casos.
- No automatic transfer to Caja on upload. The transfer is always an explicit operator click.

## Capabilities

### New Capabilities
- `caja-flow`: how cases enter and leave the Caja queue (from Casos, from F8, returning to Casos), and the "Cerrado sin carpeta" closure of F8 cases.

### Modified Capabilities
- `dashboard`: "Case list reflecting the real lifecycle" changes. Casos hides cases sent to Caja, shows "Cerrado sin carpeta" cases without actions, shows Caja-only actions for reincorporated F8 cases, and the action buttons are redesigned.

## Impact

- `Domain/PersonRequest.cs`: new closed-without-folder state (field), separate from the existing `SinCarpeta` "S/C date" flag.
- `Persistence/PersonRequestRepository.cs`: schema column, `CloseWithoutFolder`, a unified `RevertF8` (it replaces `RevertF8AndReturnToCasos`, clears `CodigoF8`), `SendToCaja` accepting F8 cases, and the Casos query excluding `Destination = Caja`.
- `Dashboard/Pages/Index`, `F8`, `Caja` (`.cshtml` + `.cshtml.cs`): new and removed handlers, action markup.
- `wwwroot/css/dashboard.css`: action-button styles.
- Tests: `PersonRequestRepositoryTests`, page-model tests where they exist.
- `docs/flujo-proceso.md`: Paso 6 branch (Caja / Sin carpeta / Revertir F8).
