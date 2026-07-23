# Codex Task 0009A — Minimal Remotion proof of concept

## Goal

Create a minimal Remotion proof of concept that renders the current resolved ChatScreen/renderable tree visually.

The goal is to see an actual visual output as soon as possible while preserving the renderer-agnostic architecture.

This task should prove this path:

```text
fixture data
  ↓
in-memory repository
  ↓
resolver
  ↓
resolved props
  ↓
renderer-agnostic layout/renderable tree
  ↓
Remotion adapter
  ↓
visible ChatScreen proof of concept
```

Do not replace the renderer-agnostic modules with Remotion-specific logic.

Remotion should be an adapter/view layer over the existing renderable tree, not the new source of truth.

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
docs/architecture/08_visual_tokens_layout_contract.md
PROJECT_STATUS.md
```

Review recent task responses:

```text
docs/exchange/responses/0007_visual_tokens_layout_contract_response.md
docs/exchange/responses/0008_chat_layout_pass_response.md
```

Review current implementation:

```text
src/domain/schemas/
src/domain/repository/
src/domain/resolvers/
src/visual/
src/visual/layout/
src/visual/modules/
src/visual/renderable/
src/visual/validation/
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
- Visual style values live in theme tokens unless instance-specific.
- Device geometry lives in device metrics.
- Device live state lives in device state JSON.
- Visual modules receive render-ready token values through resolved props.
- Renderable metadata is diagnostic, not canonical configuration.

## Important boundary

This task introduces Remotion only as a proof-of-concept renderer adapter.

Correct:

```text
RenderableNode tree
  ↓
RemotionRendererAdapter
  ↓
visual preview
```

Incorrect:

```text
Remotion components fetch DB data
Remotion components resolve theme/device/message data
Remotion components replace the domain resolvers
Remotion components become the canonical visual module layer
```

Remotion components must not query repository/database.

They should receive either:

- a `RenderableNode` tree, or
- data already resolved through existing resolver/visual-module pipeline.

## Scope

Implement only:

1. Minimal Remotion setup.
2. A Remotion composition that renders one example ChatScreen frame/sequence.
3. A renderer adapter that converts `RenderableNode` into React/Remotion elements.
4. A small bridge that uses the current fixture/resolver/visual-module pipeline to produce the renderable tree for the composition.
5. A minimal way to run Remotion preview/studio.
6. A minimal validation/build command if practical.
7. Update `PROJECT_STATUS.md`.
8. Create the Codex response file for this task.

## Do not implement

Do not create or implement:

- Electron app
- full editor UI
- SQLite database
- migrations
- persistence layer
- import/export UI
- final video export pipeline
- asset management pipeline
- advanced animation system
- advanced typography/text measurement
- real phone-device frame
- full phone screen renderer
- Instagram/custom app module

This task is a minimal Remotion proof of concept only.

## Dependency guidance

Add only the dependencies needed for a minimal Remotion project.

Do not add Electron, SQLite, Prisma, Drizzle, full UI frameworks beyond what Remotion requires, or unrelated packages.

If package versions or Remotion setup conventions differ from assumptions, use the current installed package guidance and document choices in the response.

## Suggested file structure

Use a structure similar to this unless a simpler compatible Remotion structure is required:

```text
src/
  remotion/
    Root.tsx
    ChatScreenPreview.tsx
    RemotionRenderableAdapter.tsx
    buildRenderableForFrame.ts
```

If Remotion requires specific entry files, follow the current Remotion convention and explain the final structure.

## Composition requirements

Create a composition for the current chat proof of concept.

Suggested name:

```text
ChatScreenPreview
```

Suggested dimensions may come from the current device/example metrics. If too large for preview, use a scaled display but keep the logical layout based on the existing resolved/device data.

Suggested fps:

```text
25
```

Duration should be long enough to see:

- chat screen active.
- at least one write-on state.
- completed message state.

