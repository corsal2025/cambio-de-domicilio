# Visual update deployment verification

Deployed on 2026-11-09 to the existing `publish` installation.

- Release: self-contained Windows x64 single-file executable.
- Regression suite: 511 passed, 0 failed, 0 skipped (`scratch/deploy-tests/predeploy.trx`).
- HTTP checks: 18 routes returned 200, including operational pages, status filters,
  search, printable sector listings, and the new CSS/JavaScript assets.
- Browser checks: Cases rendered with the new theme; the navigation hide/show
  controls worked; Statistics displayed its chart elements. At a 390 px viewport,
  measured document width stayed within the viewport.
- SQLite schema stayed at version 3. All seven application tables retained
  identical counts and SHA-256 content digests before deployment and after checks.
  The database contains 514 person requests and 22 boxes.
- Production configuration hashes were unchanged. Credentials, certificates,
  data-protection keys and existing startup entry points were preserved.
- No email synchronization, confirmations, edits or deletions were triggered.

Backup: `scratch/visual-backup-20261109-100025` contains previous application files,
static assets, configuration, keys and runtime data. Deployment logs and database
comparison receipts are stored there. The exact backup location is also recorded
in `scratch/last-visual-backup.txt`.

The package restore emitted NU1900 because the NuGet vulnerability service was
unreachable. Compilation and publishing succeeded using the available packages.
