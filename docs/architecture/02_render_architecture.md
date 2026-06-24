# Render architecture

## Flow

```text
Production DB / JSON
  ↓
ShotResolver
  ↓
ScreenInstanceResolver / module host
  ↓
Validated module data + resolved common context
  ↓
VisualModuleRegistry
  ↓
Screen modules
  ↓
Atomic visual components
  ↓
ShotBuilder composition
  ↓
Renderer / export
```

The central operation is:

```text
renderShot(productionId, shotId, frame)
```

It is not `renderChat(chatId)`: chat is only one possible screen type inside a shot.

`ShotResolver` loads the shot and production-scoped resources. The module host selects `module_id` + `module_schema_version`, validates module-owned JSON, resolves actor/device/state/theme-mode/assets/icons/events, and supplies a self-contained module input. The central app may validate module internals but does not interpret them. The registry selects the module without exposing storage concerns; modules produce `RenderableNode`, and renderer adapters preview or export it.

For `core.chat` schema version 1, the canonical content flow is:

```text
module_instances.content_json
  → ChatModuleDataSchema
  → participant/actor/asset resolution
  → message.direction alignment + senderParticipantId identity
  → ResolvedChatScreenProps
```

`module_instances.behavior_json` is the sole per-instance behavior source. `module_instances.animation_json` is the reserved per-instance parameter-animation source. Visual design tokens are resolved from Theme → App → Module Theme Config and are not overridden at app/module instance level. Chat resolution has no `data_ref_json`/conversation/message fallback.

Preview and final render should use the same resolvers, resolved props, module registry, visual modules, and React render adapter. The debug preview shell may fit/scale the renderable and draw optional external overlays such as a device frame, but it must not affect the renderable coordinate system. Rendering must be frame-based and deterministic: the same production data, shot, frame, assets, and configuration must produce the same output, without wall-clock time, hidden mutable state, or live network dependencies.

The preview shell contract is intentionally strict:

- The renderable viewport size is always `RenderableNode.box.width × RenderableNode.box.height`.
- Browser preview zoom is a display-only scale calculated from the available panel size.
- Device chrome, borders, shadows, debug overlays, and preview controls live outside the renderable coordinate system.
- Preview shell borders or overlays must never be included in module layout calculations.
- Export PNG resolution is the device render size multiplied by the selected output scale, currently read from the screen-instance transform scale.
- Debug frame PNGs are written under the production media root when configured, using `renders/frames/<production>_<episode>_<shot>_vNN_fNNNNNN.png`; the browser response also exposes that frame-specific filename for open/save flows. The browser preview header reports both render size and preview zoom.

`src/debug-ui/preview/RenderSurface.tsx` is the browser-only pure preview surface: it receives a `RenderableNode`, display fit, and optional external frame flag, then paints only the scaled renderable plus chrome overlay. `PreviewPanel` owns measurement, headers, controls, and state; `RenderSurface` owns the visual surface. `src/visual/adapters/react/RenderableReactAdapter.tsx` is the shared adapter used by both the debug preview and Remotion. `npm run render:frame` renders the canonical check frame to `out/current-frame.png` so preview/render parity can be checked visually. `npm run validate:preview` checks the preview-fit math so display zoom cannot silently change render dimensions.

Text has an approximate renderer-agnostic measurement mode and a final renderer-assisted mode. Preview and final export must share the final strategy. See `09_foundational_module_contracts.md`.
