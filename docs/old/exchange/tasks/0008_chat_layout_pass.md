# Codex Task 0008 — Renderer-agnostic ChatScreen layout pass

## Goal

Implement a minimal renderer-agnostic layout pass for `ChatScreen` and `MessageBubble` using the clarified visual tokens from task 0007.

This task should make the abstract renderable tree more useful by adding plausible layout boxes and positions, while still staying renderer-agnostic.

The immediate next goal after this task is a Remotion proof of concept, so keep this task focused and avoid overengineering.

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
docs/exchange/responses/0006_visual_module_stubs_response.md
docs/exchange/responses/0007_visual_tokens_layout_contract_response.md
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

## Typing/write-on clarification

For now, keep the existing simple deterministic write-on behavior.

However, document that the architecture should support a future advanced typing plan for hesitation/correction behavior, for example:

```text
type → pause → delete → type again
```

This advanced mode should live in flexible JSON, likely inside:

```text
messages.animation_override_json
```

or a future animation/text-reveal preset structure.

Do not implement typing actions in this task.

The key boundary is:

```text
resolver computes visibleText / cursorState / typing metadata
visual modules render what they receive
```

`MessageBubbleModule` should not need to know whether `visibleText` came from a simple write-on or a future typing-actions plan.

If useful, add a short note to the architecture docs or decisions log:

```text
Message text reveal supports simple write-on now and may support optional typing actions later through JSON configuration.
```

Do not add rigid SQL columns for typing actions.

## Important boundary

This is not a renderer task.

Correct:

```text
ResolvedChatScreenProps
  ↓
ChatScreen layout pass
  ↓
RenderableNode tree with approximate boxes
```

Incorrect:

```text
ResolvedChatScreenProps
  ↓
React/Remotion/DOM/Canvas/SVG drawing
```

Do not implement Remotion in this task. The next task will likely be a Remotion proof of concept.

## Scope

Implement only:

1. A renderer-agnostic layout helper/pass for ChatScreen.
2. A renderer-agnostic layout helper/pass for MessageBubble.
3. Use tokens from resolved props for:
   - screen gutter
   - header height
   - status bar area
   - message spacing
   - message group spacing
   - maximum bubble width ratio
   - avatar size
   - avatar gap
   - bubble padding
   - line height
4. Improve renderable node `box` values for:
   - chat screen
   - status bar
   - chat header
   - message bubbles
   - avatars where present
5. Keep deterministic approximate text measurement isolated.
6. Add validation/tests for the layout output.
7. Update `PROJECT_STATUS.md`.
8. Create the Codex response file for this task.

## Do not implement

Do not create or implement:

- Remotion app
- React components
- Electron app
- Canvas drawing
- SVG output
- DOM rendering
- renderer/compositor backend
- image/video export
- SQLite database
- migrations
- persistence layer
- UI
- asset management pipeline
- advanced typing actions engine

This task is layout math and renderable descriptions only.

## Suggested file structure

Use a structure similar to this unless the repo already has a better compatible structure:

```text
src/
  visual/
    layout/
      types.ts
      textMeasurement.ts
      layoutMessageBubble.ts
      layoutChatScreen.ts
      index.ts
```

If current visual module files are better places for this, keep the structure simple and explain the choice in the response.

## Layout model requirements

The layout should be deterministic and based on resolved props.

At minimum, compute:

### Chat screen

- root box from viewport/device metrics.
- status bar box.
- header box below status bar if enabled.
- message list area below header.
- screen gutters from tokens.
- basic vertical stack for messages.

### Message bubble

- direction:
  - `sent` aligns right.
  - `received` aligns left.
  - `system` centers if present.
- max width from bubble max width ratio and available message area.
- width from approximate text measurement, clamped to max width.
- height from approximate wrapped line count, padding and line height.
- avatar box for received messages if avatar is enabled/present.
- bubble box adjusted for avatar gap where relevant.
- bubble tail metadata if token exists, but no real path drawing yet.

### Text measurement

Implement a small isolated approximate measurement helper.

