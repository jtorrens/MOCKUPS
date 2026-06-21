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
 │   ├─ Assets
 │   ├─ AnimationPresets
 │   ├─ RenderPresets
 │   └─ ScreenTemplates
 │
 ├─ Data
 │   ├─ Conversations
 │   │   └─ Messages
 │   ├─ Notifications
 │   ├─ Calls
 │   └─ CustomDataSources
 │
 └─ Shots
     └─ ScreenInstances
         └─ ScreenEvents
```

## Main entities

- `productions`: root scope and owner of resources, data, and shots.
- `shots`: central render units, with duration, frame rate, output settings, and optional render preset.
- `screen_templates`: reusable definitions that select a visual module and provide default configuration.
- `screen_instances`: screens placed in a shot. Each references a template, owner actor, device, theme, `dataRef`, timing, transform, and props/overrides.
- `screen_events`: frame-timed changes within a screen instance, such as a notification appearing or a message being sent.
- `themes`: style tokens and visual defaults; suitable for reusable style packs.
- `devices`: device identity and physical/display metrics; suitable for device packs.
- `device_states`: reusable runtime states such as battery, signal, time, lock state, and orientation.
- `actors`: reusable character/profile identity, contact data, avatars, and app-specific handles.
- `apps`: reusable app identity, icon, branding, and module configuration.
- `media_assets`: production-owned images, video, audio, fonts, and other referenced files.
- `animation_presets`: reusable timing, easing, and transition configuration.
- `render_presets`: output dimensions, frame rate, codec/export, color, and quality settings.
- `conversations`: structured chat data and participants.
- `messages`: ordered conversation items with sender, content, payload, and timing metadata.
- `notifications`: notification content, source app, owner, payload, and timing metadata.
- `calls`: incoming or active call data, participants, state, and timing metadata.
- `data_sources`: custom or imported datasets referenced by screen instances.

## SQL and JSON boundary

SQL stores stable identity, ownership, foreign keys, ordering, and queryable relationships. JSON stores flexible visual props, theme tokens, device metrics, event payloads, module configuration, transforms, and overrides. Frequently queried or integrity-critical fields belong in SQL; module-specific or evolving configuration belongs in JSON.

A shot is not tied to one chat or one device. It may contain any number of screen instances using different devices, actors, templates, and data sources, either sequentially or in overlapping layers.
