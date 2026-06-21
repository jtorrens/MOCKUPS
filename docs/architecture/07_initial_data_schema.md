# Initial data schema

This is the first persistence-oriented schema for MOCKUPS. It is a design target for later TypeScript/Zod schemas and SQLite migrations, not an implementation.

## Storage boundary

SQL/stable fields hold IDs, names, relationships, ordering, frame timings, core type discriminators, and references to assets, templates, themes, devices, and actors. JSON/flexible fields hold visual tokens, device metrics, module props, event payloads, animation parameters, style/layout overrides, and template-specific configuration.

JSON column names use a `_json` suffix below. Resolvers parse these values and emit camelCase resolved props for visual modules.

## Conceptual hierarchy

```text
Production
 ├─ Resources
 │   ├─ Themes
 │   ├─ Devices
 │   ├─ DeviceStates
 │   ├─ Actors
 │   ├─ Apps
 │   ├─ MediaAssets
 │   ├─ AnimationPresets
 │   ├─ RenderPresets
 │   └─ ScreenTemplates
 │
 ├─ Data
 │   ├─ Conversations
 │   │   └─ Messages
 │   ├─ Notifications
 │   ├─ Calls
 │   └─ DataSources
 │
 └─ Shots
     └─ ScreenInstances
         └─ ScreenEvents
```

## Entity schemas

### `productions`

- Purpose: root scope for all reusable resources, narrative data, and shots.
- SQL/stable fields: `id`, `name`, `slug`, `created_at`, `updated_at`.
- JSON/flexible fields: `settings_json`, `metadata_json`.
- Relationships: owns every production-scoped entity.
- Must not contain: individual screen props, message content, or shot timing.

### `shots`

- Purpose: central unit requested for preview and final rendering.
- SQL/stable fields: `id`, `production_id`, `name`, `sort_order`, `duration_frames`, `fps`, `render_preset_id`.
- JSON/flexible fields: `canvas_json`, `metadata_json`.
- Relationships: belongs to one production and optionally one render preset; owns many screen instances.
- Must not contain: a single mandatory chat/device reference or detailed UI drawing rules.

### `screen_templates`

- Purpose: reusable mapping from a screen type to a visual module and its defaults.
- SQL/stable fields: `id`, `production_id`, `name`, `screen_type`, `module_key`, `version`.
- JSON/flexible fields: `default_props_json`, `config_json`.
- Relationships: belongs to a production; referenced by screen instances.
- Must not contain: shot timing, narrative records, or database access logic.

### `screen_instances`

- Purpose: core bridge between shots and visual modules; places a configured screen in a shot.
- SQL/stable fields: `id`, `shot_id`, `screen_template_id`, `screen_type`, `owner_actor_id`, `device_id`, `device_state_id`, `theme_id`, `start_frame`, `end_frame`, `layer_order`.
- JSON/flexible fields: `data_ref_json`, `transform_json`, `props_json`, `transition_in_json`, `transition_out_json`.
- Relationships: belongs to a shot and references a template, owner actor, device, optional device state, and theme; owns screen events.
- Must not contain: detailed drawing logic or copied theme/device records.

`screen_type` is a stable discriminator such as `chat`, `lock_screen`, `notification_stack`, `incoming_call`, `in_call`, `home_screen`, or `custom_app`. `data_ref_json` identifies the narrative source, for example `{ "type": "conversation", "conversation_id": "conversation_001" }`. `props_json` contains template-specific configuration. `transform_json` positions or transforms the rendered screen inside the shot. Transition JSON holds instance-level entrance and exit configuration.

### `screen_events`

- Purpose: temporal changes inside a screen instance.
- SQL/stable fields: `id`, `screen_instance_id`, `event_type`, `start_frame`, `duration_frames`, `target_id`, `animation_preset_id`.
- JSON/flexible fields: `payload_json`.
- Relationships: belongs to one screen instance and may reference an animation preset; `target_id` identifies the affected narrative or visual item within resolved data.
- Must not contain: complete screen state, theme defaults, or module drawing code.

Event types include notification appearing, unlock gesture, incoming call accepted, message write-on starting, scroll movement, keyboard appearing, and app transition. Frames are evaluated deterministically relative to the parent shot unless a later schema explicitly defines another frame space.

### `themes`

- Purpose: reusable visual system or style pack.
- SQL/stable fields: `id`, `production_id`, `name`, `family`, `version`.
- JSON/flexible fields: `tokens_json`.
- Relationships: belongs to a production; referenced by actors and screen instances.
- Must not contain: shot-specific timing or message content.

`tokens_json` may contain font, bubble, notification, and status-bar tokens; colors; spacing; radii; shadows; and component defaults.

### `devices`

- Purpose: reusable device identity, screen geometry, and device-pack entry.
- SQL/stable fields: `id`, `production_id`, `name`, `manufacturer`, `model`, `os_family`, `frame_asset_id`.
- JSON/flexible fields: `metrics_json`.
- Relationships: belongs to a production; may reference a media asset for its frame; referenced by actors and screen instances.
- Must not contain: actor-specific content or shot-specific transforms.

`metrics_json` may contain canvas size, screen bounds, viewport, safe areas, status-bar height, notch/dynamic-island geometry, corner radius, and pixel ratio.

### `device_states`

