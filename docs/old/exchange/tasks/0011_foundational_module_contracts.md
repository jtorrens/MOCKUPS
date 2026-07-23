# Codex Task 0011 — Foundational module contracts and debug UI boundaries

## Goal

Update the MOCKUPS architecture and schemas to reflect the foundational decisions made after the first Remotion/SQLite passes.

This task should document and lightly refactor the project around a more encapsulated screen-module architecture before implementing the debug calibration UI.

Do not implement the debug UI yet.

The goal is to formalize:

```text
shot = device-screen action sequence
screen_instance = module runtime container
screen module = owner of its internal data schema and visual behavior
theme/device/context = supplied by the shot/screen instance
renderer = adapter over RenderableNode output
```

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
PROJECT_STATUS.md
```

Review recent task responses:

```text
docs/exchange/responses/0008_chat_layout_pass_response.md
docs/exchange/responses/0009A_remotion_poc_response.md
docs/exchange/responses/0010_sqlite_persistence_response.md
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

## Important architecture correction

A `Shot` in MOCKUPS does not mean a phone composited inside an external UHD/video plate.

A `Shot` means:

```text
the timeline/sequence of actions shown by a device screen
```

External placement of the device render into a real video plate is outside MOCKUPS and will be handled later in compositing software such as AE, Fusion, Resolve, Nuke, etc.

MOCKUPS should render the device-screen animation at the device render resolution, optionally with a final output scale in a render preset.

## High-level decisions to document

Update `docs/architecture/05_decisions_log.md` with accepted decisions covering the following points. Continue numbering from the current last decision.

### A. Logical units and device design space

- Theme tokens use logical units, not physical pixels.
- Devices define a `designSpace` that maps logical units to internal render pixels.
- Shot defines device-screen timeline/actions, not external video placement.
- RenderPreset may define output scale/format/codec/alpha but not external video placement.

### B. Asset/media taxonomy

- Reusable production media and one-off content media share asset references but differ by usage/scope.
- OS/app iconography is token-based and theme/OS/mode-specific, separate from user/content media.
- Heavy media stays external and is referenced by URI.
- Small SVG icons may later support inline storage only if useful.
- Media inside components is controlled by a media window plus asset transform, not only cover/contain fit.
- Media window dimensions and offsets use logical units; media scale is a ratio.

### C. Text measurement and fonts

- Text layout has approximate renderer-agnostic and final renderer-assisted modes.
- Preview and final export must use the same final text measurement strategy.
- Manual line breaks are preserved before automatic wrapping.
- Text reveal/write-on should operate on grapheme clusters where possible.
- Fonts are selected per theme from installed system fonts through a font picker.
- Themes store selected font family/style/weight; the project assumes required fonts are installed on Mac and PC.
- Do not introduce a production font whitelist/table unless there is a later explicit requirement.

### D. Module-owned animation behavior

- Modules own their visual behavior and animation interpretation.
- Resolvers provide modules with resolved data, timings, theme/device tokens, events and module config.
- Module-specific persistent rules live in JSON config fields, not new global SQL columns.
- Visual modules must remain pure/deterministic: props + frame/context → RenderableNode.
- Visual modules must not access DB/repositories directly.

### E. Screen module encapsulation

- Screen modules own their internal data schema.
- Screen instances store module-specific data/config JSON.
- The central app validates module JSON through the module schema but does not interpret module internals.
- Every screen module implements a stable input/output contract:
  - resolved common context
  - module JSON
  - frame
  - output RenderableNode
- Screen modules are versioned by `module_id` and `module_schema_version`.

### F. Module editors and runtime context

- Module editors are user/device independent.
- Module editors create/edit module data and module config.
- They may use a preview context, but final render context is supplied by shot/screen instance.
- Screen instances provide owner actor, device, device state, theme, theme mode and timings.
- Themes support light/dark modes inside the same theme.
- Modules receive already-resolved/merged theme tokens for the selected mode.

### G. Chat module participants

