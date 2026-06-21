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
screen_instance.module_data_json
  → ChatModuleDataSchema
  → participant/actor/asset resolution
  → senderParticipantId direction
  → ResolvedChatScreenProps
```

`module_config_json` is the sole instance behavior source and `module_tokens_override_json` is the local visual-override source. Chat resolution has no `data_ref_json`/conversation/message fallback.

Preview and final render should use the same resolvers, resolved props, module registry, and visual modules. Rendering must be frame-based and deterministic: the same production data, shot, frame, assets, and configuration must produce the same output, without wall-clock time, hidden mutable state, or live network dependencies.

Text has an approximate renderer-agnostic measurement mode and a final renderer-assisted mode. Preview and final export must share the final strategy. See `09_foundational_module_contracts.md`.
