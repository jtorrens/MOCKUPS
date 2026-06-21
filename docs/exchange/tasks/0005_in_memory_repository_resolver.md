# Codex Task 0005 — Minimal in-memory repository and resolver

## Goal

Implement a minimal in-memory repository and resolver layer that converts raw domain records into resolved props for the existing example shot.

This task should validate the core architecture path:

```text
raw domain data / fixtures
  ↓
in-memory repository
  ↓
shot resolver
  ↓
screen-instance resolver
  ↓
resolved props
  ↓
Zod validation
```

Do not implement UI, renderer, Electron, Remotion, SQLite persistence, migrations, visual modules, or export pipeline yet.

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

Review the latest responses:

```text
docs/exchange/responses/0003_schema_consistency_review_response.md
docs/exchange/responses/0004_typescript_zod_schemas_response.md
```

Review current schemas and validation code:

```text
src/domain/schemas/
src/domain/validation/validateExamples.ts
```

Review current fixtures:

```text
docs/examples/production_minimal.json
docs/examples/shot_lock_to_chat.json
docs/examples/theme_ios_light.json
docs/examples/device_iphone_generic.json
docs/examples/resolved_props_chat_screen.json
docs/examples/resolved_props_message_bubble.json
```

## Important architecture constraints

Preserve these decisions:

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

The JSON files in `docs/examples/` are fixtures and contract examples only.

They are not the final project storage format.

The future real storage model remains:

```text
SQLite tables
  + stable relational columns
  + flexible JSON stored as TEXT columns
```

This task may load JSON fixtures directly for development/testing, but do not design the app as file-based JSON storage.

## Important frame-coordinate semantics

Preserve the convention from task 0003:

- Screen-instance placement and transform are shot-relative.
- Screen events are screen-instance-local.
- Message timing is screen-instance-local.
- Module input frames are screen-instance-local.
- ShotBuilder/resolver code is responsible for converting shot frame to each active screen instance local frame.

## Scope

Implement only:

1. Minimal in-memory repository interfaces/classes.
2. A fixture loader for the existing example data.
3. A shot resolver that resolves active screen instances for a given shot frame.
4. A screen-instance resolver that produces resolved props for the example `ChatScreen`.
5. Optional minimal resolver support for the lock-screen instance if needed to keep the example coherent.
6. Validation of resolver output with existing Zod resolved-props schemas.
7. A test or validation script that proves the lock-to-chat fixture can be loaded and the chat screen props can be resolved.
8. Update `PROJECT_STATUS.md`.
9. Create the Codex response file for this task.

## Do not implement

Do not create or implement:

- Electron app
- Remotion app
- React UI
- renderer
- visual modules
- real ShotBuilder renderer/compositor
- SQLite database
- migrations
- persistence layer
- import/export UI
- video export
- asset management pipeline

This task is resolver/data plumbing only.

## Suggested file structure

Use a structure similar to this unless the repo already has a better compatible structure:

```text
src/
  domain/
    repository/
      InMemoryRepository.ts
      types.ts
      fixtureLoader.ts
    resolvers/
      resolveShot.ts
      resolveScreenInstance.ts
      resolveChatScreen.ts
      resolveMessageBubble.ts
      index.ts
    validation/
      validateResolver.ts
```

If you choose a different structure, explain why in the response.

## Repository requirements

Create an in-memory repository capable of returning records by ID for the domain entities needed by the example shot.

At minimum, it should support:

```text
getProduction(id)
getShot(id)
getScreenInstancesForShot(shot_id)
getScreenEventsForInstance(screen_instance_id)
getScreenTemplate(id)
getTheme(id)
getDevice(id)
getDeviceState(id)
getActor(id)
getMediaAsset(id)
getConversation(id)
getConversationParticipants(conversation_id)
getMessagesForConversation(conversation_id)
getNotification(id)
getApp(id)
```

It does not need to be generic or production-ready. It only needs to support the current example fixtures cleanly.

Keep the repository independent from visual modules.

## Fixture loader requirements

The loader should construct a coherent in-memory dataset from the existing example files.

If the existing examples are not sufficient as full domain records, create a small fixture module under `src/domain/repository/fixtures/` or similar that derives/fills minimal missing records from the examples.

Do not silently change architecture. If the examples are structurally insufficient in a way that reflects a schema problem, create an Architecture Question instead.

## Shot resolver requirements

Create a resolver function conceptually similar to:

```ts
resolveShot({
  repository,
  productionId,
  shotId,
  shotFrame
})
```

It should:

1. Load the shot.
2. Find screen instances for the shot.
3. Determine which screen instances are active at `shotFrame`.
4. Convert `shotFrame` to each screen instance local frame:

