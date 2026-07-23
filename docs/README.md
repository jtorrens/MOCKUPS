# MOCKUPS documentation

Status: authoritative index.

The active documentation for MOCKUPS is the set listed in
`docs/architecture/README.md`. It describes the application, data model,
authoring workflows, Preview pipeline and development rules as they exist now.

## Historical archive prohibition

`docs/old` is a sealed historical archive. Agents and contributors must not
open, search, read, quote, summarize, cite or use any file under `docs/old` for
design, architecture, implementation, debugging, validation or product
decisions unless the user explicitly authorizes that exact historical
consultation.

An instruction to inspect the repository, read the documentation, audit the
architecture or continue development is not authorization to consult
`docs/old`.

If active documentation is incomplete, inspect the current code, schema,
manifest, committed database and tests, then update the active canonical
document. Do not fill a gap from the historical archive.

## Active entry points

- `AGENTS.md`: mandatory working rules.
- `docs/architecture/README.md`: canonical architecture index.
- `src/desktop-preview/desktopPreviewManifest.json`: current Preview route
  manifest.
- `spikes/desktop-editor-shell/Data/SpikeDatabase.Schema.cs`: current physical
  SQLite schema.
- `spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs`: executable
  current-database validation.

Only files outside `docs/old` may define current behavior.
