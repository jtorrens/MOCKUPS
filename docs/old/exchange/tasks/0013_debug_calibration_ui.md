# Codex Task 0013 — Minimal debug calibration UI

## Goal

Create a minimal debug calibration UI for MOCKUPS.

This UI should help inspect and calibrate the current architecture visually:

```text
SQLiteRepository / current data
  ↓
shot + frame selection
  ↓
resolver
  ↓
RenderableNode tree
  ↓
Remotion preview
```

The UI must make it easy to inspect and edit the correct source fields:

```text
screen_instances.module_data_json
screen_instances.module_config_json
screen_instances.module_tokens_override_json
theme.tokens_json
device.metrics_json
device_state.state_json
RenderableNode output (read-only)
```

This is not the final editor. It is a technical calibration/debug tool.

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
PROJECT_STATUS.md
```

Review recent task responses:

```text
docs/exchange/responses/0010_sqlite_persistence_response.md
docs/exchange/responses/0011_foundational_module_contracts_response.md
docs/exchange/responses/0012_chat_module_data_canonical_response.md
```

Review current implementation:

```text
src/domain/schemas/
src/domain/repository/
src/domain/resolvers/
src/persistence/sqlite/
src/visual/
src/remotion/
```

## Important architecture constraints

Preserve these decisions:

- Shot means a device-screen action sequence, not external video placement.
- Chat module data is canonical in `screen_instances.module_data_json`.
- Chat behavior/config is canonical in `screen_instances.module_config_json`.
- Per-instance visual exceptions are canonical in `screen_instances.module_tokens_override_json`.
- Debug UI must not edit legacy `conversations`, `conversation_participants`, or `messages`.
- RenderableNode is calculated output and must be read-only.
- Remotion is an adapter over RenderableNode, not the source of truth.
- Visual modules must not access DB/repositories directly.
- SQLite should not leak into resolvers/visual modules.

## Scope

Implement only a minimal debug/calibration UI.

Required features:

1. Load the development SQLite database.
2. List/select available productions.
3. List/select shots for the selected production.
4. List/select screen instances for the selected shot.
5. Provide a frame slider/input.
6. Show a Remotion preview for the selected shot/frame or current example.
7. Show editable JSON panels for:
   - `screen_instances.module_data_json`
   - `screen_instances.module_config_json`
   - `screen_instances.module_tokens_override_json`
   - selected `theme.tokens_json`
   - selected `device.metrics_json`
   - selected `device_state.state_json`
8. Validate JSON before saving.
9. Save edits back to SQLite.
10. Re-resolve and refresh preview after save.
11. Show read-only panels for:
   - resolved screen/module input if available
   - RenderableNode tree
   - validation errors/warnings
12. Keep existing validation/test commands passing.
13. Update `PROJECT_STATUS.md`.
14. Create the Codex response file for this task.

## Do not implement

Do not create or implement:

- final editor UI
- production-ready UX
- Electron shell
- final export/render pipeline
- asset browser/asset manager
- icon manager
- font picker
- multi-user project management
- authentication
- destructive migrations
- normalized legacy Chat editing
- custom Instagram module
- full module editor framework

This task is a minimal local debug UI only.

## Technology guidance

Use the smallest practical approach compatible with the existing project.

A simple React/Vite or Remotion-adjacent debug app is acceptable if it is the fastest clean route.

Do not add Electron yet.

Do not add large UI frameworks unless already present or clearly justified.

Keep the UI local/dev-only.

If creating a web server/API is necessary to write SQLite from the UI, keep it minimal and document commands clearly.

## Suggested file structure

Use a structure similar to this unless a simpler compatible structure is better:

```text
src/
  debug-ui/
    App.tsx
    main.tsx
    api/
      client.ts
    components/
      JsonEditorPanel.tsx
      PreviewPanel.tsx
      InspectorPanel.tsx
      SelectionPanel.tsx
    styles.css

src/
  debug-server/
    server.ts
    routes.ts