- Chat module data includes participants.
- Each chat message references a sender participant.
- Participants may reference production actors.
- This supports group chats while keeping chat internals owned by ChatModule.

### H. Module schema versioning

- Screen instances store `module_id` and `module_schema_version`.
- Each module validates its own `module_data_json` and `module_config_json`.
- Module schema versioning is independent from SQLite/app schema versioning.
- Unsupported or missing modules should fail clearly without corrupting data.

### I. Asset/icon resolution for modules

- Modules may reference assets/icons but do not resolve files directly.
- The host/resolver resolves asset IDs and icon tokens before module render.
- Iconography is token-based and theme/mode aware.
- Missing asset behavior is configurable; final render should default to error.

### J. Module data/config/override separation

- `module_data_json` stores shot-specific content edited by the module editor.
- `module_config_json` stores instance-level behavior/configuration.
- `module_tokens_override_json` stores shot/screen-specific visual exceptions.
- Theme tokens remain the canonical reusable design source.

### K. Debug UI boundaries

- Debug UI separates source data by responsibility.
- RenderableNode is calculated output and should not be edited directly.
- Per-shot content editing belongs to the module editor; reusable design editing belongs to the theme editor.

## Create or update architecture docs

Create this new document:

```text
docs/architecture/09_foundational_module_contracts.md
```

It should concisely describe the new architecture model:

```text
Production
  ├─ Themes / Devices / Actors / Assets / Icons
  └─ Shots
      └─ ScreenInstances
          ├─ module_id
          ├─ module_schema_version
          ├─ module_data_json
          ├─ module_config_json
          ├─ module_tokens_override_json
          └─ runtime context refs
```

Also update these existing docs where necessary:

```text
docs/architecture/01_data_model.md
docs/architecture/02_render_architecture.md
docs/architecture/03_visual_modules.md
docs/architecture/04_shot_builder.md
docs/architecture/07_initial_data_schema.md
docs/architecture/08_visual_tokens_layout_contract.md
```

Do not duplicate huge sections. Prefer short updates and cross-references.

## Required screen_instances model update

Update docs and schemas if needed so `screen_instances` clearly supports:

```text
id
shot_id
screen_type
screen_template_id
module_id
module_schema_version
owner_actor_id
device_id
device_state_id
theme_id
theme_mode
start_frame
end_frame
layer_order
module_data_json
module_config_json
module_tokens_override_json
transform_json
```

Legacy fields may remain temporarily if required by current implementation, but document the intended direction.

If there is a migration impact, document it clearly. Do not perform a destructive migration in this task unless trivial and safe.

## Required module contract

Document a screen module interface conceptually similar to:

```ts
type ScreenModuleInput<TData, TConfig> = {
  frame: number;
  fps: number;
  screenInstanceId: string;
  moduleId: string;
  moduleSchemaVersion: number;
  moduleData: TData;
  moduleConfig: TConfig;
  ownerActor?: ResolvedActor;
  device: ResolvedDevice;
  deviceState?: ResolvedDeviceState;
  themeTokens: ResolvedThemeTokens;
  themeMode: 'light' | 'dark';
  assets: ResolvedAssetMap;
  icons: ResolvedIconMap;
  props?: Record<string, unknown>;
};

type ScreenModuleOutput = RenderableNode;
```

The exact implementation can differ, but the docs should express the boundary.

## Required module_data/config separation

Update docs and schemas/examples so Chat data distinguishes:

### module_data_json

Shot-specific content:

```text
participants
header
messages
message media refs
message timings
per-message sender references
```

### module_config_json

Instance behavior/config:

```text
showHeader
showKeyboard
initialScroll
messageGrouping
debug flags
module behavior defaults
```

### module_tokens_override_json

Local visual exceptions:

```text
per-screen gutter override
header size override
bubble style override
other token overrides scoped to this screen instance
```

## Required light/dark theme handling

Update docs/examples as needed to show:

