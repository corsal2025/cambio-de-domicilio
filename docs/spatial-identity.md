# Spatial identity and Lico

The shared operational partial loads `spatial.css` and `spatial.js` after the
base theme. It also supplies the local mascot image URL through a meta tag.
The approved original Lico design is preserved in `images/lico-volumen-v1.webp`
with illustrated depth and transparency (640 px, about 51 KB). The generated
concept was created with the built-in ImageGen tool and optimized for local use.

The screen shell uses graphite surfaces, blue accents and an explicit Raúl
Salazar authorship mark. Operational tables retain light backgrounds and their
existing status colors. All spatial styling is scoped to screen media.

Lico reacts to pointer entry and pointer position through bounded transforms.
The guide uses the same image. These are image-based depth effects, not a
rigged 3D mesh. No animation library, external font or network service is required.
Reduced-motion preferences and the persistent Focus mode disable motion.

Cases and F8 rows with editable names expose a read-only record dialog. It reads
visible field values and checkboxes from the selected row, excludes hidden fields
and action forms, and renders data with textContent. It performs no network
requests and has no save, delete, transfer or email behavior. Native dialog
behavior provides Escape handling, focus trapping and return focus.

## Reversal boundary

Remove the spatial stylesheet/script and mascot meta tag from
`Shared/_OperationalTheme.cshtml`, and revert the image preference and image
mount guard in `guia-interactiva.js`. The spatial CSS/JS and WebP can then be
removed. Business data and handlers require no rollback or migration.

## Validation

JavaScript syntax and Release publishing are checked before deployment. Runtime
checks cover opening/closing the record dialog, guide navigation, focus mode,
mascot loading, desktop/mobile layouts and unchanged SQLite contents. Deployment
uses a separate staging folder and backs up the active installation before restart.

### Deployment receipt — 2026-11-09

- Active installation: existing `publish` executable, restarted after staging.
- Regression results: 514 passed, 0 failed, 0 skipped
  (`scratch/spatial-tests/spatial-identity.trx`).
- HTTP verification: all 15 checked pages/assets returned 200
  (`scratch/spatial-tests/routes.json`).
- Browser: Cases dialog opened with 11 fields and closed; Focus mode toggled and
  restored; the guide opened with the new image and closed; F8 exposed 51 record
  buttons; Sin Carpetas retained 44 folder-restoration buttons; Statistics showed
  nine chart canvases. No browser console errors were recorded on the F8 check.
- At a 390 px viewport, document width measured 375 px; the remaining table
  width stayed within its scroll container. Desktop rows measured about 59 px.
- All seven SQLite table content digests and counts matched the stopped-process
  backup after the browser/HTTP checks: 517 person requests and 23 boxes.
- Production configuration hashes were unchanged. Verification submitted no case
  mutations, synchronization or emails.
- Latest rollback backup: `scratch/visual-backup-20261109-175056`.
- NuGet vulnerability metadata remained unreachable (NU1900); build, publish and
  the regression suite completed successfully using the available dependencies.
