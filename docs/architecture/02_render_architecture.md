# Render architecture

## Flow

```text
Production DB / JSON
  ↓
ShotResolver
  ↓
ScreenInstanceResolver
  ↓
ResolvedProps
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

`ShotResolver` loads the shot and production-scoped resources. `ScreenInstanceResolver` combines each instance's template, actor, device, theme, data reference, events, timing, transform, props, and overrides into self-contained `ResolvedProps`. The registry selects the visual module without exposing storage concerns to it. Screen modules compose atomic components, and `ShotBuilder` places their renderables into the final frame. The renderer then previews or exports that composed frame.

Preview and final render should use the same resolvers, resolved props, module registry, and visual modules. Rendering must be frame-based and deterministic: the same production data, shot, frame, assets, and configuration must produce the same output, without wall-clock time, hidden mutable state, or live network dependencies.
