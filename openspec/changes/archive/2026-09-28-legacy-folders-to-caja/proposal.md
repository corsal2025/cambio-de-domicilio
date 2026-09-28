## Why

The operator has old physical folders (Cambio de Domicilio) that were never registered. They must go into Caja, but the comunas must not be emailed: those requests were settled long ago.

## What Changes

- The manual entry form gets a "Carpeta antigua: enviar directo a Caja (sin correo)" checkbox. When ticked, each entered person is registered and sent straight to the Caja queue, without any email and without appearing as Pendiente in Casos.
- Their source is recorded as "Carpeta antigua ingresada manualmente (sin correo)" for traceability.

Mapped to docs/flujo-proceso.md Paso 7b (carpeta física a Caja).

## Non-goals

- No bulk import from files; entry stays through the existing multi-row form.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `caja-flow`: manual registration of old folders directly into the Caja queue.

## Impact

- `Dashboard/Pages/Index.cshtml(.cs)`: `OnPostAddManualCases(..., directToCaja)`, a form checkbox.
- Tests: `IndexModelTests`.
