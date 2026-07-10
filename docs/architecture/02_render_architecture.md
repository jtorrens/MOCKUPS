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
  → actor/asset resolution
  → message.direction alignment + actorId identity
  → ResolvedChatScreenProps
```

`module_instances.behavior_json` is the sole per-instance behavior source. `module_instances.animation_json` is the reserved per-instance parameter-animation source. Visual design tokens are resolved from Theme → App → Module Theme Config and are not overridden at app/module instance level. Chat resolution has no `data_ref_json`/conversation/message fallback.

Preview and final render should use the same resolver contracts and final web
HTML render path. The old React/Vite/Remotion debug route has been removed from
this repository. Future screen/module render work must be recreated through the
desktop component route instead of restoring those legacy modules.

Rendering must be frame-based and deterministic: the same production data, shot,
frame, assets, and configuration must produce the same output, without wall-clock
time, hidden mutable state, or live network dependencies.

The preview shell contract is intentionally strict:

- The renderable viewport size is always `RenderableNode.box.width × RenderableNode.box.height`.
- Browser preview zoom is a display-only scale calculated from the available panel size.
- Device chrome, borders, shadows, debug overlays, and preview controls live outside the renderable coordinate system.
- Preview shell borders or overlays must never be included in module layout calculations.
- Export PNG resolution will be the device render size multiplied by the selected output scale.
- Debug frame artifacts, when reintroduced, must be produced from the same final web render path used by the desktop preview.

The desktop preview route is:

```text
component contract/resolver
  -> component renderable module
  -> common preview helpers
  -> DesktopRenderableHtmlAdapter
  -> final web HTML
```

`DesktopRenderableHtmlAdapter` may use React server rendering as an internal
HTML-generation detail, but it only paints generic resolved desktop primitives.
It must not import component resolvers, theme/database records, legacy visual
modules, or the removed React runtime adapter.

Text has an approximate renderer-agnostic measurement mode and a final renderer-assisted mode. Preview and final export must share the final strategy. See `09_foundational_module_contracts.md`.
