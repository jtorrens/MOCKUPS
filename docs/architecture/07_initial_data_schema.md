# Initial data schema

This is the persistence-oriented schema for MOCKUPS. The initial SQLite implementation now exists. The current design-stage schema is version 10 and intentionally breaks from the earlier Screen Template layer: app identity/defaults and module-specific defaults are now direct runtime layers, and explicit module instances own per-shot content, behavior, and parameter animation.

## Storage boundary

SQL/stable fields hold IDs, names, relationships, ordering, frame timings, core type discriminators, and references to apps, assets, themes, devices, and actors. JSON/flexible fields hold visual tokens, app defaults, device metrics, module content/behavior/parameter animation, event payloads, visual animation preset parameters, transforms, and module-specific configuration.

JSON column names use a `_json` suffix below. Resolvers parse these values and emit camelCase resolved props for visual modules.

## Conceptual hierarchy

```text
Production
 ├─ Resources
 │   ├─ Themes
 │   ├─ ModuleThemeConfigs
 │   ├─ Devices
 │   ├─ Actors
 │   ├─ Apps
 │   ├─ MediaAssets
 │   ├─ AnimationPresets
 │   └─ RenderPresets
 │
 ├─ Data
 │   ├─ Conversations
 │   │   └─ Messages
 │   ├─ Notifications
 │   ├─ Calls
 │   └─ DataSources
 │
 └─ Episodes
     └─ Shots
         └─ ScreenInstances
             ├─ ModuleInstances
             └─ ScreenEvents
```

## Entity schemas

### `productions`

- Purpose: root scope for all reusable resources, narrative data, and shots.
- SQL/stable fields: `id`, `name`, `slug`, `default_fps`, `created_at`, `updated_at`.
- JSON/flexible fields: `settings_json`, `metadata_json`.
- Relationships: owns every production-scoped entity.
- Must not contain: individual screen props, message content, or shot timing.

### `episodes`

- Purpose: editorial container inside one production.
- SQL/stable fields: `id`, `production_id`, `name`, `slug`, `sort_order`.
- JSON/flexible fields: `metadata_json`.
- Relationships: belongs to one production; owns many shots.
- Must not contain: screen-instance timing, module content, render output, or reusable production resources.

### `shots`

- Purpose: central device-screen action sequence requested for preview and final rendering.
- SQL/stable fields: `id`, `production_id`, `episode_id`, `owner_actor_id`, `name`, `slug`, `version`, `sort_order`, `duration_frames`, `fps`, `render_preset_id`.
- JSON/flexible fields: `canvas_json`, `metadata_json`.
- Relationships: belongs to one production, optionally belongs to one episode, references one owner actor, and optionally references one render preset; owns many screen instances.
- Must not contain: a single mandatory chat/device reference, detailed UI drawing rules, or placement into an external UHD/video plate.

### `screen_instances`

- Purpose: runtime container for placing a versioned screen module inside a shot.
- SQL/stable fields: `id`, `shot_id`, `app_id`, `screen_type`, `module_id`, `module_schema_version`, `owner_actor_id`, `device_id`, `device_state_id`, `theme_id`, `theme_mode`, `start_frame`, `end_frame`, `layer_order`.
- JSON/flexible fields: `device_state_json`, `transform_json`; compatibility fields are `data_ref_json`, `props_json`, `transition_in_json`, `transition_out_json`, `module_data_json`, `module_config_json`, and `module_tokens_override_json`.
- Relationships: belongs to a shot and references an app plus optional screen-level overrides for owner/device/theme context; owns module instances and screen events. By default, owner comes from the shot, and device/theme come from that actor's defaults. Module-internal references remain inside versioned module instance JSON and are validated by the selected module.
- Must not contain: detailed drawing logic or copied theme/device records.

`screen_type` is a broad discriminator; `module_id` + `module_schema_version` select the exact module contract. `transform_json` transforms the device-screen render inside the shot's device render space. `core.chat` now reads content/behavior from `module_instances` and has no runtime fallback to `data_ref_json` or `props_json`.

SQLite schema version 10 includes `screen_instances.app_id`, explicit `module_instances`, `module_instances.animation_json`, production default FPS, episode/shot render slugs, inline `screen_instances.device_state_json`, removes the active `screen_templates` table, and scopes `module_theme_configs` by `theme_id + app_id + module_id + module_schema_version`. This is a design-stage breaking schema: local development databases may be reset explicitly with `npm run db:reset`; normal app startup must not reseed or overwrite edited data.

