# Foundational module contracts

## Architecture model

```text
Production
  ├─ Themes / Apps / ModuleThemeConfigs / Devices / Actors / Assets / Icons
  └─ Episodes
      └─ Shots
          └─ ScreenInstances
              ├─ module_id
              ├─ module_schema_version
              ├─ transform_json
              └─ runtime context refs
                  └─ ModuleInstances
                      ├─ content_json
                      ├─ behavior_json
                      └─ metadata_json
```

A shot is the frame-addressable sequence of actions shown by a device screen. MOCKUPS renders that sequence in the device render space. Placement into an UHD plate or other filmed composition is deliberately outside this project.

A shot has an owner actor that supplies default device/theme context for its screen instances. A screen instance is the runtime container for one versioned screen module inside one app. It supplies the app, module selection, shot timing, device state, theme mode, transform, resolved assets/icons, and events. A module instance supplies the selected module's content and behavior for that screen instance. It may still carry explicit context overrides when needed. A screen module owns the schema and interpretation of its internal content and behavior.

## Screen-instance target

The target stable/JSON boundary is:

```text
id, shot_id, app_id, screen_type
module_id, module_schema_version
owner_actor_id, device_id, device_state_id, theme_id, theme_mode
start_frame, end_frame, layer_order
transform_json
```

The target module-instance JSON boundary is:

```text
id, screen_instance_id, module_id, module_schema_version, sort_order
content_json, behavior_json, animation_json, metadata_json
```

`content_json` is shot-specific content created by the module editor. `behavior_json` is instance behavior. `animation_json` is the module-instance timeline for parameter value changes, stored separately from both base content and behavior. Reusable global design remains in the theme; reusable app defaults live in `apps.config_json.tokens_json`; reusable module-specific design defaults live in `module_theme_configs`. Per-instance visual token overrides are not part of the active model. For Chat these are now the only active runtime sources. Legacy narrative Chat tables and legacy screen-instance module JSON columns remain physically present only as deprecated structures.

Parameter animation is not the same thing as `animation_presets`. Presets, if used, remain suitable for visual transitions such as entrances, exits, fades, slides, and app/screen movement. `module_instances.animation_json` is for data/value changes over frames, for example a Chat header subtitle changing, a message status changing, or a message text being replaced. Text reveal modes such as `writeDown` or `writeDownNatural` describe how the current text is displayed; keyframed parameter animation describes what the text value is at a given frame.

Module schema versions are independent from the app/SQLite schema version. A host must locate `module_id`, select exactly the supported `module_schema_version`, and validate both JSON documents with that module's schemas. Missing modules and unsupported versions fail clearly without modifying stored JSON.

## Module boundary

The portable contract is conceptually:

```ts
type ScreenModuleInput<TData, TConfig> = {
  frame: number;
  fps: number;
  screenInstanceId: string;
  moduleId: string;
  moduleSchemaVersion: number;
  appId: string;
  moduleData: TData;
  moduleConfig: TConfig;
  ownerActor?: ResolvedActor;
  device: ResolvedDevice;
  deviceState?: ResolvedDeviceState;
  themeTokens: ResolvedThemeTokens;
  themeMode: "light" | "dark";
  assets: ResolvedAssetMap;
  icons: ResolvedIconMap;
  props?: Record<string, unknown>;
};

type ScreenModuleOutput = RenderableNode;
```

The host/resolver loads records, validates module JSON through the selected module definition, resolves references, merges tokens, and supplies render-ready assets/icons. Token merge order is:

```text
theme base tokens
  → selected theme mode tokens
  → app tokens for app_id
  → selected app mode tokens
  → module_theme_configs tokens for theme_id + app_id + module_id + module_schema_version
  → selected module theme config mode tokens
```

Modules never query repositories or open files. Their output is pure and deterministic:

```text
module input + frame/context → RenderableNode
```

Renderer integrations such as Remotion only adapt the resulting tree.

## Chat-owned data

Chat module data contains header data, messages, media references, message
timings, message direction, and direct production actor references. The previous
module-local `participants` layer is deprecated for the current design:
`direction` carries the visual role (`incoming`, `outgoing`, `system`) and
`actorId` points to the production actor when a sender/contact is needed.

```text
module_instances.content_json
  header
  messages[] → actorId, direction, text, optional media attachment, timings

module_instances.behavior_json
  showHeader, showKeyboard, initialScroll
  messageGrouping, debug, behavior defaults
```

