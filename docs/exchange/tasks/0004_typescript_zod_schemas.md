# Codex Task 0004 — Implement TypeScript/Zod domain schemas

## Goal

Implement the first TypeScript/Zod domain schemas for the documented MOCKUPS data model.

This is the first controlled implementation task. It should create a minimal TypeScript foundation and Zod schemas that validate the documented entities and example JSON files.

Do not implement UI, renderer, Electron, Remotion, SQLite persistence, migrations, or export pipeline yet.

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

Also review the latest response:

```text
docs/exchange/responses/0003_schema_consistency_review_response.md
```

Review these fixtures/examples:

```text
docs/examples/production_minimal.json
docs/examples/shot_lock_to_chat.json
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

## Important storage clarification

The JSON files in `docs/examples/` are documentation fixtures and validation examples only.

They are not the final project storage format.

The future real storage model should be:

```text
SQLite tables
  + stable relational columns
  + flexible JSON stored as TEXT columns
```

Examples:

- `themes.tokens_json`
- `devices.metrics_json`
- `screen_instances.data_ref_json`
- `screen_instances.transform_json`
- `screen_instances.props_json`
- `screen_events.payload_json`
- `messages.style_override_json`
- `messages.animation_override_json`
- `messages.layout_override_json`

Large media files must not be stored directly in SQLite. They should be referenced through `media_assets.path` or a future asset URI system.

## Important frame-coordinate semantics

Preserve the convention established in task 0003:

- Screen-instance placement and transform are shot-relative.
- Screen events are screen-instance-local.
- Message timing is screen-instance-local.
- Module input frames are screen-instance-local.
- ShotBuilder is responsible for converting shot frame to each active screen instance local frame.

Do not silently change this convention.

## Scope

Implement only:

1. Minimal TypeScript project setup if not already present.
2. Zod schemas for the documented domain entities.
3. Zod schemas for resolved visual-module props examples.
4. A small validation script that validates all example JSON files.
5. Minimal tests or script commands to run validation.
6. Update `PROJECT_STATUS.md`.
7. Create the Codex response file for this task.

## Do not implement

Do not create or implement:

- Electron application
- Remotion application
- React UI
- renderer
- ShotBuilder runtime
- visual modules
- SQLite database
- migrations
- repository layer
- import/export UI
- video export
- asset management pipeline

This task is schema validation only.

## Package/dependency guidance

If there is no package setup yet, create the smallest practical Node/TypeScript setup.

Allowed dependencies:

- `typescript`
- `zod`
- `tsx` or equivalent lightweight runner, if useful for validation scripts

Do not add large frameworks.

Do not add Electron, React, Remotion, Vite, SQLite, Prisma, Drizzle, or any renderer dependency in this task.

## Suggested file structure

Use a structure similar to this unless the repo already has a better compatible structure:

```text
src/
  domain/
    schemas/
      common.ts
      production.ts
      shot.ts
      screen.ts
      theme.ts
      device.ts
      actor.ts
      asset.ts
      animation.ts
      render.ts
      conversation.ts
      notification.ts
      call.ts
      dataSource.ts
      resolvedProps.ts
      index.ts
    validation/
      validateExamples.ts
```

If you choose a different structure, explain why in the response.

## Required schema coverage

Create schemas and inferred TypeScript types for:

```text
Production
Shot
ScreenTemplate
ScreenInstance
ScreenEvent
Theme
Device
DeviceState
Actor
App
MediaAsset
AnimationPreset
RenderPreset
Conversation
ConversationParticipant
Message
Notification
Call
DataSource
```

Also create schemas for resolved visual-module props:

```text
ResolvedChatScreenProps
ResolvedMessageBubbleProps
```

## Common conventions

Use string IDs.

Use integer frame fields with `z.number().int().min(0)` unless the docs indicate otherwise.

Use discriminators where useful, for example:

```text
screen_type
message_type
event_type
media_asset.type
data_source.type
```

Use JSON object schemas for flexible fields. They can be permissive for now, but should still distinguish object vs primitive.

Suggested helper:

```ts
export const JsonObjectSchema = z.record(z.string(), z.unknown());
```

For nullable or optional JSON fields, be explicit.

## Required screen instance schema

The `ScreenInstanceSchema` must include at least:

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

Notes:

- `device_id`, `device_state_id`, and `theme_id` may be nullable/optional if the docs allow resolving defaults from actor/template/production.
- `end_frame` must be greater than `start_frame` if practical to validate with Zod refinement.
- `data_ref_json`, `transform_json`, `props_json`, `transition_in_json`, and `transition_out_json` should be JSON object fields.

## Required screen event schema

The `ScreenEventSchema` must include at least:

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

`payload_json` should be a JSON object field.

## Required message schema

The `MessageSchema` must include at least:

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

Messages should not require ordinary theme values like font, colors, padding or radius.

## Required conversation participant model

Task 0003 clarified that conversation participant membership is a stable SQL relationship, not canonical JSON.

Create a `ConversationParticipantSchema` with fields similar to:

```text
id
conversation_id
actor_id
role
sort_order
metadata_json
```

Exact field names may follow `07_initial_data_schema.md` if already specified there.

## Required resolved props schemas

The resolved props schemas should validate the two example files:

```text
docs/examples/resolved_props_chat_screen.json
docs/examples/resolved_props_message_bubble.json
```

They should represent module input, not raw database records.

They should include enough data for rendering using only:

```text
resolvedProps + frame
```

They should not contain database query requirements.

Be permissive where needed, but keep these schemas meaningful.

## Example validation script

Create a validation script that:

1. Loads the six JSON files from `docs/examples/`.
2. Parses them.
3. Validates each with the appropriate schema.
4. Prints a clear success message.
5. Prints readable errors if validation fails.
6. Exits non-zero on validation failure.

Suggested command name:

```text
npm run validate:examples
```

If the repo does not use npm, choose the smallest compatible alternative and document it.

## Documentation update

Update `PROJECT_STATUS.md` to reflect:

- initial architecture docs exist
- initial data schema docs exist
- schema consistency review completed
- TypeScript/Zod schemas now exist
- example JSON validation command exists

Set next recommended task to:

```text
Implement a minimal in-memory repository/resolver layer that converts raw domain records into resolved props for the existing example shot, without UI or rendering.
```

## Update exchange response

Create this response file:

```text
docs/exchange/responses/0004_typescript_zod_schemas_response.md
```

Use this format:

```md
# Codex Response 0004 — TypeScript/Zod domain schemas

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
- examples
- accepted decisions D001–D009

do not silently invent a new architecture.

Instead, stop and create an Architecture Question in the response file.

## Acceptance criteria

- TypeScript/Zod schemas exist for required domain entities.
- TypeScript/Zod schemas exist for required resolved props examples.
- Example validation script exists.
- `npm run validate:examples` or equivalent passes.
- `PROJECT_STATUS.md` is updated.
- Response file exists in `docs/exchange/responses/`.
- No UI, renderer, Electron, Remotion, SQLite, migrations or export code is added.
- No large framework dependencies are added.