### `module_instances`

- Purpose: concrete module payload and behavior for a module attached to one screen instance.
- SQL/stable fields: `id`, `screen_instance_id`, `module_id`, `module_schema_version`, `sort_order`.
- JSON/flexible fields: `content_json`, `behavior_json`, `animation_json`, `metadata_json`.
- Relationships: belongs to one screen instance. One screen instance may own more than one module instance if a future module composition requires it.
- Must not contain: reusable visual design defaults, copied theme/app/module tokens, device geometry, or render output.

For `core.chat@1`, `content_json` owns participants, header, messages, timings, media references, and sender IDs. `behavior_json` owns per-shot behavior such as `showHeader`, `showStatusBar`, `showKeyboard`, `initialScroll`, and `messageGrouping`. `animation_json` is reserved for per-frame changes to module parameters, such as changing a header subtitle, message text, or message status at specific frames. This is separate from reveal modes like `writeDown`, which define how the current text value is displayed.

### `screen_events`

- Purpose: temporal changes inside a screen instance.
- SQL/stable fields: `id`, `screen_instance_id`, `event_type`, `start_frame`, `duration_frames`, `target_id`, `animation_preset_id`.
- JSON/flexible fields: `payload_json`.
- Relationships: belongs to one screen instance and may reference an animation preset; `target_id` identifies the affected narrative or visual item within resolved data.
- Must not contain: complete screen state, theme defaults, or module drawing code.

Canonical initial event types include `notification_appears`, `unlock_gesture`, `incoming_call_accepted`, `message_write_on_starts`, `scroll_moves`, `keyboard_appears`, and `app_transition`. Event `start_frame` is relative to the parent screen instance. ShotBuilder derives the local frame from the requested shot frame before module rendering.

### `themes`

- Purpose: reusable visual system or style pack.
- SQL/stable fields: `id`, `production_id`, `name`, `family`, `version`.
- JSON/flexible fields: `tokens_json`.
- Relationships: belongs to a production; referenced by actors and screen instances.
- Must not contain: shot-specific timing or message content.

`tokens_json` may contain logical-unit typography/layout/component tokens, installed font selection, base values, `modes.light`, `modes.dark`, and `defaultMode`. The resolver merges base + selected mode + local overrides. See `08_visual_tokens_layout_contract.md`.

Global theme tokens should not own every internal module-specific design value. Chat bubble geometry, message spacing, chat header defaults, cursor behavior, and similar module internals belong in `module_theme_configs.tokens_json`.

### `module_theme_configs`

- Purpose: reusable module-specific design defaults for one theme, app, and module/schema version.
- SQL/stable fields: `id`, `production_id`, `theme_id`, `app_id`, `module_id`, `module_schema_version`, `name`.
- JSON/flexible fields: `tokens_json`, `metadata_json`.
- Relationships: belongs to a production, theme, and app; selected by screen instances through their `theme_id`, `app_id`, `module_id`, and `module_schema_version`.
- Must not contain: shot content, device geometry, live device state, or per-instance exceptions.

Resolution order is:

```text
theme.tokens_json
  → app.config_json tokens_json
  → module_theme_configs.tokens_json
  → selected mode collapse for render
```

App and module JSON may contain `modes.light` and `modes.dark` color values. These are not collapsed until render/preview resolution. For the initial fixture, `module_theme_configs` contains one Chat config for `theme_ios_light` + `app_messages` + `core.chat` schema version 1.

### `devices`

- Purpose: reusable device identity, screen geometry, and device-pack entry.
- SQL/stable fields: `id`, `production_id`, `name`, `manufacturer`, `model`, `os_family`, `frame_asset_id`.
- JSON/flexible fields: `metrics_json`.
- Relationships: belongs to a production; may reference a media asset for its frame; referenced by actors and screen instances.
- Must not contain: actor-specific content or shot-specific transforms.

`metrics_json` may contain logical `designSpace`, internal pixel `renderSize`, `scaleToPixels`, canvas/screen/viewport bounds, safe areas, status-bar area, notch/dynamic-island geometry, corner radius, pixel ratio, and default screen scale.

### `device_states` deprecated compatibility table

