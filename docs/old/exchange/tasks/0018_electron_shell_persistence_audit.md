# Codex Task 0018 — Electron shell and SQLite save audit

## Goal

Move MOCKUPS from the local browser/debug workflow toward a desktop Electron shell, while first auditing and fixing SQLite persistence.

The user suspects changes may not actually be saved to SQLite in some cases. Before adding native file/font pickers or more UI features, verify the full save path.

This task replaces the previous planned `0018_screen_instance_creation` task if it has not been executed yet.

## Current context

Read these files first:

```text
docs/architecture/00_project_vision.md
docs/architecture/01_data_model.md
docs/architecture/02_render_architecture.md
docs/architecture/03_visual_modules.md
docs/architecture/04_shot_builder.md
docs/architecture/05_decisions_log.md
docs/architecture/07_initial_data_schema.md
docs/architecture/08_visual_tokens_layout_contract.md
docs/architecture/09_foundational_module_contracts.md
docs/architecture/10_module_theme_configs.md
PROJECT_STATUS.md
```

Review recent task responses:

```text
docs/exchange/responses/0013_debug_calibration_ui_response.md
docs/exchange/responses/0014_core_app_shell_response.md
docs/exchange/responses/0015_json_tree_editor_response.md
docs/exchange/responses/0016_module_theme_configs_response.md
```

Review current implementation:

```text
src/debug-ui/
src/debug-server/
src/debug-ui/components/json-editor/
src/domain/schemas/
src/domain/repository/
src/domain/resolvers/
src/persistence/sqlite/
src/visual/
src/remotion/
package.json
vite.config.ts
```

## User concern

The user believes some edits may not be saved to SQLite.

Do not assume autosave is correct because tests pass. Explicitly verify persistence across:

```text
edit in UI
  ↓
autosave request
  ↓
server validation
  ↓
SQLite write
  ↓
reload app/server
  ↓
read value back from SQLite
  ↓
preview uses persisted value
```

## Phase decision

It now makes sense to introduce Electron because upcoming features need native system access:

```text
file picker
font picker / installed font inspection
asset paths
future local export
```

However, Electron should wrap the existing app shell and persistence path rather than rewriting the architecture.

## Important architecture constraints

Preserve these decisions:

- SQLite remains the local persistence source.
- JSON fields remain canonical for flexible module/config/token data.
- `screen_instances.module_data_json`, `module_config_json`, and `module_tokens_override_json` are canonical for Chat screen instances.
- `module_theme_configs` hold module-specific design tokens.
- RenderableNode is calculated output and read-only.
- Remotion preview is an adapter over RenderableNode, not source of truth.
- Electron must not make visual modules access the database directly.
- Electron native APIs must be exposed through a safe preload/API boundary, not by enabling broad Node access in the renderer.

## Scope

Implement only:

1. Audit/fix SQLite save persistence for current app shell edits.
2. Add explicit persistent-save verification tests.
3. Introduce a minimal Electron shell around the existing app shell.
4. Keep the existing browser/Vite dev workflow working if practical.
5. Prepare safe Electron API boundaries for future file/font pickers, without implementing full pickers yet unless trivial.
6. Update run scripts.
7. Update `PROJECT_STATUS.md`.
8. Create the Codex response file for this task.

## Do not implement

Do not implement yet:

- full native file picker workflow
- full font picker
- asset import/copy workflow
- export/render pipeline
- Electron packaging/installer
- screen instance creation flow
- specialized Chat editor
- undo/redo
- production-ready menu system
- auto-updater

This task is persistence audit + minimal Electron shell only.

## Part A — SQLite save audit

Audit the current save path:

```text
JsonTreeEditor / scalar editor
  ↓
client update call
  ↓
debug/app server
  ↓
validation
  ↓
SQLite update
  ↓
app state reload
  ↓
resolver/preview
```

Check for common failure causes:

- UI edits remain only in React state.
- debounce does not fire before navigation/reload.
- server accepts patch but does not update DB.
- backend writes to a different database path than the app reads.
- `db:seed` overwrites changes when launching.
- app always reloads seeded defaults.
- in-memory path is accidentally used instead of SQLite for preview.
- JSON validation succeeds client-side but fails server-side and error is not visible.
- browser cache/state shows stale values after save.
- app UI says saved before SQLite write completes.

## Required persistence tests

Add or update tests/smoke checks that prove persistence survives reload.

At minimum, test one scalar field and one JSON field.

Suggested cases:

