# Codex Task 0014 — Core app shell with data tabs, autosave and preview panel

## Goal

Create the first practical MOCKUPS app UI shell.

This should move beyond the technical debug UI into a simple app layout for editing core system tables while keeping the current Chat module as the preview/reference module.

The UI should have:

```text
left panel  = tabs for core tables/entities
right panel = persistent preview/debug output
```

This is still not the final production UI, but it should become the base app workflow.

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
docs/exchange/responses/0011_foundational_module_contracts_response.md
docs/exchange/responses/0012_chat_module_data_canonical_response.md
docs/exchange/responses/0013_debug_calibration_ui_response.md
```

Review current implementation:

```text
src/debug-ui/
src/debug-server/
src/domain/schemas/
src/domain/repository/
src/domain/resolvers/
src/persistence/sqlite/
src/visual/
src/remotion/
```

## Current phase decision

The previous debug calibration UI proved that:

- SQLite edits affect the render.
- JSON parameters can vary the visual display.
- Chat renders through the current module/resolver/RenderableNode pipeline.

Do not keep expanding the debug UI as a separate workflow.

Instead, start shaping the actual app shell around the core tables/entities.

The existing Chat module/render can remain as the preview/reference module until the base tables and app structure are more mature.

## Important architecture constraints

Preserve these decisions:

- Shot means a device-screen action sequence, not external video placement.
- The app does not composite the device into a video plate.
- The app outputs device-screen animation at device render resolution or optional output scale.
- Chat module data is canonical in `screen_instances.module_data_json`.
- Chat config is canonical in `screen_instances.module_config_json`.
- Per-instance visual exceptions live in `screen_instances.module_tokens_override_json`.
- RenderableNode is calculated output and should not be edited directly.
- Remotion is an adapter/preview layer over RenderableNode, not source of truth.
- Visual modules must not access DB/repositories directly.
- SQLite should not leak into resolvers/visual modules.

## UI layout requirement

Create a two-panel layout:

```text
┌──────────────────────────────┬──────────────────────────────┐
│ Left panel                   │ Right panel                  │
│                              │                              │
│ Tabs for core tables/entities│ Preview / module output      │
│ Forms / JSON editors         │ Frame selector               │
│ Autosave status              │ Warnings/errors              │
│                              │ Read-only inspectors optional│
└──────────────────────────────┴──────────────────────────────┘
```

## Left panel tabs

Implement tabs for the core system entities.

Required tabs:

```text
Productions
Shots
Screen Instances
Actors
Themes
Devices
Device States
Media Assets
Render Presets
```

Optional if already easy:

```text
Apps
Animation Presets
Screen Templates
```

Each tab should allow at least:

- listing records.
- selecting one record.
- editing stable fields where practical.
- editing JSON fields where applicable.
- creating a new record if easy, otherwise leave creation for a later task.
- deleting records is optional and can be deferred.

This task may start with basic forms and raw JSON editors. Do not overbuild.

## Right panel preview

The right panel should remain visible across tabs.

It should include:

- selected production/shot/screen instance context.
- frame slider or numeric frame input.
- current visual preview using the existing Chat/RenderableNode/Remotion adapter path.
- warnings/errors.
- read-only RenderableNode inspector, preferably collapsible.
- read-only resolved props/module input inspector if available, preferably collapsible.

The right panel should update when relevant autosaved fields change.

## Selection behavior

The UI should maintain a current selection context:

```text
selectedProductionId
selectedShotId
selectedScreenInstanceId
selectedFrame
```

When selecting a production:

- update shot list.

When selecting a shot:

- update screen instance list.
- preview that shot/screen if possible.

When selecting a screen instance:

- show its fields in the Screen Instances tab.
- preview it if supported.

If selected screen instance is unsupported or inactive at the frame, show a clear warning rather than fabricating output.

## Autosave requirement

Implement validated autosave from this task.

### Simple fields

Simple scalar fields should autosave after debounce.

Examples:

```text
name
description
fps
duration_frames
start_frame
end_frame
layer_order
theme_id
device_id
owner_actor_id
```

Use a debounce, for example 500–800 ms.

### JSON fields

JSON fields should autosave only if valid.

Examples:

```text
module_data_json
module_config_json
module_tokens_override_json
theme.tokens_json
device.metrics_json
device_state.state_json
metadata_json
```

Flow:

```text
user edits JSON
  ↓
parse JSON
  ↓
if invalid: show error and do not save
  ↓
if valid: validate with Zod when schema exists
  ↓
debounced save
  ↓
