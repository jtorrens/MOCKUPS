# Codex Task 0002 — Initial data schema documentation

## Goal

Define the initial data schema for the `MOCKUPS` project as documentation and example JSON only.

This task should refine the architecture into a practical first schema, but it must not implement application code yet.

The result should make the next task straightforward: implementing TypeScript/Zod schemas and, later, SQLite migrations.

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
PROJECT_STATUS.md
```

Core architecture decisions already accepted:

* Production is the root entity.
* Shot is the central render unit.
* A shot contains one or more screen instances.
* Chat is only one screen type.
* SQL stores stable relationships.
* JSON stores flexible visual/configuration data.
* Visual modules do not access the database directly.
* Resolvers create resolved props.
* ShotBuilder composes screens but does not draw them.
* Modules receive props + frame and return renderables.
* Renderer should be frame-based and deterministic.

## Create these files

```text
docs/architecture/07_initial_data_schema.md
docs/examples/production_minimal.json
docs/examples/shot_lock_to_chat.json
docs/examples/theme_ios_light.json
docs/examples/device_iphone_generic.json
docs/examples/resolved_props_chat_screen.json
docs/examples/resolved_props_message_bubble.json
```

## Do not implement yet

Do not create or modify:

* application code
* Electron app
* Remotion app
* renderer code
* SQLite migrations
* TypeScript types
* Zod schemas
* package dependencies
* build configuration

This task is documentation and example data only.

## Requirements for `07_initial_data_schema.md`

Document a practical first schema for the project.

Use the existing architecture docs as source of truth. If there is a conflict, do not invent a different architecture. Create an Architecture Question instead.

The schema should cover these entities:

```text
productions
shots
screen_templates
screen_instances
screen_events
themes
devices
device_states
actors
apps
media_assets
animation_presets
render_presets
conversations
messages
notifications
calls
data_sources
```

For each entity, include:

* purpose
* important stable fields
* flexible JSON fields
* key relationships
* notes about what should not live in that entity

Use this distinction consistently:

```text
SQL/stable fields:
- IDs
- names
- relationships
- ordering
- frame timings
- core type fields
- references to assets/templates/themes/devices/actors

JSON/flexible fields:
- visual tokens
- device metrics
- module props
- event payloads
- animation parameters
- style overrides
- layout overrides
- template-specific config
```

## Required conceptual hierarchy

Include this hierarchy in the document:

```text
Production
 ├─ Resources
 │   ├─ Themes
 │   ├─ Devices
 │   ├─ DeviceStates
 │   ├─ Actors
 │   ├─ Apps
 │   ├─ MediaAssets
 │   ├─ AnimationPresets
 │   ├─ RenderPresets
 │   └─ ScreenTemplates
 │
 ├─ Data
 │   ├─ Conversations
 │   │   └─ Messages
 │   ├─ Notifications
 │   ├─ Calls
 │   └─ DataSources
 │
 └─ Shots
     └─ ScreenInstances
         └─ ScreenEvents
