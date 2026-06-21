# Foundational module contracts

## Architecture model

```text
Production
  ├─ Themes / ModuleThemeConfigs / Devices / Actors / Assets / Icons
  └─ Episodes
      └─ Shots
          └─ ScreenInstances
              ├─ module_id
              ├─ module_schema_version
              ├─ module_data_json
              ├─ module_config_json
              ├─ module_tokens_override_json
              └─ runtime context refs
```

A shot is the frame-addressable sequence of actions shown by a device screen. MOCKUPS renders that sequence in the device render space. Placement into an UHD plate or other filmed composition is deliberately outside this project.

A shot has an owner actor that supplies default device/theme context for its screen instances. A screen instance is the runtime container for one versioned screen module. It supplies the module selection, shot timing, device state, theme mode, module JSON, local token exceptions, transforms, resolved assets/icons, and events. It may still carry explicit context overrides when needed. A screen module owns the schema and interpretation of its internal content and behavior.

## Screen-instance target

The target stable/JSON boundary is:

```text
id, shot_id, screen_type, screen_template_id
module_id, module_schema_version
owner_actor_id, device_id, device_state_id, theme_id, theme_mode
start_frame, end_frame, layer_order
module_data_json, module_config_json, module_tokens_override_json
transform_json
```

`module_data_json` is shot-specific content created by the module editor. `module_config_json` is instance behavior. `module_tokens_override_json` contains intentional local visual exceptions. Reusable global design remains in the theme; reusable module-specific design defaults live in `module_theme_configs`. For Chat these are now the only active runtime sources. Legacy columns/tables remain physically present only as deprecated structures.

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
  → module_theme_configs tokens for theme_id + module_id + module_schema_version
  → selected module theme config mode tokens
  → screen_instance.module_tokens_override_json
```

Modules never query repositories or open files. Their output is pure and deterministic:

```text
module input + frame/context → RenderableNode
```

Renderer integrations such as Remotion only adapt the resulting tree.

## Chat-owned data

Chat module data contains participants, header data, messages, media references, message timings, and `senderParticipantId`. A participant may reference a reusable production actor but has module-local identity, so group chats do not depend on an owner/target pair.

```text
module_data_json
  participants[]
  header
  messages[] → senderParticipantId, text, optional media attachment, timings

module_config_json
  showHeader, showKeyboard, initialScroll
  messageGrouping, debug, behavior defaults

module_tokens_override_json
  local gutter/header/bubble exceptions
```

`core.chat` schema version 1 now resolves directly from this module JSON. Direction is derived from participant ownership: owner sender → outgoing, non-owner sender → incoming, and system type → system. Participants may reference actors or carry a module-local display name, so group chats are native. Optional media uses `mediaAssetId` plus a logical media window/transform and may coexist with message text, for example an image/video with a caption or accompanying text.

There is no runtime fallback to `data_ref_json`, `conversations`, `conversation_participants`, `messages`, or generic `props_json`. Those SQLite structures may remain until a later cleanup/migration task, but debug/editor tooling must not write them.

## Logical design and output

Theme dimensions are logical units. `device.metrics_json.designSpace` defines the logical coordinate system, `renderSize` defines the internal pixel dimensions, and `scaleToPixels` maps between them. Layout is computed in design-space units and the renderer maps it to internal pixels.

Normal output is the device render resolution. A render preset may change output scale, format, codec, alpha, color, or quality. It does not describe placement in an external video plate.

## Themes, text, assets, and icons

A theme stores base global tokens, named modes such as `light` and `dark`, `defaultMode`, and the installed font family/style/weight selected through a font picker. Module theme configs store module-specific values such as Chat bubble geometry, message spacing, chat header defaults, cursor behavior, and future module-local design defaults. The shot selects the owner actor; that actor supplies default device and theme for the plane. The screen instance may still select `theme_mode` from the selected theme's available modes, and may carry explicit context overrides when needed. Modules receive only the merged tokens. No production font whitelist/table is required.

Text measurement has two modes: an approximate renderer-agnostic mode for fast structural work, and a final renderer-assisted mode shared by preview and export. Manual line breaks are preserved before wrapping; reveal operations should segment grapheme clusters where possible.

Heavy media remains external and uses preferably project-relative URIs. Asset metadata distinguishes reusable production media from one-off content usage without creating separate binary systems. Component media uses an asset reference plus a logical-unit media window and a transform (`scale` ratio, translation, rotation), not only cover/contain.

OS/app iconography uses theme/OS/mode-aware icon tokens and is separate from content media. The host resolves icon tokens and asset IDs before render. Missing-reference behavior is configurable, with final render defaulting to an error. Small inline SVG storage may be introduced later only if it proves useful.

## Module editors and debug boundaries

Module editors edit only module data/config and are independent of final owner, device, and theme. They may provide a temporary preview context; the shot/screen instance supplies final runtime context.

The generic JSON tree editor is a fallback editing surface, not the final UX for every module. Module-specific editor hint contracts may provide path labels, widgets, collapsed row summaries, and safe structural affordances for a given `module_id` + `module_schema_version`. `core.chat@1` is the first registered contract. A future specialized module editor may replace the generic tree for a module while keeping the same stored JSON shape.

The app/debug UI must show sources separately: project hierarchy, library resources, module data, module config, module theme configs, token overrides, reusable theme tokens, device metrics/state, and calculated renderable output. `RenderableNode` is derived output and is never directly edited. Shot-specific content belongs to a module editor; reusable global design belongs to a theme editor; reusable module-specific design belongs to a module theme config editor.

The initial local app shell now implements this boundary over SQLite. Its HTTP API owns persistence and validation, then routes saved records through `SQLiteRepository` and the existing resolvers/modules. The React preview reuses `RemotionRenderableAdapter`; it does not make Remotion or the browser a source of truth. The current Project workspace can create productions, episodes, and shots with conservative defaults; deep duplicate/delete and screen-instance creation remain future workflow decisions.