refresh affected preview/output
```

Invalid JSON must never be persisted.

### Save states

Show save state per active editor or record:

```text
Saved
Unsaved changes
Invalid JSON
Saving…
Save failed
```

A manual `Save now` button is optional, but recommended.

A `Reload from DB` or `Revert` action is recommended if simple.

## Backend/API requirements

Reuse the existing debug-server / SQLite API if practical, but evolve it toward app-core endpoints.

The API should support:

- list/select records for the required tabs.
- update records with validation.
- fetch current preview payload for selected shot/screen/frame.
- return readable validation errors.

Keep it local/dev-only for now.

Do not introduce authentication or production server concerns.

## Data editing requirements by tab

### Productions

Editable fields:

- name
- code if present
- description if present
- default fps/render settings if already represented.

### Shots

Editable fields:

- name/title/code if present
- production_id if appropriate
- fps
- duration_frames
- render preset reference if present.

### Screen Instances

Editable fields:

- screen_type
- module_id
- module_schema_version
- owner_actor_id
- device_id
- device_state_id
- theme_id
- theme_mode
- start_frame
- end_frame
- layer_order
- module_data_json
- module_config_json
- module_tokens_override_json
- transform_json

Do not edit legacy normalized Chat tables.

### Actors

Editable fields:

- display name
- short name if present
- avatar asset reference if present
- default device/theme references if present
- metadata_json.

### Themes

Editable fields:

- name
- family if present
- version if present
- tokens_json.

Remember:

- fonts are selected per theme from installed fonts in a future picker.
- for now, editing stored family/style/weight in tokens_json is enough.
- do not implement full font picker yet.

### Devices

Editable fields:

- name
- manufacturer/model/os_family if present
- metrics_json
- frame asset reference if present.

### Device States

Editable fields:

- name if present
- device_id if present
- state_json.

### Media Assets

Editable fields:

- name
- type
- uri
- mime_type
- width/height/duration if present
- metadata_json.

Do not implement full asset import/picker yet.

### Render Presets

Editable fields:

- name
- fps/width/height/format/codec/alpha/output scale if present.
- quality/settings JSON if present.

## Validation

Use existing Zod schemas wherever possible.

Important:

- `core.chat` screen instances must validate `module_data_json` with `ChatModuleDataSchema`.
- `core.chat` screen instances must validate `module_config_json` with `ChatModuleConfigSchema`.
- `module_tokens_override_json` must be valid object JSON.
- theme/device/device_state should validate by reconstructing the full record and using current schemas.

If a table lacks strict schema coverage, at least enforce JSON object validity for JSON fields.

## Do not implement

Do not create or implement:

- Electron shell
- final production editor UX
- final render/export pipeline
- asset manager/import workflow
- icon manager
- font picker
- visual chat editor
- timeline editor beyond frame input/slider
- undo/redo history
- authentication
- multi-user workflows
- custom Instagram module
- legacy Chat normalized editor

This task is the first app shell + autosave + preview panel.

## Scripts

Reuse existing `npm run debug` if it naturally becomes the app shell, or add a new script.

Recommended script names:

```text
npm run app
npm run app:check
npm run app:build
```

If keeping `debug` naming temporarily, document it clearly.

Existing scripts must keep working:

```text
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm run validate:sqlite
npm run remotion:check
npm test
```

## Smoke checks

Add/update a smoke check if practical.

It should verify:

- app API can list core records.
- seeded production/shot/screen instance can be selected.
- at least one editable JSON field rejects malformed JSON.
- a valid autosave/update persists to SQLite.
- preview payload can be re-resolved after save.

Do not add brittle visual screenshot tests.

## Documentation update

Update `PROJECT_STATUS.md` to reflect:

- core app shell exists.
- left panel has tabs for core entities.
- right panel has persistent preview/output.
- validated autosave exists.
- Chat remains the reference module for preview.
- debug UI is now evolving into app shell.
- no Electron/export/final editor exists yet.

Set next recommended task to one of:

```text
Review core app shell workflow visually, then choose: calibration UX polish, font picker, asset picker, screen-instance creation flow, or Electron shell.
```

## Update exchange response

Create this response file:

```text
docs/exchange/responses/0014_core_app_shell_response.md
```

Use this format:

```md
# Codex Response 0014 — Core app shell with data tabs, autosave and preview

## Summary

## Files changed

## Questions / conflicts

## Tests

## Run commands

## Notes
```

## Notes requirements

In `## Notes`, include:

- whether this reused or replaced the previous debug UI.
- which tabs are implemented.
- what autosaves.
- what validation exists.
- what remains raw JSON.
- whether preview is still Chat-only.
- limitations/shortcuts.
- recommended next app step.

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

- Two-panel app shell exists.
- Left panel has tabs for core entities.
- Right panel has persistent preview/output.
- Production/shot/screen instance selection works.
- Frame input/slider exists.
- Autosave exists for simple fields where implemented.
- Autosave exists for JSON fields where implemented.
- Invalid JSON is not persisted.
- Save/dirty/error state is visible.
- Screen Instances tab edits canonical module_data_json/module_config_json/module_tokens_override_json.
- Themes tab edits tokens_json.
- Devices tab edits metrics_json.
- Device States tab edits state_json.
- RenderableNode output is read-only.
- Chat preview still works through existing module/renderable/adapter path.
- Existing validation commands pass.
- npm test passes.
- PROJECT_STATUS.md is updated.
- Response file exists in docs/exchange/responses/.
- No Electron shell, final export pipeline, asset manager, font picker, or legacy Chat normalized editor is added.