```text
localFrame = shotFrame - screenInstance.start_frame
```

5. Resolve each active screen instance into a normalized object containing:
   - screen_instance_id
   - screen_type
   - shot_frame
   - local_frame
   - layer_order
   - transform
   - resolved props, when supported

The output does not need to be final renderer output.

## Screen-instance resolver requirements

Create a resolver function conceptually similar to:

```ts
resolveScreenInstance({
  repository,
  screenInstance,
  shotFrame
})
```

It should:

1. Resolve owner actor.
2. Resolve device.
3. Resolve device state.
4. Resolve theme.
5. Resolve screen events for the instance.
6. Resolve `data_ref_json`.
7. Route to a screen-type-specific resolver when available.

For this task, only `chat` must produce fully validated `ResolvedChatScreenProps`.

Other screen types may return a lightweight normalized object, unless lock-screen support is easy and useful.

## Chat screen resolver requirements

Create a resolver function conceptually similar to:

```ts
resolveChatScreen({
  repository,
  screenInstance,
  localFrame
})
```

It should produce data compatible with `ResolvedChatScreenPropsSchema`.

It should resolve:

- frame
- fps if available from the shot/context
- screenInstanceId
- viewport
- theme
- device
- device state
- owner actor
- header
- messages
- events
- props

The resolved props should be render-ready and must not require DB access.

## Message bubble resolver requirements

Create a resolver function conceptually similar to:

```ts
resolveMessageBubble({
  repository,
  message,
  conversation,
  ownerActor,
  theme,
  localFrame
})
```

It should produce data compatible with `ResolvedMessageBubblePropsSchema`.

It should resolve:

- frame
- fps if available
- id
- direction
- text
- visibleText
- actor
- style
- layout
- timing
- animation

The `direction` should be based on whether `message.sender_actor_id` matches the screen owner actor.

```text
sender_actor_id === owner_actor_id → sent
sender_actor_id !== owner_actor_id → received
```

If system messages exist, preserve a `system` direction/type where supported by current schemas.

## Write-on behavior

Implement only minimal deterministic visible text logic.

For example:

- before `write_on_start_frame`: empty or initial state
- during write-on: substring based on local frame progress
- after write-on: full text

Do not implement advanced human typing variation yet.

Keep it deterministic and easy to test.

Unicode support can be simple for now, but avoid breaking obvious emoji strings if the current examples include them.

## Validation script

Create a command similar to:

```text
npm run validate:resolver
```

It should:

1. Load the in-memory fixture dataset.
2. Resolve the lock-to-chat example shot at one or more meaningful shot frames.
3. Ensure that the chat screen is active at a frame where it should be active.
4. Validate `ResolvedChatScreenProps` with Zod.
5. Validate at least one `ResolvedMessageBubbleProps` with Zod.
6. Print a clear success message.
7. Exit non-zero on validation failure.

Update `npm test` to include this validation if appropriate.

Keep `npm run validate:examples` working.

## Test frames

Use meaningful frames based on the documented example.

At minimum test:

- a frame where the lock screen is active
- a frame where the chat screen is active
- a frame during or after message write-on

The exact frames should come from the current fixture data.

## Documentation update

Update `PROJECT_STATUS.md` to reflect:

- TypeScript/Zod schemas exist.
- Example validation exists.
- Minimal in-memory repository/resolver exists.
- Resolver output can validate resolved chat screen and message bubble props.
- No renderer/UI/SQLite implementation exists yet.

Set next recommended task to:

```text
Implement minimal visual module stubs that consume resolved props and return renderer-agnostic renderable descriptions, without React/Remotion/Electron.
```

## Update exchange response

Create this response file:

```text
docs/exchange/responses/0005_in_memory_repository_resolver_response.md
```

Use this format:

```md
# Codex Response 0005 — Minimal in-memory repository and resolver

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
- current Zod schemas
- fixtures/examples
- accepted decisions D001–D009

do not silently invent a new architecture.

Instead, stop and create an Architecture Question in the response file.

## Acceptance criteria

- In-memory repository exists.
- Fixture loader or fixture dataset exists.
- `resolveShot` or equivalent exists.
- `resolveScreenInstance` or equivalent exists.
- `resolveChatScreen` exists.
- `resolveMessageBubble` exists.
- Resolver output validates with existing Zod schemas.
- `npm run validate:examples` still passes.
- `npm run validate:resolver` or equivalent passes.
- `npm test` passes if present.
- `PROJECT_STATUS.md` is updated.
- Response file exists in `docs/exchange/responses/`.
- No UI, renderer, Electron, Remotion, SQLite, migrations, visual modules or export code is added.
