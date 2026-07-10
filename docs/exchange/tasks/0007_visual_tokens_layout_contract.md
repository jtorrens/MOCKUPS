# Codex Task 0007 — Visual tokens and layout contract review

## Goal

Review the missing visual/layout tokens discovered in task 0006 and turn them into a clear contract across:

```text
theme.tokens_json
device.metrics_json
screen_instance.props_json
resolved props
visual module renderable metadata
```

This task should reduce reliance on hardcoded stub constants and clarify where each visual/layout value belongs.

Do not implement React, Remotion, Electron, Canvas drawing, real renderer backend, SQLite persistence, migrations, UI, or export pipeline yet.

This is primarily documentation + schema/example refinement. Small TypeScript/Zod updates are allowed only where needed to keep existing validation aligned with the refined token contract.

## Current context

Read these files first:

```text
docs/architecture/00_project_vision.md
docs/architecture/01_data_model.md
docs/architecture/02_render_architecture.md
docs/architecture/03_visual_modules.md
docs/architecture/04_shot_builder.md
docs/architecture/05_decisions_log.md
docs/architecture/06_codex_workflow.md
docs/architecture/07_initial_data_schema.md
PROJECT_STATUS.md
```

Review recent task responses:

```text
docs/exchange/responses/0005_in_memory_repository_resolver_response.md
docs/exchange/responses/0006_visual_module_stubs_response.md
```

Review current implementation:

```text
src/domain/schemas/
src/domain/repository/
src/domain/resolvers/
src/visual/
src/domain/validation/
src/visual/validation/
```

Review current fixtures/examples:

```text
docs/examples/production_minimal.json
docs/examples/shot_chat.json
docs/examples/theme_ios_light.json
docs/examples/device_iphone_generic.json
docs/examples/resolved_props_chat_screen.json
docs/examples/resolved_props_message_bubble.json
```

## Important architecture constraints

Preserve these accepted decisions:

- Production is the root entity.
- Shot is the central render unit.
- A shot contains one or more screen instances.
- Chat is only one screen type.
- SQL stores stable relationships.
- JSON stores flexible visual/configuration data.
- Visual modules do not access the database directly.
- Resolvers create resolved props.
- ShotBuilder composes screens but does not draw them.
- Modules receive props + frame and return renderables.
- Renderer should be frame-based and deterministic.

## Background from task 0006

Task 0006 intentionally used approximate layout and exposed underdefined tokens/fields:

```text
header height
screen gutter
message-group spacing
maximum bubble width
avatar gap / placement
bubble-tail geometry
status-bar icon sizing
Wi-Fi state/icon explicitness
renderer-independent text measurement strategy
```

This task should decide where those values belong and update the documentation/examples accordingly.

## Storage clarification

The final storage model remains:

```text
SQLite tables
  + stable relational columns
  + flexible JSON stored as TEXT columns
```

The JSON examples in `docs/examples/` are documentation fixtures and validation examples only. They are not the final app storage format.

## Scope

Implement only:

1. A new architecture document describing the visual token/layout contract.
2. Updates to `07_initial_data_schema.md` if needed.
3. Updates to example JSON fixtures to include the newly clarified tokens/fields.
4. Small Zod schema refinements if needed to validate the updated examples.
5. Small resolver refinements if needed to pass the new tokens into resolved props.
6. Small visual module refinements to use tokens instead of stub constants where practical.
7. Validation updates if required.
8. Update `PROJECT_STATUS.md`.
9. Create the Codex response file for this task.

## Do not implement

Do not create or implement:

- Electron app
- Remotion app
- React components
- Canvas drawing
- SVG output
- DOM rendering
- renderer/compositor backend
- video export
- image export
- SQLite database
- migrations
- persistence layer
- UI
- asset management pipeline

This task is visual contract refinement only.

## Create this file

```text
docs/architecture/08_visual_tokens_layout_contract.md
```

## Required content for `08_visual_tokens_layout_contract.md`

Document the contract for visual/layout values.

The document should answer:

1. Which values belong in `theme.tokens_json`.
2. Which values belong in `device.metrics_json`.
3. Which values belong in `screen_instance.props_json`.
4. Which values belong in `resolved props`.
5. Which values may remain runtime metadata in renderable nodes.
6. Which values are not yet final and should be treated as implementation notes.

## Required placement rules

Use these rules unless there is a strong reason not to.

### Theme tokens

Values that define reusable visual style should live in `theme.tokens_json`.

Examples:

```text
font families
font sizes
line heights
colors
bubble padding
bubble radius
bubble max width ratio
bubble tail style/geometry
bubble shadow
message spacing
message group spacing
avatar size
avatar gap
header height
header background
header separator
notification blur/background/radius
status bar text/icon color
status bar icon scale
cursor style
```

