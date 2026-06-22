# Data model

## Conceptual hierarchy

```text
Production
 ├─ Resources
 │   ├─ Themes
 │   ├─ Devices
 │   ├─ DeviceStates
 │   ├─ Actors
 │   ├─ Apps
 │   ├─ ModuleThemeConfigs
 │   ├─ Assets
 │   ├─ AnimationPresets
 │   └─ RenderPresets
 │
 ├─ Data
 │   ├─ Legacy conversations/messages (deprecated for Chat runtime)
 │   ├─ Notifications
 │   ├─ Calls
 │   └─ CustomDataSources
 │
 └─ Episodes
     └─ Shots
         └─ ScreenInstances
             ├─ App-linked versioned module data/config/token overrides
             └─ ScreenEvents
```

## Main entities

- `productions`: root scope and owner of resources, data, and episodes.
- `episodes`: editorial containers inside a production; shots hang from episodes.
- `shots`: device-screen action sequences, with duration, frame rate, output settings, and optional render preset. They belong to an episode and do not define placement in an external video plate.
- `apps`: reusable app identity, icon, branding, and app-level token defaults inherited by screens.
- `module_theme_configs`: reusable screen/module design defaults scoped by production, theme, app, module, and module schema version.
- `screen_instances`: runtime containers for versioned screen modules. Each supplies `app_id`, `module_id`, `module_schema_version`, owner/device/state/theme/mode context, timing, `module_data_json`, `module_config_json`, `module_tokens_override_json`, and transform.
- `screen_events`: frame-timed changes within a screen instance, such as a notification appearing or a message being sent.
- `themes`: style tokens and visual defaults; suitable for reusable style packs.
- `devices`: device identity and physical/display metrics; suitable for device packs.
- `device_states`: reusable runtime states such as battery, signal, time, lock state, and orientation.
- `actors`: reusable character/profile identity, contact data, avatars, and app-specific handles.
- `media_assets`: production-owned images, video, audio, fonts, and other referenced files.
- `animation_presets`: reusable timing, easing, and transition configuration.
- `render_presets`: output dimensions, frame rate, codec/export, color, and quality settings.
- `conversations` / `messages`: deprecated compatibility structures still present in the initial SQLite schema; canonical Chat runtime content now lives in `screen_instances.module_data_json`.
- `notifications`: notification content, source app, owner, payload, and timing metadata.
- `calls`: incoming or active call data, participants, state, and timing metadata.
- `data_sources`: custom or imported datasets referenced by screen instances.

## SQL and JSON boundary

SQL stores stable identity, ownership, foreign keys, ordering, and queryable relationships. JSON stores flexible visual props, theme tokens, app tokens, module tokens, device metrics, event payloads, module configuration, transforms, and overrides. Frequently queried or integrity-critical fields belong in SQL; module-specific or evolving configuration belongs in JSON.

The active visual-token inheritance model is:

```text
Theme
  → App
  → Screen / Module
  → Screen Instance
  → selected render mode
```

App and screen/module layers may store mode-aware light/dark color values. They remain reusable defaults until a shot/screen render context selects one mode. A screen instance may override one or both modes locally, but shot-specific content remains in `module_data_json`.

A shot is not tied to one chat or one device. It may contain any number of screen instances sequentially or in overlapping layers, but its output space is the device screen—not an UHD/video plate. See `09_foundational_module_contracts.md`; debug/editor tooling must edit module-owned JSON rather than deprecated Chat tables.
