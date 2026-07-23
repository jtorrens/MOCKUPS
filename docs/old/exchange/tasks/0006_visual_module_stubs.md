# Codex Task 0006 — Renderer-agnostic visual module stubs

## Goal

Implement minimal renderer-agnostic visual module stubs that consume resolved props and return abstract renderable descriptions.

This task should validate the next architecture step:

```text
resolved props
  ↓
visual modules
  ↓
renderer-agnostic renderable tree
```

Do not implement React, Remotion, Electron, Canvas drawing, real video rendering, SQLite persistence, migrations, UI, or export pipeline yet.

The purpose is to start discovering practical visual tokens, layout needs and module boundaries without committing to a final renderer.

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
docs/exchange/responses/0004_typescript_zod_schemas_response.md
docs/exchange/responses/0005_in_memory_repository_resolver_response.md
```

Review current implementation:

```text
src/domain/schemas/
src/domain/repository/
src/domain/resolvers/
src/domain/validation/
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

## Important boundary

Visual modules must not query repository/database.

They must receive resolved props only.

Correct:

```text
MessageBubbleModule.render(resolvedMessageBubbleProps) → RenderableNode
```

Incorrect:

```text
MessageBubbleModule.render(message_id) → fetch message/theme/device from repository
```

## Important storage clarification

The JSON files in `docs/examples/` are fixtures and contracts only.

Future real storage remains:

```text
SQLite tables
  + stable relational columns
  + flexible JSON stored as TEXT columns
```

Do not implement SQLite in this task.

## Important frame-coordinate semantics

Preserve the convention from task 0003:

- Screen-instance placement and transform are shot-relative.
- Screen events are screen-instance-local.
- Message timing is screen-instance-local.
- Module input frames are screen-instance-local.
- Resolvers convert shot frame to local screen-instance frame before modules receive props.

## Scope

Implement only:

1. A small renderer-agnostic renderable description model.
2. A minimal visual module interface.
3. Minimal atomic module stubs:
   - MessageBubble
   - StatusBar
   - Header or ChatHeader
   - Avatar, if useful for MessageBubble/Header
4. A minimal screen module stub:
   - ChatScreen
5. A visual module registry.
6. A validation/demo script that:
   - uses the existing in-memory resolver
   - resolves the example chat screen
   - passes resolved props into visual modules
   - returns an abstract renderable tree
   - validates the shape of the renderable tree
7. Update `PROJECT_STATUS.md`.
8. Create the Codex response file for this task.

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

This task is visual-module contract and abstract renderable output only.

## Suggested file structure

Use a structure similar to this unless the repo already has a better compatible structure:

```text
src/
  visual/
    renderable/
      types.ts
      schema.ts
      helpers.ts
    modules/
      types.ts
      registry.ts
      atomic/
        MessageBubbleModule.ts
        StatusBarModule.ts
        ChatHeaderModule.ts
        AvatarModule.ts
      screens/
        ChatScreenModule.ts
    validation/
      validateVisualModules.ts
```

If you choose a different structure, explain why in the response.

## Renderable description model

Create a simple, renderer-agnostic renderable tree.

It should be explicit enough to test visual composition, but abstract enough that future renderers can translate it to React/Remotion/Canvas/SVG.

Suggested shape:

```ts
type RenderableNode = {
  id: string;
  type: string;
  role?: string;
  frame?: number;
  box?: {
    x: number;
    y: number;
    width: number;
    height: number;
  };
  transform?: {
    x?: number;
    y?: number;
    scale?: number;
    rotation?: number;
    opacity?: number;
  };
  style?: Record<string, unknown>;
  text?: string;
  asset?: {
    type: string;
    uri: string;
  };
  children?: RenderableNode[];
  metadata?: Record<string, unknown>;
};
```

You may adjust this shape if needed, but keep it simple.

Add a Zod schema for the renderable tree.

## Visual module interface

Create a minimal interface similar to:

```ts
export interface VisualModule<InputProps> {
  type: string;
  version: number;
  render(input: InputProps): RenderableNode;
}
```

If a context object is useful, keep it small and renderer-agnostic:

```ts
type VisualModuleContext = {
  frame: number;
  fps: number;
};
```

Do not pass repository/database access to visual modules.

## MessageBubble module requirements

`MessageBubbleModule` should consume `ResolvedMessageBubbleProps`.

It should return a renderable node representing a message bubble.

It should include:

- id
- type: `message_bubble`
- role/direction if useful
- text using `visibleText`
- approximate box/layout fields if available
- style fields derived from resolved props
- avatar child or asset reference if currently present in resolved props
- metadata for timing/animation if useful

Do not implement full text measurement yet.

Use placeholder/approximate width and height if necessary, but make that explicit in metadata.

This module is meant to reveal missing tokens/layout needs, not to be final pixel-perfect layout.

## StatusBar module requirements

`StatusBarModule` should consume a small resolved input derived from the chat screen resolved props.

It should return a renderable node representing status bar content.

It should include:

- time if available
- battery/signal/wifi if available
- color/style tokens if available
- approximate box based on device metrics if available

If the current resolved chat props do not contain enough status bar data, do not redesign the whole schema. Use available data and add a note in response if a token/field appears missing.

## ChatHeader module requirements

`ChatHeaderModule` should consume a small resolved input derived from the chat screen resolved props.

It should return a renderable node representing the chat header.

It should include:

- title/contact name if available
- avatar asset if available
- style tokens if available
- approximate box based on viewport/header props if available

## ChatScreen module requirements

`ChatScreenModule` should consume `ResolvedChatScreenProps`.

It should compose:

- status bar node if enabled/available
- chat header node if enabled/available
- message bubble nodes
- optional event/debug metadata

It should return a renderable node/tree:

```text
chat_screen
 ├─ status_bar
 ├─ chat_header
 └─ message_bubble...
```

The module should not fetch data. It should only use the provided resolved props.

## Visual module registry

Create a registry mapping module names to modules.

At minimum:

```text
chat_screen
message_bubble
status_bar
chat_header
avatar
```

The registry can be simple and static.

## Validation/demo script

Create a command similar to:

```text
npm run validate:visual
```

It should:

1. Use the existing fixture loader/repository.
2. Resolve the example shot at a frame where chat is active.
3. Obtain `ResolvedChatScreenProps`.
4. Render it through `ChatScreenModule`.
5. Validate the returned renderable tree with the renderable Zod schema.
6. Print a clear success message.
7. Exit non-zero on validation failure.

Update `npm test` to include this validation if appropriate.

Keep existing commands working:

```text
npm run validate:examples
npm run validate:resolver
npm test
```

## Missing-token notes

Because this is the first visual module pass, add a small document or response note listing any fields/tokens that appear missing or underdefined.

Prefer adding this to the response file under `## Notes`.

Do not expand the schema aggressively unless needed to make the current modules function.

The goal is discovery, not final pixel-perfect design.

Potential examples of missing or future tokens:

- max bubble width
- group spacing
- avatar gap
- header height
- status bar icon scale
- notification blur
- cursor style
- bubble tail geometry
- text measurement strategy

Only mention items actually encountered or clearly relevant from current implementation.

## Documentation update

Update `PROJECT_STATUS.md` to reflect:

- TypeScript/Zod schemas exist.
- Example validation exists.
- Minimal in-memory repository/resolver exists.
- Minimal renderer-agnostic visual module stubs exist.
- Visual modules can consume resolved chat props and return validated renderable trees.
- No renderer/UI/SQLite implementation exists yet.

Set next recommended task to:

```text
Review visual module output and missing-token notes, then decide whether to implement a renderer-agnostic layout pass or begin a Remotion proof of concept.
```

## Update exchange response

Create this response file:

```text
docs/exchange/responses/0006_visual_module_stubs_response.md
```

Use this format:

```md
# Codex Response 0006 — Renderer-agnostic visual module stubs

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
- current resolver implementation
- fixtures/examples
- accepted decisions D001–D009

do not silently invent a new architecture.

Instead, stop and create an Architecture Question in the response file.

## Acceptance criteria

- Renderer-agnostic `RenderableNode` or equivalent exists.
- Zod schema validates the renderable tree.
- Visual module interface exists.
- MessageBubble module exists.
- StatusBar module exists.
- ChatHeader module exists.
- ChatScreen module exists and composes atomic modules.
- Visual module registry exists.
- `npm run validate:examples` still passes.
- `npm run validate:resolver` still passes.
- `npm run validate:visual` or equivalent passes.
- `npm test` passes if present.
- `PROJECT_STATUS.md` is updated.
- Response file exists in `docs/exchange/responses/`.
- No UI, renderer backend, React, Remotion, Electron, Canvas, SVG, SQLite, migrations or export code is added.