### Device metrics

Values that define the physical/logical screen container should live in `device.metrics_json`.

Examples:

```text
canvas size
screen bounds
viewport
safe area
status bar area
notch/dynamic island
corner radius
pixel ratio
default screen scale
```

### Screen instance props

Values that are specific to one screen instance should live in `screen_instance.props_json`.

Examples:

```text
show_header
show_status_bar
show_keyboard
initial_scroll
message_render_mode
debug_show_bounds
screen-specific overrides
```

### Device state

Values that describe the live state of a device should live in `device_states.state_json`.

Examples:

```text
time
date
battery
signal strength
network label
wifi enabled/icon state
focus mode
device state
```

### Resolved props

Resolved props should contain render-ready values derived from:

```text
theme + device + device state + actor + screen instance props + dataRef + events
```

Visual modules should not need to re-fetch theme/device/actor/data.

### Renderable metadata

Renderable node metadata may include diagnostic information and layout notes, such as:

```text
approximate text measurement
source token path
layout approximation warning
debug timing info
```

Renderable metadata should not be the canonical place for required style values.

## Required decisions to add

Update `docs/architecture/05_decisions_log.md` with new accepted decisions if appropriate.

Suggested decisions:

```text
D010 — Visual style values live in theme tokens unless instance-specific.
D011 — Device geometry lives in device metrics.
D012 — Device live state lives in device state JSON.
D013 — Visual modules receive render-ready token values through resolved props.
D014 — Renderable metadata is diagnostic, not canonical configuration.
```

If the decision numbering is already used, continue with the next available numbers.

## Required example updates

Update examples as needed, especially:

```text
docs/examples/theme_ios_light.json
docs/examples/device_iphone_generic.json
docs/examples/resolved_props_chat_screen.json
docs/examples/resolved_props_message_bubble.json
```

The theme example should include clarified tokens for at least:

```text
screen gutter
header height
message spacing
message group spacing
maximum bubble width
avatar size
avatar gap
bubble tail geometry/style
status bar icon sizing
cursor style
```

The device/device-state examples or fixture dataset should include Wi-Fi state/icon information if it is not already explicit.

Resolved props should pass enough of these values to visual modules so that current stubs can avoid at least some hardcoded constants.

## Required implementation refinements

If practical, update current visual modules to consume the refined tokens from resolved props rather than hardcoded stub constants.

Do not aim for pixel-perfect layout yet.

Do not implement real text measurement. Instead, document the current approximate strategy and keep it isolated.

## Validation requirements

All existing validation must still pass:

```text
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm test
```

If new or updated examples require schema changes, make the smallest meaningful schema updates.

## Missing-token report

In the response file, include a short `## Notes` section with:

- tokens resolved by this task
- tokens still intentionally approximate
- any values still represented by stub constants
- any schema areas that may need future versioning

## Documentation update

Update `PROJECT_STATUS.md` to reflect:

- visual module stubs exist
- the first visual token/layout contract exists
- examples/resolved props have been aligned with the contract
- no renderer/UI/SQLite implementation exists yet

Set next recommended task to:

```text
Implement a renderer-agnostic layout pass for ChatScreen and MessageBubble using the clarified tokens, still without React/Remotion/Electron.
```

## Update exchange response

Create this response file:

```text
docs/exchange/responses/0007_visual_tokens_layout_contract_response.md
```

Use this format:

```md
# Codex Response 0007 — Visual tokens and layout contract review

## Summary

## Files changed

## Questions / conflicts

## Tests

## Notes
```

## Architecture Question rule

If you find a conflict between:

- architecture docs
- `07_initial_data_schema.md`
- current schemas
- current resolver implementation
- current visual modules
- fixtures/examples
- accepted decisions D001–D009

do not silently invent a new architecture.

Instead, stop and create an Architecture Question in the response file.

## Acceptance criteria

- `docs/architecture/08_visual_tokens_layout_contract.md` exists.
- Missing tokens from task 0006 are assigned to the correct canonical location.
- Decisions log is updated if new decisions are added.
- Theme/device/resolved-props examples are updated as needed.
- Current schemas/resolvers/modules are minimally refined if needed.
- Visual modules use clarified tokens where practical.
- `npm run validate:examples` passes.
- `npm run validate:resolver` passes.
- `npm run validate:visual` passes.
- `npm test` passes.
- `PROJECT_STATUS.md` is updated.
- Response file exists in `docs/exchange/responses/`.
- No UI, renderer backend, React, Remotion, Electron, Canvas, SVG, SQLite, migrations or export code is added.
