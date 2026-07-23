# Codex Response 0018 — Electron shell and SQLite save audit

## Summary

Implemented the SQLite persistence audit and a minimal Electron development shell.

The critical finding was that normal app startup was not reseeding the database, but `npm run validate:sqlite` was validating against `data/mockups-dev.sqlite` and calling the destructive fixture seed. That could overwrite edited development data when validation was run. This has been fixed by moving SQLite validation to an isolated in-memory database.

`npm run db:seed` is now non-destructive: it seeds only when the persistent database has no productions. A new explicit `npm run db:reset` command owns the destructive reset behavior.

## Files changed

- `package.json`
  - Added `db:reset`.
  - Added `app:persistence-check`.
  - Added `electron`.
  - Added `electron:check`.
  - Added Electron as a dev dependency.
- `package-lock.json`
  - Updated for Electron.
- `src/persistence/sqlite/seedDevelopmentDatabase.ts`
  - Changed `db:seed` to skip existing databases with production data.
- `src/persistence/sqlite/resetDevelopmentDatabase.ts`
  - Added the explicit destructive reset command.
- `src/persistence/sqlite/validateSQLiteRepository.ts`
  - Changed validation to use `:memory:` instead of the persistent development database.
- `src/debug-server/checkPersistence.ts`
  - Added an isolated persistence check that writes scalar and JSON edits, closes/reopens SQLite, and confirms preview payloads use persisted values.
- `src/electron/main.cjs`
  - Added a minimal Electron BrowserWindow shell for the existing app UI.
- `src/electron/preload.cjs`
  - Added a narrow `window.mockupsNative` preload bridge.
- `src/electron/startElectron.ts`
  - Added startup orchestration for the debug server/UI and Electron.
  - Explicitly removes `ELECTRON_RUN_AS_NODE` before launching Electron.
- `src/electron/checkElectronSetup.ts`
  - Added a static Electron setup check.
- `docs/architecture/01_data_model.md`
  - Updated hierarchy language to include episodes between productions and shots.
- `docs/architecture/07_initial_data_schema.md`
  - Updated schema notes for episodes and persistence expectations.
- `docs/architecture/05_decisions_log.md`
  - Added D037 for non-destructive startup/validation.
  - Added D038 for the Electron shell/native boundary.
- `PROJECT_STATUS.md`
  - Updated current status, implemented decisions, validation notes, and next steps.

## Questions / conflicts

No blocking questions.

One important operational note: `npm run electron` starts its own API/UI servers. If an existing `npm run app` or `npm run debug` process is already occupying ports `4173` or `4174`, Electron startup will fail until those old processes are stopped.

## Tests

Passed:

- `npm run typecheck`
- `npm run app:persistence-check`
- `npm run electron:check`
- `npm run app:build`
- `npm run app:check`
- `npm run validate:examples`
- `npm run validate:resolver`
- `npm run validate:visual`
- `npm run validate:sqlite`
- `npm run remotion:check`
- `npm test`
- `git diff --check`
- `npm run db:seed`
- `npm run electron`

## Run commands

Normal app/browser workflow:

```bash
npm run app
```

Electron development shell:

```bash
npm run electron
```

Persistence smoke check:

```bash
npm run app:persistence-check
```

Non-destructive seed for an empty development database:

```bash
npm run db:seed
```

Explicit destructive fixture reset:

```bash
npm run db:reset
```

## Persistence audit findings

Current save path:

- The debug API writes through `updateAppRecord`.
- Scalar fields and JSON fields are validated before writing.
- Valid updates are persisted with SQL `UPDATE`.
- Preview payloads are reloaded through the SQLite repository and resolver path, not from stale in-memory state.

Confirmed by `npm run app:persistence-check`:

- A scalar shot edit persisted after closing and reopening SQLite.
- A `screen_instances.module_config_json` edit persisted after closing and reopening SQLite.
- The preview payload used the persisted SQLite values after reopening.

Startup/reseed audit:

- `npm run app` does not reseed existing SQLite data.
- `npm run debug` does not reseed existing SQLite data.
- `npm run app:build` does not open or reseed the persistent SQLite database.
- `npm run electron` does not reseed existing SQLite data.
- Electron startup uses the same debug server persistence path as the browser app.
- `createDatabase(...)` creates/migrates schema only; it does not seed fixture data.
- `npm run db:seed` now skips when the persistent database already contains productions.
- `npm run db:reset` is now the only explicit destructive reset command.
- `npm run validate:sqlite` now uses an isolated in-memory database, so validation cannot overwrite edited development data.

The old risky behavior was:

- `validateSQLiteRepository.ts` opened `data/mockups-dev.sqlite`.
- It then called `seedExampleDataset(database)`.
- `seedExampleDataset` is intentionally fixture-destructive.

That risk has been removed.

## Electron notes

The Electron work is intentionally minimal:

- It wraps the existing Vite/debug UI instead of creating a second app implementation.
- It keeps SQLite access in the existing debug server/repository layer.
- The renderer has `nodeIntegration: false`.
- The renderer has `contextIsolation: true`.
- Native capabilities are exposed through a narrow preload bridge.

Current preload bridge:

- `mockupsNative.pickFile()`
- `mockupsNative.listFonts()`

`pickFile` is a minimal future boundary for asset/file import workflows. `listFonts` is a placeholder returning an empty list until a real font enumeration implementation is needed.

## Notes

This task did not implement the full native file picker workflow, font picker, export/package system, or specialized module editors. It only introduced the safe Electron shell boundary and audited/fixed persistence reset behavior.