```text
1. Modify a theme name or shot name.
2. Modify a nested value in theme.tokens_json or module_config_json.
3. Save through the same API used by the UI.
4. Close/reopen repository or restart server-equivalent object.
5. Read from SQLite directly or via SQLiteRepository.
6. Assert the edited value is still present.
7. Resolve preview payload and assert it uses the persisted value where applicable.
8. Restore original values after test.
```

Do not rely only on in-memory state.

## Required UI behavior updates

If not already present, ensure:

- save state only shows `Saved` after server/SQLite write success.
- failed server validation appears clearly.
- a visible error appears when save fails.
- reload/revert from DB works or is clearly available.
- dirty state is not cleared prematurely.

## Part B — Electron shell

Add a minimal Electron shell that loads the existing app UI.

Recommended approach:

- Keep Vite/React renderer.
- Add Electron main process.
- Add secure preload bridge.
- Use context isolation.
- Do not enable broad Node integration in renderer.
- Keep local SQLite/server access either in Electron main or existing local server, whichever is simplest and safest for this phase.

If using the existing local debug/app server inside Electron, document it clearly.

If moving server logic into Electron main, keep it small and avoid changing domain/resolver boundaries.

## Electron requirements

The Electron shell should:

- open the current app shell UI.
- use the same SQLite development database path or a clearly documented Electron dev database path.
- support current preview workflow.
- not break the browser dev workflow if practical.
- not require packaging yet.

## Future native API preparation

Expose a minimal placeholder preload API for future native integrations, for example:

```text
window.mockupsNative.pickFile(...)
window.mockupsNative.listFonts(...)
```

It is acceptable for these to be stubbed or not implemented yet, but define the intended safe boundary if useful.

Do not implement full file/font pickers unless doing so is trivial and does not expand scope.

## Scripts

Add clear scripts.

Possible examples:

```json
{
  "app": "...existing browser app...",
  "electron": "...start electron dev shell...",
  "electron:check": "...smoke check if practical..."
}
```

Keep existing scripts working:

```text
npm run app
npm run app:check
npm run app:build
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm run validate:sqlite
npm run remotion:check
npm test
```

## Validation requirements

All existing validation should still pass:

```text
npm run typecheck
npm run app:check
npm run app:build
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm run validate:sqlite
npm run remotion:check
npm test
```

Add a persistence-specific test/check if it does not already exist, for example:

```text
npm run app:persistence-check
```

Add an Electron smoke check if practical, but do not make it brittle.

## Documentation update

Update `PROJECT_STATUS.md` to reflect:

- core app shell exists.
- structured JSON editor exists.
- SQLite persistence has been audited.
- save/reload persistence is verified.
- minimal Electron shell exists.
- native file/font picker work is still future.
- no packaging/export/specialized editor exists yet.

Set next recommended task to one of:

```text
Implement native file picker / asset registration.
Implement installed font picker for theme typography.
Implement screen instance creation flow.
Polish Electron shell and app menus.
```

## Update exchange response

Create this response file:

```text
docs/exchange/responses/0018_electron_shell_persistence_audit_response.md
```

Use this format:

```md
# Codex Response 0018 — Electron shell and SQLite save audit

## Summary

## Files changed

## Questions / conflicts

## Tests

## Run commands

## Persistence audit findings

## Electron notes

## Notes
```

## Notes requirements

In `Persistence audit findings`, include:

- whether saves were actually reaching SQLite.
- any bug found and fixed.
- how persistence across reload is verified.
- whether app/preview uses SQLite or in-memory data.
- whether `db:seed` can overwrite user edits and how that is handled/documented.

In `Electron notes`, include:

- Electron architecture chosen.
- whether app still works in browser mode.
- database path used in Electron mode.
- whether preload/context isolation is used.
- native API boundary status.

## Architecture Question rule

If you find a conflict between:

- architecture docs
- current schemas
- current SQLite schema
- current repository implementation
- current resolver implementation
- current app shell
- Electron integration requirements
- accepted decisions already in the log

do not silently invent a new architecture.

Instead, stop and create an Architecture Question in the response file.

## Acceptance criteria

- SQLite save path is audited.
- At least one scalar edit persists across reload/repository reopen.
- At least one JSON edit persists across reload/repository reopen.
- Save state does not report saved before successful persistence.
- Any discovered save bug is fixed or clearly documented.
- Minimal Electron shell launches the current app UI.
- Existing browser app workflow remains working unless there is a documented reason.
- Existing validation commands pass.
- npm test passes.
- PROJECT_STATUS.md is updated.
- Response file exists in docs/exchange/responses/.
- No final file picker, font picker, asset manager, packaging, export pipeline or specialized Chat editor is added.
