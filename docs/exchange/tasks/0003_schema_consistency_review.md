# Codex Task 0003 — Schema consistency review

## Goal

Review the initial schema documentation and example JSON files created in task 0002.

This task is a consistency and architecture review only. Do not implement application code yet.

The objective is to catch naming inconsistencies, unclear relationships, schema/example mismatches, and any places where the architecture may have drifted before implementing TypeScript/Zod schemas.

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

Also review these example files:

```text
docs/examples/production_minimal.json
docs/examples/shot_lock_to_chat.json
docs/examples/theme_ios_light.json
docs/examples/device_iphone_generic.json
docs/examples/resolved_props_chat_screen.json
docs/examples/resolved_props_message_bubble.json
```

## Core accepted architecture

The review must preserve these accepted decisions:

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

## Review checklist

### 1. Naming consistency

Verify consistent usage of:

```text
production_id
shot_id
screen_template_id
screen_instance_id
owner_actor_id
sender_actor_id
target_actor_id
device_id
device_state_id
theme_id
app_id
media_asset_id
animation_preset_id
render_preset_id
conversation_id
message_id
notification_id
call_id
data_source_id
```

Check that snake_case is used for database-style fields in schema documentation.

Check that camelCase is used in resolved props examples consumed by visual modules.

### 2. Entity relationships

Verify that relationships are clear and consistent:

- productions contain resources, data and shots.
- shots contain screen_instances.
- screen_instances reference screen_templates, owner actors, devices, themes, device states and data references.
- screen_events belong to screen_instances.
- conversations contain messages.
- notifications reference apps and actors where appropriate.
- actors may have default devices and themes.
- devices do not contain actor-specific content.
- themes do not contain shot-specific timing or message content.

### 3. SQL vs JSON separation

Verify that stable relational fields are documented as stable fields.

Verify that flexible fields are documented as JSON fields.

Flag any place where visual detail is incorrectly modeled as stable relational data.

Flag any place where essential relationships are hidden only inside JSON when they should be stable fields.

### 4. Screen instance model

Verify that `screen_instances` remains the core bridge between shots and visual modules.

Required stable fields:

```text
id
shot_id
screen_template_id
screen_type
owner_actor_id
device_id
device_state_id
theme_id
start_frame
end_frame
layer_order
data_ref_json
transform_json
props_json
transition_in_json
transition_out_json
```

Check that:

- `screen_type` is used as a stable discriminator.
- `data_ref_json` points to narrative/data sources.
- `props_json` is template-specific configuration.
- `transform_json` positions/transforms the screen inside the shot.
- no detailed drawing logic is stored in `screen_instances`.

### 5. Event model

Verify that `screen_events` can represent:

- notification appears
- unlock gesture
- incoming call accepted
- message starts write-on
- scroll moves
- keyboard appears
- app transition

Required stable fields:

```text
id
screen_instance_id
event_type
start_frame
duration_frames
target_id
animation_preset_id
payload_json
```

### 6. Resolved props examples

Review:

```text
docs/examples/resolved_props_chat_screen.json
docs/examples/resolved_props_message_bubble.json
```

Check that they are truly resolved module input, not raw database objects.

They should not require database access at render time.

Check that they include enough information for the visual modules to render using only:

```text
resolvedProps + frame
```

### 7. Example coherence

Verify that IDs and references in examples are coherent.

Examples do not need to include every full related object, but references should be clear and plausible.

Check that:

- `production_minimal.json` is minimal but structurally valid.
- `shot_lock_to_chat.json` clearly shows lock screen → notification → unlock → chat.
- `theme_ios_light.json` contains useful tokens.
- `device_iphone_generic.json` contains useful metrics.
- resolved props examples are aligned with the visual module architecture.

## What to change

You may edit documentation and example JSON files to fix inconsistencies.

You may add small clarifying notes to architecture docs if needed.

Do not change accepted decisions D001–D009 unless there is a real conflict. If there is a conflict, stop and create an Architecture Question instead.

## Do not implement

Do not create or modify:

- application code
- Electron app
- Remotion app
- renderer code
- SQLite migrations
- TypeScript types
- Zod schemas
- package dependencies
- build configuration

This task is review and documentation cleanup only.

## Update `PROJECT_STATUS.md`

Update `PROJECT_STATUS.md` to reflect that the initial data schema documentation has been reviewed for consistency.

Set next recommended task to:

```text
Implement TypeScript/Zod schemas for the documented data model in a small scoped task.
```

## Update exchange response

Create this response file:

```text
docs/exchange/responses/0003_schema_consistency_review_response.md
```

Use this format:

```md
# Codex Response 0003 — Schema consistency review

## Summary

## Files changed

## Questions / conflicts

## Tests

## Notes
```

## Acceptance criteria

- Schema docs and examples have been reviewed for consistency.
- Any documentation/example corrections are made.
- No application code is added.
- No package dependencies are added.
- `PROJECT_STATUS.md` is updated.
- Response file exists in `docs/exchange/responses/`.
- Any architecture conflict is reported as an Architecture Question rather than silently resolved.