```

## Required naming conventions

Use these names consistently:

* `production_id`
* `shot_id`
* `screen_template_id`
* `screen_instance_id`
* `owner_actor_id`
* `sender_actor_id`
* `target_actor_id`
* `device_id`
* `device_state_id`
* `theme_id`
* `app_id`
* `media_asset_id`
* `animation_preset_id`
* `render_preset_id`
* `conversation_id`
* `message_id`
* `notification_id`
* `call_id`
* `data_source_id`

Prefer snake_case for database-style fields in schema docs.

For JSON examples, use camelCase if the example represents resolved props consumed by visual modules.

## Required screen instance model

Document `screen_instances` as the core bridge between shots and visual modules.

It should include these stable fields:

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

Explain:

* `screen_type` is a stable discriminator, such as `chat`, `lock_screen`, `notification_stack`, `incoming_call`, `home_screen`, or `custom_app`.
* `data_ref_json` points to the narrative/data source used by the screen.
* `props_json` contains template-specific configuration.
* `transform_json` describes how the screen is positioned or transformed inside the shot.
* `screen_instances` should not contain detailed drawing logic.

## Required event model

Document `screen_events`.

Stable fields:

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

Explain that events represent temporal changes inside a screen, for example:

* notification appears
* unlock gesture
* incoming call accepted
* message starts write-on
* scroll moves
* keyboard appears
* app transition

## Required theme model

Document `themes`.

Stable fields:

```text
id
production_id
name
family
version
tokens_json
```

Explain that `tokens_json` may include:

* font tokens
* bubble tokens
* notification tokens
* status bar tokens
* colors
* spacing
* radii
* shadows
* component defaults

Themes should not contain shot-specific timing or message content.

## Required device model

Document `devices`.

Stable fields:

```text
id
production_id
name
manufacturer
model
os_family
metrics_json
frame_asset_id
```

Explain that `metrics_json` may include:

* canvas size
* screen bounds
* viewport
* safe areas
* status bar height
* notch/dynamic island
* corner radius
* pixel ratio

Devices should not contain actor-specific content or shot-specific transforms.

## Required actor model

Document `actors`.

Stable fields:

```text
id
production_id
display_name
short_name
avatar_asset_id
default_device_id
default_theme_id
metadata_json
```

Explain that actors represent fictional/narrative people or accounts, not necessarily real application users.

## Required message model

Document `messages`.

Stable fields:

```text
id
conversation_id
sort_order
sender_actor_id
message_type
text
start_frame
enter_duration_frames
write_on_enabled
write_on_start_frame
write_on_duration_frames
exit_frame
media_asset_id
style_override_json
animation_override_json
layout_override_json
metadata_json
```

Explain that messages should not store normal theme values like font, color, padding or radius unless they are intentional overrides.

## Required example files

### `docs/examples/production_minimal.json`

Create a minimal example containing:

* one production
* one theme reference
* one device reference
* one actor
* one shot
* one screen instance

This example may be compact.

### `docs/examples/shot_lock_to_chat.json`

Create a richer example showing:

```text
lock screen → notification appears → unlock → chat screen
```

It should include:

* one production ID
* one shot
* at least two screen instances:

  * lock screen
  * chat screen
* at least one screen event for a notification appearing
* one unlock event
* one conversation reference
* one notification reference

The example does not need to include every full related object, but IDs and references must be clear.

### `docs/examples/theme_ios_light.json`

Create an example theme with `tokensJson` or `tokens` covering:

* fonts
* colors
* status bar
* chat bubbles
* notifications
* spacing
* radii
* shadows

### `docs/examples/device_iphone_generic.json`

Create an example device with metrics covering:

* canvas
* screen
* viewport
* safeArea
* statusBar
* cornerRadius
* pixelRatio

### `docs/examples/resolved_props_chat_screen.json`

Create an example of the JSON object a `ChatScreen` visual module would receive after resolver processing.

Use camelCase and include:

* frame
* fps
* screenInstanceId
* viewport
* theme
* device
* ownerActor
* header
* messages
* events
* props

Important: this is resolved props, not raw DB structure.

### `docs/examples/resolved_props_message_bubble.json`

Create an example of the JSON object a `MessageBubble` atomic module would receive.

Use camelCase and include:

* frame
* fps
* id
* direction
* text
* visibleText
* actor
* style
* layout
* timing
* animation

Important: this module should not need database access.

## Documentation style

Keep everything concise and practical.

Prefer implementation-oriented examples over abstract explanation.

Use clear warnings where useful, for example:

```text
Do not store normal bubble colors on messages. Store them in the theme and only use message.style_override_json for exceptions.
```

## Update `PROJECT_STATUS.md`

Update the status to reflect that initial architecture docs exist and initial data schema documentation is now being created.

After completing the task, set next recommended task to:

```text
Review the initial schema docs and examples. If approved, implement TypeScript/Zod schemas in a small scoped task.
```

## Update exchange response

Create a response file:

```text
docs/exchange/responses/0002_initial_data_schema_docs_response.md
```

Use this format:

```md
# Codex Response 0002 — Initial data schema documentation

## Summary

## Files changed

## Questions / conflicts

## Tests

## Notes
```

## Acceptance criteria

* `docs/architecture/07_initial_data_schema.md` exists.
* All requested example JSON files exist.
* No application code is added.
* No package dependencies are added.
* `PROJECT_STATUS.md` is updated.
* Response file exists in `docs/exchange/responses/`.
* Any architecture conflict is reported as an Architecture Question rather than silently resolved.
