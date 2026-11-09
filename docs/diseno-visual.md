# Operational dashboard presentation

Dashboard views include `Shared/_OperationalTheme.cshtml`, which loads the
operational and spatial presentation layers after the base theme. The interface
uses locally hosted assets and requires no external fonts or services.

On screens at least 1100 px wide, the application sections appear in a sidebar.
The navigation toggle expands the workspace and remembers the preference in the
browser. On smaller windows, section links wrap into the normal page flow.

The presentation retains all operational columns, forms, confirmations and
existing actions. Status filters appear beside the workspace rather than in the
global navigation. Printed listings retain their original styles.

See `spatial-identity.md` for Lico, motion, Focus mode, the read-only record dialog,
validation evidence and the boundary for reverting the spatial identity.

## Manual verification

- Open Cases, F8, Caja, Statistics and search with representative data.
- Check desktop and mobile widths, navigation and table scrolling.
- Toggle navigation by keyboard, reload and confirm the saved preference.
- Verify sticky headers and frozen identity columns while scrolling.
- Review marked, pending, confirmed and error states.
- Open the guide and print previews for official listings.

## Operational layer rollback

First remove the spatial layer as described in `spatial-identity.md`. To remove
the remaining operational layer, remove `_OperationalTheme` from the 12 views
and delete that partial, `operacional.css` and `operacional.js`. No database or
business-handler rollback is required.