```text
theme base tokens
theme modes.light
theme modes.dark
theme defaultMode
screen_instance.theme_mode or props override
device_state appearance
resolved merged theme tokens
```

Do not implement a full UI switch yet.

## Required designSpace/logical units handling

Update docs/examples as needed to show:

```text
device.metrics_json.designSpace
theme tokens in logical units
scaleToPixels
render/output scale distinction
```

Clarify that render output is normally device render resolution, not UHD plate placement.

## Required media/icon contract

Update docs/examples as needed to show:

- media asset refs.
- icon tokens.
- media window + media transform.
- reusable vs one-off asset usage/scope.
- project-relative URIs where possible.

## Required schemas/SQLite updates

Make small non-destructive schema/Zod updates if needed to support the documented fields.

If the current SQLite schema already has flexible JSON fields that can hold these values, avoid unnecessary churn.

If a new column is clearly needed, add it carefully and update seed/validation.

Do not over-normalize module internals into central SQL tables.

## Required current implementation compatibility

Existing tests must continue to pass.

If current implementation still uses older central chat/conversation/message fixtures, do not perform a huge refactor in this task.

It is acceptable to document the target direction and add compatibility fields/wrappers.

The next tasks will decide whether to refactor current Chat implementation to `module_data_json`.

## Do not implement

Do not create or implement:

- debug UI
- editor UI
- Electron shell
- final render/export pipeline
- custom Instagram module
- full Chat module refactor unless very small and safe
- destructive SQLite migration
- asset manager UI
- full font picker UI
- full module editor UI

This task is architecture/schema alignment only.

## Validation requirements

All existing validation should still pass:

```text
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm run validate:sqlite
npm test
```

If Remotion scripts exist, do not break them.

## Documentation update

Update `PROJECT_STATUS.md` to reflect:

- SQLite persistence exists.
- Remotion POC exists.
- foundational module contracts are documented.
- screen instances are moving toward module-owned data/config/override JSON.
- debug UI has not been implemented yet.

Set next recommended task to:

```text
Create a minimal debug calibration UI for selecting a shot/frame, viewing the Remotion preview, and inspecting/editing module data, module config, theme tokens, device metrics/state and renderable output.
```

## Update exchange response

Create this response file:

```text
docs/exchange/responses/0011_foundational_module_contracts_response.md
```

Use this format:

```md
# Codex Response 0011 — Foundational module contracts and debug UI boundaries

## Summary

## Files changed

## Questions / conflicts

## Tests

## Notes
```

## Notes requirements

In `## Notes`, include:

- whether any schema/Zod/SQLite changes were needed.
- whether current chat implementation remains compatibility-based.
- which fields are target direction vs already used.
- any migration recommendations.
- any remaining ambiguity before debug UI.

## Architecture Question rule

If you find a conflict between:

- architecture docs
- current schemas
- current SQLite schema
- current resolver implementation
- current visual modules/layout
- Remotion integration
- accepted decisions already in the log

do not silently invent a new architecture.

Instead, stop and create an Architecture Question in the response file.

## Acceptance criteria

- `docs/architecture/09_foundational_module_contracts.md` exists.
- Decisions log includes the new foundational decisions.
- Docs clarify that Shot means device-screen action sequence, not external video placement.
- Docs clarify logical units/designSpace/output scale.
- Docs clarify asset/media/icon taxonomy.
- Docs clarify font picker/theme font selection.
- Docs clarify module-owned animation behavior.
- Docs clarify screen module contract and versioning.
- Docs clarify module editor independence from user/device.
- Docs clarify light/dark theme mode handling.
- Docs clarify Chat participants/senderParticipantId for group chats.
- Docs clarify module_data_json vs module_config_json vs module_tokens_override_json.
- Docs clarify debug UI boundaries.
- Existing validation commands still pass.
- `PROJECT_STATUS.md` is updated.
- Response file exists in `docs/exchange/responses/`.
- No debug UI/editor UI/Electron/export pipeline is added.