```

If using a different structure, explain it in the response.

## Data source requirements

The debug UI should use SQLite as the editable source.

Use the development database created by:

```text
npm run db:init
npm run db:seed
```

or a combined command if already available.

Do not write edits only to in-memory fixtures.

If the existing Remotion preview currently uses the in-memory bridge, adapt it minimally so the debug UI can preview SQLite-backed data or clearly route the selected SQLite data through the same resolver pipeline.

## Required editable fields

### Screen instance fields

The UI must expose:

```text
module_data_json
module_config_json
module_tokens_override_json
```

These are the main fields the future module editor will own.

For this task, raw JSON textareas/editors are acceptable.

Do not edit legacy normalized Chat tables.

### Theme fields

The UI must expose:

```text
theme.tokens_json
```

This is where reusable design tokens are edited.

### Device fields

The UI must expose:

```text
device.metrics_json
device_state.state_json
```

This is where device designSpace/render metrics and live state are edited.

## JSON validation

Before saving any JSON field:

1. Parse JSON.
2. Validate with the relevant Zod schema where one exists.
3. For module JSON, validate with Chat module schemas when `module_id = core.chat`.
4. Show readable errors.
5. Do not save invalid JSON.

It is acceptable to start with basic parse validation for fields that do not yet have strict schemas, but use existing schemas wherever possible.

## Preview behavior

The preview should be sufficient for calibration.

It should allow:

- frame slider or numeric frame input.
- preview refresh after saving JSON.
- current selected shot/screen instance.
- visible ChatScreen output through the current Remotion adapter.

It does not need:

- transport controls beyond simple frame input.
- export.
- pixel-perfect device chrome.
- full timeline editor.

## Read-only output panels

Show at least one read-only debug panel for:

```text
RenderableNode tree
```

Prefer also showing:

```text
resolved props / resolved module input
```

if easily available.

The RenderableNode tree must not be directly editable.

## Scripts

Add clear scripts.

Possible examples:

```json
{
  "debug:ui": "...",
  "debug:server": "...",
  "debug": "..."
}
```

Use names that fit the implementation.

Document exact run commands in the response file.

Keep existing scripts working:

```text
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm run validate:sqlite
npm run remotion:check
npm test
```

## Validation / smoke checks

Add a smoke check if practical, such as:

```text
npm run debug:check
```

It may validate that:

- debug server starts or route handlers can load data.
- selected seed shot can resolve from SQLite.
- debug payloads include editable JSON and RenderableNode output.

Do not add brittle browser screenshot tests.

## Documentation update

Update `PROJECT_STATUS.md` to reflect:

- SQLite persistence exists.
- Chat module JSON is canonical.
- Minimal debug calibration UI exists.
- Debug UI edits module data/config/token overrides, theme tokens, and device metrics/state.
- RenderableNode remains read-only calculated output.
- No Electron shell or final editor/export pipeline exists yet.

Set next recommended task to:

```text
Review the debug UI workflow visually, then decide whether to improve calibration UX, add a font picker, add an asset picker, or start an Electron shell.
```

## Update exchange response

Create this response file:

```text
docs/exchange/responses/0013_debug_calibration_ui_response.md
```

Use this format:

```md
# Codex Response 0013 — Minimal debug calibration UI

## Summary

## Files changed

## Questions / conflicts

## Tests

## Run commands

## Notes
```

## Notes requirements

In `## Notes`, include:

- what technology was used for the debug UI.
- how SQLite is accessed.
- which fields are editable.
- which fields are read-only.
- how validation errors are shown.
- whether Remotion preview uses SQLite-backed data.
- any shortcuts/limitations.
- recommended next UX improvements.

## Architecture Question rule

If you find a conflict between:

- architecture docs
- current schemas
- current SQLite schema
- current repository implementation
- current resolver implementation
- current Remotion adapter
- accepted decisions already in the log

do not silently invent a new architecture.

Instead, stop and create an Architecture Question in the response file.

## Acceptance criteria

- Minimal debug UI exists.
- Debug UI can select production/shot/screen instance or at least load the seeded example.
- Debug UI can edit `module_data_json`.
- Debug UI can edit `module_config_json`.
- Debug UI can edit `module_tokens_override_json`.
- Debug UI can edit `theme.tokens_json`.
- Debug UI can edit `device.metrics_json`.
- Debug UI can edit `device_state.state_json`.
- JSON is validated before save.
- Invalid JSON is not saved.
- Debug UI shows Remotion preview or equivalent current visual preview.
- Debug UI shows RenderableNode output as read-only.
- Existing validation commands still pass.
- `npm test` passes if present.
- `PROJECT_STATUS.md` is updated.
- Response file exists in `docs/exchange/responses/`.
- No Electron shell, final editor UI, export pipeline, asset manager, font picker or legacy Chat editor is added.