- Purpose: reusable state layered onto device metrics for a particular presentation condition.
- SQL/stable fields: `id`, `production_id`, `device_id`, `name`.
- JSON/flexible fields: `state_json` for time, battery, signal, network, orientation, lock state, and transient hardware UI.
- Relationships: belongs to a production and device; referenced by screen instances.
- Must not contain: base device geometry, narrative content, or shot transforms.

### `actors`

- Purpose: fictional or narrative people/accounts participating in screen content; they are not necessarily real application users.
- SQL/stable fields: `id`, `production_id`, `display_name`, `short_name`, `avatar_asset_id`, `default_device_id`, `default_theme_id`.
- JSON/flexible fields: `metadata_json`.
- Relationships: belongs to a production; may reference avatar, device, and theme defaults; referenced as owner, sender, or target.
- Must not contain: credentials, visual-module logic, or copies of conversations/messages.

### `apps`

- Purpose: reusable identity and defaults for an app represented on a device.
- SQL/stable fields: `id`, `production_id`, `name`, `bundle_key`, `app_type`, `icon_asset_id`.
- JSON/flexible fields: `config_json`, `metadata_json`.
- Relationships: belongs to a production; may reference an icon media asset; referenced by notifications, calls, data sources, or templates.
- Must not contain: shot placement, actor credentials, or renderer-specific code.

### `media_assets`

- Purpose: registry of production-owned images, video, audio, fonts, and other files.
- SQL/stable fields: `id`, `production_id`, `name`, `asset_type`, `uri`, `mime_type`, `checksum`.
- JSON/flexible fields: `dimensions_json`, `metadata_json`.
- Relationships: belongs to a production; referenced through `media_asset_id` or role-specific asset IDs.
- Must not contain: binary data duplicated into narrative records or per-shot transforms.

### `animation_presets`

- Purpose: reusable animation timing and curve definitions.
- SQL/stable fields: `id`, `production_id`, `name`, `animation_type`, `version`.
- JSON/flexible fields: `parameters_json` for easing, spring, stagger, opacity, scale, and motion settings.
- Relationships: belongs to a production; referenced by screen events and visual configuration.
- Must not contain: a specific event's start frame, target ID, or narrative payload.

### `render_presets`

- Purpose: reusable final-output settings.
- SQL/stable fields: `id`, `production_id`, `name`, `width`, `height`, `fps`, `format`.
- JSON/flexible fields: `codec_json`, `color_json`, `quality_json`, `export_json`.
- Relationships: belongs to a production; referenced by shots.
- Must not contain: screen layout, narrative content, or module props.

### `conversations`

- Purpose: ordered message context and participant grouping for chat-like screens.
- SQL/stable fields: `id`, `production_id`, `name`, `app_id`, `owner_actor_id`, `target_actor_id`.
- JSON/flexible fields: `participant_ids_json`, `metadata_json`.
- Relationships: belongs to a production; may reference an app and actors; owns messages; referenced from `data_ref_json`.
- Must not contain: device metrics, screen transforms, or message rows embedded as the canonical copy.

### `messages`

- Purpose: ordered narrative items within a conversation, including frame-addressable entrance and write-on behavior.
- SQL/stable fields: `id`, `conversation_id`, `sort_order`, `sender_actor_id`, `message_type`, `text`, `start_frame`, `enter_duration_frames`, `write_on_enabled`, `write_on_start_frame`, `write_on_duration_frames`, `exit_frame`, `media_asset_id`.
- JSON/flexible fields: `style_override_json`, `animation_override_json`, `layout_override_json`, `metadata_json`.
- Relationships: belongs to a conversation; references a sender actor and optional media asset.
- Must not contain: normal theme font, color, padding, or radius values unless they are intentional one-off overrides.

> Warning: Do not store normal bubble colors on messages. Store them in the theme and use `style_override_json` only for exceptions.

### `notifications`

- Purpose: narrative notification content that screens and events can reference.
- SQL/stable fields: `id`, `production_id`, `app_id`, `owner_actor_id`, `sender_actor_id`, `notification_type`, `title`, `body`, `sort_order`.
- JSON/flexible fields: `payload_json`, `style_override_json`, `metadata_json`.
- Relationships: belongs to a production; references an app and optional actors; referenced from screen-instance data or event payloads using `notification_id`.
- Must not contain: appearance timing for every shot, normal theme tokens, or screen transforms.

### `calls`

- Purpose: narrative incoming or active call data.
- SQL/stable fields: `id`, `production_id`, `app_id`, `owner_actor_id`, `target_actor_id`, `call_type`, `initial_state`.
- JSON/flexible fields: `payload_json`, `metadata_json`.
- Relationships: belongs to a production and may reference an app and actors; referenced from screen-instance data or events using `call_id`.
- Must not contain: shot-specific accept timing, device metrics, or drawing instructions.

### `data_sources`

- Purpose: custom or imported structured data for custom apps and future screen types.
- SQL/stable fields: `id`, `production_id`, `name`, `data_type`, `app_id`, `version`.
- JSON/flexible fields: `data_json`, `config_json`, `metadata_json`.
- Relationships: belongs to a production; may reference an app; referenced from `data_ref_json` using `data_source_id`.
- Must not contain: executable visual-module code, unrelated production resources, or hidden live-fetch dependencies required during render.

## Resolution guidance

Resolvers validate that references remain inside the same production, apply template/theme/device defaults, then apply instance and item overrides. They return self-contained camelCase props. Visual modules must never interpret SQL rows or retrieve missing references themselves.