It may use:

```text
average_glyph_width = font_size * 0.52
line_count = wrapped text length / max chars per line
height = line_count * line_height + padding_y * 2
```

or another simple deterministic approximation.

Do not attempt real font shaping, grapheme shaping, DOM measurement, canvas measurement or renderer-specific measurement in this task.

Document approximation in metadata and/or comments.

## Scroll/overflow policy

Implement only a minimal policy:

- messages stack top-to-bottom in the message list area.
- if messages overflow, either:
  - keep positions and add overflow metadata, or
  - apply a simple scroll offset to keep the latest visible message in view.

Choose the simplest deterministic policy.

Document the chosen policy in the response.

Do not implement interactive scrolling.

## Visual module requirements

Update current modules so that their renderable nodes use the layout pass.

`ChatScreenModule` should compose children with improved boxes.

`MessageBubbleModule` should use computed bubble/text/avatar boxes.

Avoid hardcoded layout constants when a resolved token exists.

Defensive fallbacks are allowed for malformed inputs but should not be used for validated fixtures.

## Validation requirements

Add or update visual validation so it checks:

- ChatScreen root has a box.
- StatusBar has a box.
- ChatHeader has a box.
- MessageBubble nodes have boxes.
- Avatar nodes have boxes when present.
- Sent/received alignment is plausible.
- Message bubbles are inside the message list horizontal bounds.
- Message bubbles stack deterministically.
- Boxes are deterministic for identical input.
- The renderable tree validates with the existing renderable Zod schema.

All existing validation must still pass:

```text
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm test
```

## Optional debug output

If useful, add a command or console output that prints a compact layout summary for the example chat screen.

Example:

```text
chat_screen 0,0 1179x2556
  status_bar 0,0 1179x54
  chat_header 0,54 1179x96
  message_bubble received 64,210 520x96
  message_bubble sent 580,322 535x96
```

Keep this debug output short.

## Documentation update

Update `PROJECT_STATUS.md` to reflect:

- visual token/layout contract exists.
- renderer-agnostic visual modules exist.
- renderer-agnostic ChatScreen layout pass exists.
- MessageBubble layout uses resolved tokens and isolated approximate measurement.
- no renderer/UI/SQLite implementation exists yet.

Set next recommended task to:

```text
Create a minimal Remotion proof of concept that renders the resolved ChatScreen renderable tree visually, without replacing the renderer-agnostic architecture.
```

## Update exchange response

Create this response file:

```text
docs/exchange/responses/0008_chat_layout_pass_response.md
```

Use this format:

```md
# Codex Response 0008 — Renderer-agnostic ChatScreen layout pass

## Summary

## Files changed

## Questions / conflicts

## Tests

## Notes
```

## Notes requirements

In `## Notes`, include:

- chosen scroll/overflow policy.
- current text measurement approximation.
- any layout values still approximate.
- whether the future typing-actions note was added and where.
- any token/schema gaps discovered.

## Architecture Question rule

If you find a conflict between:

- architecture docs
- `08_visual_tokens_layout_contract.md`
- current schemas
- current resolver implementation
- current visual modules
- fixtures/examples
- accepted decisions D001–D014

do not silently invent a new architecture.

Instead, stop and create an Architecture Question in the response file.

## Acceptance criteria

- Renderer-agnostic layout helper/pass exists.
- ChatScreen renderable tree has improved boxes.
- MessageBubble renderable nodes have improved boxes.
- Sent/received alignment is computed.
- Approximate text measurement is isolated.
- Existing resolved tokens are used where practical.
- Advanced typing-actions support is documented only as a future flexible JSON capability, not implemented.
- `npm run validate:examples` passes.
- `npm run validate:resolver` passes.
- `npm run validate:visual` passes.
- `npm test` passes.
- `PROJECT_STATUS.md` is updated.
- Response file exists in `docs/exchange/responses/`.
- No UI, renderer backend, React, Remotion, Electron, Canvas, SVG, SQLite, migrations or export code is added.