`core.chat` schema version 1 now resolves directly from this module JSON. Direction is explicit per message. Non-system messages reference production actors through `actorId`; system messages may omit actor identity. Optional media uses `mediaAssetId` or a direct file path plus a logical media window/transform and may coexist with message text, for example an image/video with a caption or accompanying text.

There is no runtime fallback to `data_ref_json`, `conversations`, `conversation_participants`, `messages`, or generic `props_json`. Those SQLite structures may remain until a later cleanup/migration task, but debug/editor tooling must not write them.

## Logical design and output

Theme, App, and Module dimensions are authored as logical design units. `device.metrics_json.designSpace` defines the logical coordinate system, `renderSize` defines the internal pixel dimensions, and `scaleToPixels` maps between them. The resolver scales design-unit tokens such as font sizes, line heights, padding, spacing, radii, avatar sizes, and shadows into device render pixels before producing renderable props. Ratios and frame counts are not scaled. Font weight variants are named font-face selections from the active family and are not scaled.

Normal output is the device render resolution. A render preset may change output scale, format, codec, alpha, color, or quality. It does not describe placement in an external video plate.

## Themes, text, assets, and icons

A theme stores base global tokens, named modes such as `light` and `dark`, `defaultMode`, and an approved production font family plus generic named weight selected through font pickers. Apps store generic app-level reusable defaults such as wallpaper/background roles, accent colors, direct app icon media/crop metadata, shared surfaces, and app-wide typography tokens. App wallpaper is either solid mode-aware color or direct image media rendered cover/center, with opacity stored as a decimal `0–1` layer value. Module theme configs store module-specific values such as Chat message/header typography roles, bubble geometry, message spacing, chat header defaults, cursor behavior, and future module-local design defaults. App and module color tokens can carry light and dark values; the resolver collapses them only for the selected render mode. The shot selects the owner actor; that actor supplies default device and theme for the plane. The screen instance may still select `theme_mode` from the selected theme's available modes, and may carry explicit context overrides when needed. Modules receive only the merged tokens. Font families used by preview/render should be registered in `production_fonts` and copied into the production root.

Text measurement has two modes: an approximate renderer-agnostic mode for fast structural work, and a final renderer-assisted mode shared by preview and export. Manual line breaks are preserved before wrapping; reveal operations should segment grapheme clusters where possible.

Heavy media remains external and uses preferably project-relative URIs. Asset metadata distinguishes reusable production media from one-off content usage without creating separate binary systems. Component media uses an asset reference plus a logical-unit media window and a transform (`scale` ratio, translation, rotation), not only cover/contain.

OS/app iconography uses theme/OS/mode-aware icon tokens and is separate from content media. The host resolves icon tokens and asset IDs before render. Missing-reference behavior is configurable, with final render defaulting to an error. Small inline SVG storage may be introduced later only if it proves useful.

## Module editors and debug boundaries

Module editors edit only module-instance content, behavior, and parameter animation. They are independent of final owner, device, and theme. They may provide a temporary preview context; the shot/screen instance supplies final runtime context.

The generic JSON tree editor is a fallback editing surface, not the final UX for every module. Module-specific editor hint contracts may provide path labels, widgets, collapsed row summaries, and safe structural affordances for a given `module_id` + `module_schema_version`. `core.chat@1` is the first registered contract. A future specialized module editor may replace the generic tree for a module while keeping the same stored JSON shape.

The app/debug UI must show sources separately: project hierarchy, library resources, module-instance content, module-instance behavior, app tokens, module theme configs, reusable theme tokens, device metrics/state, and calculated renderable output. `RenderableNode` is derived output and is never directly edited. Shot-specific content belongs to a module editor; reusable global design belongs to a theme editor; app-wide defaults belong to an app editor; reusable module-specific design belongs to a module theme config editor.

The initial local app shell now implements this boundary over SQLite. Its HTTP API owns persistence and validation, then routes saved records through `SQLiteRepository` and the existing resolvers/modules. The React preview and Remotion export both reuse the neutral `RenderableReactAdapter` from `src/visual/adapters/react/`; neither Remotion nor the browser is a source of truth. The preview shell may scale and overlay a device frame, but it must not change the renderable's internal size, padding, border, or coordinate system. Device chrome is an external overlay; the renderable viewport remains exactly the device render size, and final PNG output applies only the selected output scale. The current Project workspace can create productions, episodes, and shots with conservative defaults; deep duplicate/delete and screen-instance creation remain future workflow decisions.

`npm run render:frame` renders the shared Remotion composition at the canonical check frame to `out/current-frame.png`. This PNG is ignored by git and is intended as a quick parity check between the debug preview and the final render path. `npm run validate:preview` validates the preview sizing helper used by the debug shell.