- Purpose: legacy/import compatibility for reusable state layered onto device metrics. Active screen editing stores state inline on `screen_instances.device_state_json`.
- SQL/stable fields: `id`, `production_id`, `device_id`, `name`.
- JSON/flexible fields: `state_json` for time/date, battery, signal, network label, Wi-Fi state/icon, focus mode, orientation, lock state, and transient hardware UI.
- Relationships: belongs to a production and device; may be referenced by legacy screen instances.
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
- Relationships: belongs to a production; may reference an icon media asset; referenced by screen instances, module theme configs, notifications, calls, and data sources.
- Must not contain: shot placement, actor credentials, or renderer-specific code.

`config_json.tokens_json` stores app-level reusable defaults inherited by screens, such as generic app typography, wallpaper roles, icon references, shared surfaces, and mode-aware app colors. App wallpaper supports `kind: "solid" | "image"` and a shared decimal `opacity` in the `0–1` range. Solid wallpapers store mode colors under `modes.light.wallpaper.color` and `modes.dark.wallpaper.color`; image wallpapers store direct production-relative media at `wallpaper.image.filePath` and render as centered cover/crop. If an app wants to change a generic inherited role such as `colors.background` or `colors.accent`, it should override that same token path. New app-specific roles should be genuinely new, such as `colors.navigationBackground`, not duplicated as `appBackground` / `appAccent`.

### `media_assets`

- Purpose: registry of production-owned images, video, audio, fonts, and other files.
- SQL/stable fields: `id`, `production_id`, `name`, `asset_type`, `uri`, `mime_type`, `checksum`.
- JSON/flexible fields: `dimensions_json`, `metadata_json` including reusable/one-off usage scope where needed.
- Relationships: belongs to a production; referenced through `media_asset_id` or role-specific asset IDs.
- Must not contain: heavy binary data duplicated into SQLite or per-component transforms. Prefer project-relative URIs. Small inline SVG may be considered later.

Modules use asset IDs with a media window (logical width/height/offsets) and an asset transform (ratio scale, translation, rotation). OS/app icon tokens are resolved through a separate theme/OS/mode-aware icon map rather than treated as user media.

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
- Must not contain: screen layout, narrative content, module props, or external video-plate placement. It may define output scale/size, format, codec, alpha, color, and quality.

### `conversations`

- Purpose: ordered message context and participant grouping for chat-like screens.
- SQL/stable fields: `id`, `production_id`, `name`, `app_id`, `owner_actor_id`, `target_actor_id`.
- JSON/flexible fields: `metadata_json`.
- Relationships: belongs to a production; may reference an app and actors; owns messages; referenced from `data_ref_json`. Group membership is a stable relationship represented by a support table such as `conversation_participants(conversation_id, actor_id, sort_order, role)`.
- Must not contain: device metrics, screen transforms, or message rows embedded as the canonical copy.

Participant IDs must not be stored only as a canonical JSON list. A JSON projection may be emitted in resolved props, but SQL owns participant identity, membership, order, and role.

Deprecation note: these normalized relationships remain physically available for historical/import compatibility, but Chat runtime does not read them. `core.chat` owns versioned `participants[]`, header, and messages in `module_instances.content_json`; participants may reference production actors, group membership is represented directly, and every message uses `senderParticipantId`.

### `messages`

- Purpose: ordered narrative items within a conversation, including frame-addressable entrance and write-on behavior.
- SQL/stable fields: `id`, `conversation_id`, `sort_order`, `sender_actor_id`, `message_type`, `text`, `start_frame`, `enter_duration_frames`, `write_on_enabled`, `write_on_start_frame`, `write_on_duration_frames`, `exit_frame`, `media_asset_id`.
- JSON/flexible fields: `style_override_json`, `animation_override_json`, `layout_override_json`, `metadata_json`.
- Relationships: belongs to a conversation; references a sender actor and optional media asset.
- Must not contain: normal theme font, color, padding, or radius values unless they are intentional one-off overrides.

Message timing fields are relative to the screen-instance/conversation timeline supplied by the resolver, not absolute shot frames. This keeps conversation data reusable when a screen instance starts at a different shot frame.

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

Resolvers validate references remain inside the same production, including actor/media IDs inside module JSON and event targets. They convert shot frames to local frames and return self-contained camelCase props. For Chat, module config and token overrides are canonical; legacy conversation/message records are not consulted.