Use frame values from the current example/resolver tests where possible.

## Data flow requirements

The composition should use the current pipeline.

At render frame:

1. Convert Remotion frame to the relevant shot frame or local frame as needed.
2. Use fixture loader/repository.
3. Use `resolveShot` or equivalent.
4. Extract the active chat screen.
5. Use existing visual modules/layout to produce the renderable tree.
6. Pass the tree to the Remotion adapter.
7. Render visual output.

If performance requires building fixtures outside every component render, keep it simple and deterministic.

Do not duplicate resolver logic inside Remotion components.

## Remotion adapter requirements

Create a renderer adapter for the existing `RenderableNode` tree.

It should support enough node types/styles to see the chat:

- root containers
- status bar
- chat header
- message bubble
- avatar placeholder or image if asset is available
- text nodes
- basic background colors
- border radius
- approximate positioning
- opacity if present
- children recursion

It does not need to be a complete renderer.

It may ignore unsupported metadata, but unsupported node types should be handled gracefully.

## Visual expectations

This proof of concept should prioritize seeing the layout.

It does not need to be pixel-perfect.

It should show:

- screen background
- status bar area
- chat header area
- message bubbles
- sent/received alignment
- visible text/write-on progression
- basic avatar placeholder or asset if available

Avoid spending time on:

- exact iOS clone
- icon perfection
- real SF Symbols
- device chrome
- full typography fidelity
- exact emoji rendering
- advanced blur/shadow

## Validation requirements

Existing validation must continue to pass:

```text
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm test
```

Add a Remotion-related smoke check if practical, for example:

```text
npm run remotion:check
```

or document the preview command clearly if a headless check is not practical.

Do not make tests brittle on visual pixel output.

## Useful scripts

Add minimal scripts if appropriate.

Possible examples:

```json
{
  "remotion:studio": "remotion studio",
  "remotion:preview": "remotion studio",
  "remotion:check": "remotion compositions"
}
```

Use whatever commands are appropriate for the installed Remotion setup.

Keep existing scripts working.

## Documentation update

Update `PROJECT_STATUS.md` to reflect:

- renderer-agnostic layout exists.
- minimal Remotion proof of concept exists.
- Remotion is currently an adapter over renderable trees, not the source of truth.
- no Electron/SQLite/editor/export implementation exists yet.

Set next recommended task to:

```text
Review the Remotion proof of concept visually, then decide whether to refine visual fidelity, add a device frame, or start the desktop/editor shell.
```

## Update exchange response

Create this response file:

```text
docs/exchange/responses/0009A_remotion_poc_response.md
```

Use this format:

```md
# Codex Response 0009A — Minimal Remotion proof of concept

## Summary

## Files changed

## Questions / conflicts

## Tests

## Preview / run commands

## Notes
```

## Notes requirements

In `## Notes`, include:

- whether Remotion is only consuming `RenderableNode` trees.
- any visual limitations.
- any unsupported renderable node fields.
- any shortcuts used to make the POC visible quickly.
- whether a future renderer adapter abstraction should be formalized.

## Architecture Question rule

If you find a conflict between:

- architecture docs
- current schemas
- current resolver implementation
- current visual modules/layout
- Remotion integration requirements
- accepted decisions D001–D014

do not silently invent a new architecture.

Instead, stop and create an Architecture Question in the response file.

## Acceptance criteria

- Minimal Remotion setup exists.
- A `ChatScreenPreview` or equivalent composition exists.
- Current fixture/resolver/layout pipeline feeds the Remotion composition.
- Remotion adapter renders a `RenderableNode` tree.
- Chat screen is visually previewable.
- Existing validation commands still pass.
- `npm test` passes if present.
- Preview/run commands are documented in the response.
- `PROJECT_STATUS.md` is updated.
- Response file exists in `docs/exchange/responses/`.
- No Electron, SQLite, migrations, editor UI, final export pipeline, or persistence layer is added.
