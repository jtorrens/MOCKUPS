# Architecture decisions

## D001 — Production is the root entity

Status: accepted

Production is the root scope for resources, reusable presets, actors, devices, themes, assets, data, and shots.

Implications:
- All reusable resources should belong to a production.
- Shots always belong to a production.

## D002 — Shot is the central render unit

Status: accepted

Rendering starts from a production and shot, not from a particular screen data type.

Implications:
- The primary render operation is `renderShot(productionId, shotId, frame)`.
- A shot may compose multiple screen instances.

## D003 — Chat is only one screen type

Status: accepted

Chat uses the same screen-instance and module model as lock screens, notifications, calls, home screens, custom apps, and future types.

Implications:
- Chat-specific entities must not define the root architecture.
- New screen types should not require restructuring shots.

## D004 — SQL for stable relationships, JSON for flexible config

Status: accepted

SQL stores stable, queryable identities and relationships. JSON stores evolving visual props, tokens, metrics, payloads, transforms, and module configuration.

Implications:
- Integrity-critical links use SQL foreign keys.
- Module-specific configuration can evolve without excessive schema churn.

## D005 — Visual modules do not access the database directly

Status: accepted

Visual modules consume resolved inputs and remain independent of persistence.

Implications:
- Modules do not query SQL or fetch production data.
- Modules can be previewed and tested with fixtures.

## D006 — Resolvers create resolved props

Status: accepted

Resolvers combine relational data and JSON configuration into self-contained props for visual modules.

Implications:
- Data loading and default/override precedence live outside modules.
- Preview and final render can share identical resolved inputs.

## D007 — ShotBuilder composes screens but does not draw them

Status: accepted

ShotBuilder handles timing, transforms, layer order, and composition; visual modules draw screen UI.

Implications:
- Screen-level visual details remain encapsulated in modules.
- Composition logic remains independent of screen type.

## D008 — Modules receive props + frame and return renderables

Status: accepted

Every visual module follows a frame-addressable input/output contract.

Implications:
- Modules can animate without hidden global state.
- Renderer adapters can consume a consistent renderable result.

## D009 — The renderer should be frame-based and deterministic

Status: accepted

The same resolved data and frame must produce the same visual output.

Implications:
- Rendering must not depend on wall-clock time or uncontrolled external state.
- Preview and final output should share rendering logic.

## D010 — Visual style values live in theme tokens unless instance-specific

Status: accepted

Reusable typography, colors, component dimensions, spacing, shadows, and visual behavior belong in `theme.tokens_json`; one-screen behavior belongs in `screen_instance.props_json`.

Implications:
- Modules receive reusable style through resolved theme tokens.
- Instance props do not duplicate ordinary theme values.

## D011 — Device geometry lives in device metrics

Status: accepted

Canvas, screen, viewport, safe area, hardware intrusions, corner radius, pixel ratio, and default screen scale belong in `device.metrics_json`.

Implications:
- Themes remain portable across compatible devices.
- Shot transforms do not mutate canonical device geometry.

## D012 — Device live state lives in device state JSON

Status: accepted

Time, battery, signal, network/Wi-Fi, focus, orientation, and lock state belong in `device_states.state_json`.

Implications:
- Device geometry remains stable and reusable.
- Resolvers combine device metrics with the selected live state.

## D013 — Visual modules receive render-ready token values through resolved props

Status: accepted

Resolvers merge theme, device, state, actors, instance props, data, events, and overrides into self-contained module input.

Implications:
- Visual modules do not fetch or interpret persistence records.
- Atomic modules receive concrete values rather than token references.

## D014 — Renderable metadata is diagnostic, not canonical configuration

Status: accepted

Renderable metadata records approximation, provenance, and debugging information, not required style/layout configuration.

Implications:
- Required values stay in resolved props and explicit renderable fields.
- Metadata may be removed without changing intended visual output.

## D015 — Logical design space is distinct from render/output space

Status: accepted

Theme dimensions use logical units. Devices define `metrics_json.designSpace`, `renderSize`, and `scaleToPixels`; shots describe device-screen actions, not placement in an external plate. Render presets may control output scale, format, codec, alpha, color, and quality only.

Implications:
- Layout remains portable across compatible render resolutions.
- Normal output is the device render resolution; AE/Fusion/Resolve/Nuke owns later plate placement.

## D016 — Assets, content media, and icon tokens have separate roles

Status: accepted

Reusable and one-off content media share asset references but differ by usage/scope metadata. Heavy media remains external by URI. OS/app iconography is theme/OS/mode-aware token data, separate from content media. Media placement uses a logical media window plus ratio-based asset transform.

Implications:
- Modules reference assets/icons but the host resolves them before render.
- Small inline SVG may be added later only if useful; final render defaults to error for missing required assets.

## D017 — Text measurement has approximate and final modes

Status: accepted

Fast structural layout may use approximate renderer-agnostic measurement. Preview and export share one final renderer-assisted strategy. Manual line breaks precede wrapping, and text reveal uses grapheme clusters where possible.

Implications:
- Preview/export cannot diverge in final line breaking.
- Themes select installed font family and named weight variants; no production font whitelist/table is introduced.
- The renderer UI keeps one shared in-memory font catalog cache per session. All font pickers reuse the same lazy-loaded system-font list and the same in-flight load promise.

## D018 — Modules own animation interpretation and visual behavior

Status: accepted

Resolvers supply resolved data, timings, events, tokens, context, and module config. Modules interpret their own persistent behavior rules and remain pure functions of input plus frame/context.

Implications:
- Module-specific rules live in JSON rather than new global SQL columns.
- Modules never access repositories or databases.

## D019 — Screen modules own versioned internal schemas

Status: accepted

Every screen module is selected by `module_id` and `module_schema_version`, validates its own data/config JSON, and emits `RenderableNode` from a stable resolved-context contract.

Implications:
- The host validates but does not interpret module internals.
- Missing modules and unsupported versions fail clearly without corrupting stored JSON.

## D020 — Module editors are independent from final runtime context

Status: accepted

Module editors create/edit module data and config without owning a user, device, or theme. They may use preview context; screen instances supply final owner actor, device/state, theme/mode, and timing.

Implications:
- One module document can be previewed in temporary contexts.
- Themes contain light/dark modes and modules receive already merged tokens for the selected mode.

## D021 — Chat participants are module-owned

Status: accepted

Chat module data contains participants, and every message references `senderParticipantId`. Participants may reference reusable production actors.

Implications:
- Group chats do not depend on a single owner/target pair.
- `core.chat` schema version 1 uses `module_instances.content_json` as its canonical runtime source.
- Central conversation/message tables may remain physically present but are deprecated and not read by Chat runtime.

## D022 — Module schema versions are independent from app schema versions

Status: accepted

`module_schema_version` versions module JSON independently from SQLite migrations, application releases, and screen-template versions.

Implications:
- Module migrations can be explicit and scoped.
- Loading an unsupported version fails without silently rewriting data.

## D023 — The host resolves module assets and icon tokens

Status: accepted

Modules receive resolved asset and icon maps. They do not open URIs or resolve theme/OS/mode icon variants themselves.

Implications:
- Asset policy is consistent across renderers.
- Missing-asset behavior is configurable, with final rendering defaulting to error.

## D024 — Module data, config, and token overrides are separate

Status: superseded by D045

`module_data_json` stores shot content, `module_config_json` stores instance behavior, and `module_tokens_override_json` stores intentional local visual exceptions. Theme tokens remain the reusable canonical design source.

Implications:
- Editors can expose responsibilities without mixing content and design.
- Chat editors write only module data/config/token overrides; they do not mirror legacy `data_ref_json`, `props_json`, conversations, or messages.

## D025 — Debug UI exposes sources but does not edit render output

Status: accepted

Debug tooling separates module data/config, theme tokens, device metrics/state, overrides, and calculated `RenderableNode`. Per-shot content belongs to module editors; reusable design belongs to theme editors.

Implications:
- `RenderableNode` is inspected, never directly edited.
- The initial debug UI is a calibration surface, not the final editor architecture.

## D026 — Module-specific design tokens live in module theme configs

Status: accepted

Internal design values for a screen module belong in `module_theme_configs.tokens_json`, not directly in reusable global `themes.tokens_json`.

Implications:
- Global themes keep shared typography, base colors, surfaces, status-bar defaults, spacing scales, and broad visual language.
- Chat-specific values such as bubble geometry, tail shape, message spacing, chat header defaults, and cursor behavior live in the Chat module theme config.

## D027 — Module theme configs are scoped by theme and module

Status: accepted

A module theme config is selected by `theme_id`, `module_id`, and `module_schema_version`.

Implications:
- One theme can carry different module-specific defaults for Chat, lock screen, calls, home screen, and future modules.
- A theme/module pair may later have multiple named configs; the current resolver selects the seeded/default config deterministically.

## D028 — Module theme configs may inherit global theme tokens

Status: accepted

Module theme config JSON may reference, copy, or override compatible global theme values. Resolution merges global theme tokens, selected global mode, app tokens, selected app mode, module theme config tokens, and selected module mode.

Implications:
- Modules receive a render-ready merged token object.
- Module configs can stay compact where global tokens are sufficient.
- Light/dark behavior can exist at both global and module-specific levels.

## D029 — Screen instance token overrides remain local exceptions

Status: superseded by D045

`screen_instances.module_tokens_override_json` remains the canonical place for one-off per-shot/per-instance visual exceptions.

Implications:
- Local overrides do not belong in the reusable global theme or reusable module theme config.
- Removing an override should return the screen instance to inherited theme/app/module defaults.

## D030 — JSON editors visualize inherited override state

Status: accepted

JSON editors should show when a local value differs from its inherited parent value. Overridden values appear amber and offer a restore action.

Implications:
- Editors make inheritance visible without changing stored JSON shape.
- Restore should remove the local override key when absence cleanly means inherit; otherwise it may write the inherited value.

## D031 — Chat owns module-specific typography defaults

Status: accepted

Global theme fonts define the reusable family and broad typography scale. Chat-specific message and header typography defaults live in `module_theme_configs.tokens_json.typography`, with fallback to global `themes.tokens_json.fonts`.

Implications:
- Chat can tune message text, header title, and header subtitle independently from other modules.
- Font family, size, line height, and weight can be edited at the module-theme-config layer.
- Screen instance token overrides may still make one-off typography exceptions for a particular Chat screen.

## D032 — Productions contain episodes before shots

Status: accepted

Shots belong to an editorial episode inside a production. The primary app-shell navigation should present this hierarchy as `Production → Episode → Shot → Screen instance`, not as separate flat tabs for those core entities.

Implications:
- `episodes` is a first-class production-scoped entity.
- `shots.episode_id` links shots to their editorial container.
- The debug/app shell uses a project tree for core narrative structure and keeps reusable resources in a separate library area.

## D033 — Shot owner defines the default runtime context

Status: accepted

A shot has an owner actor. That actor supplies the default device and theme for the plane through `actors.default_device_id` and `actors.default_theme_id`. Screen instances may still carry explicit overrides, but the normal editor flow should not require repeating owner/device/theme on every screen instance.

Implications:
- `shots.owner_actor_id` is the primary owner for a plane.
- Resolvers use screen-instance overrides when present, otherwise shot owner actor defaults.
- `theme_mode` is selected from the resolved theme's available modes through a dropdown, not free text.

## D034 — Chat messages may combine text and media

Status: accepted

Chat message `type` describes the message family, not an exclusive payload switch. A message may contain text and attached media together, such as an image/video with a caption or accompanying text.

Implications:
- `module_instances.content_json.messages[]` may include both `text` and `mediaAssetId`/`media`.
- The Chat editor must expose media fields as optional attachments, not as mutually exclusive alternatives to text.
- Resolved Chat props preserve both `text`/`visibleText` and `media` when both are present.

## D035 — App navigation separates Project hierarchy from Library resources

Status: accepted

The app shell should not present productions, episodes, shots, screen instances, and reusable resources as one flat row of tabs. The primary Project workspace presents the editorial hierarchy as `Production → Episode → Shot → Screen instance`; reusable entities such as actors, themes, devices, media assets, presets, apps, and module theme configs live in a separate Library workspace.

Implications:
- Creating/editing hierarchy records happens in context: episode under selected production, shot under selected episode.
- Reusable resources are still editable, but they are not mixed with the narrative tree.
- The current local UI may expose create actions for productions, episodes, and shots before deeper duplicate/delete/screen-instance workflows are finalized.

## D036 — Module editor hints are registered by module and schema version

Status: accepted

The generic JSON tree editor remains a fallback surface. Module-specific UI hints are registered by `module_id` and `module_schema_version`, so each module can provide friendly labels, widgets, collapsed row summaries, and safe structural affordances without hardcoding module behavior into the generic tree.

Implications:
- `core.chat@1` can describe participants/messages without making the JSON editor Chat-specific.
- Future modules can add their own editor hints independently.
- A specialized module editor may later replace the generic tree for a module while preserving the same canonical JSON storage.

## D037 — Normal startup and validation must not reseed persistent SQLite data

Status: accepted

The local SQLite database is user-editable project state. Normal app startup, browser debug startup, Electron startup, app build, and validation commands must not overwrite an existing edited database. Destructive reseeding is only allowed through an explicit reset command.

Implications:
- `createDatabase` may create/migrate schema but must not seed data.
- `npm run app`, `npm run debug`, `npm run app:build`, and Electron startup must not call destructive seed logic.
- `npm run db:seed` may initialize an empty database but must not overwrite existing productions.
- `npm run db:reset` is the explicit destructive command for restoring the fixture dataset.
- Validation that needs seeded data must use isolated in-memory or temporary SQLite databases.

## D038 — Electron wraps the existing app shell through a safe native boundary

Status: accepted

Electron is introduced as a minimal desktop shell around the existing app/debug workflow. It must not make visual modules or React components access SQLite directly. Native capabilities are exposed only through a preload/context-bridge API.

Implications:
- The existing browser/Vite workflow remains available.
- The Electron development shell uses the same local SQLite persistence path through the existing debug server.
- Renderer Node integration stays disabled and context isolation stays enabled.
- Future file/font pickers should extend the narrow preload API instead of enabling broad Node access.

## D039 — Production-owned workspace

Status: accepted

All authoring data belongs to a production. The app should not present Apps, Shots, or Screen Instances as unrelated global tables. The UI should start from the selected production, then expose production setup/library data and the episode/shot/screen hierarchy underneath it.

Implications:
- The top-level app shell selects one production first.
- Production actions are grouped separately from the episode/shot tree.
- Project navigation is organized as `Episodes -> Shots -> Screens -> per-screen module theme/data`.
- Apps and module theme configs are production-owned library records, not global assets.
- Other tables such as actors, themes, devices, device states, media assets, render presets, and animation presets are production-owned setup data.
- Future production duplication can copy the full production tree and its library/setup records.

Superseded note: the earlier Screen Template layer has been removed from the active architecture. See D042.

## D040 — Screen instances inherit module defaults from screen templates

Status: superseded by D042

Screen Templates are the reusable base layer for Screen Instances. A template can provide module data defaults, behavior defaults, token overrides, and transform defaults. A Screen Instance stores shot-specific content and sparse overrides on top of that template.

Resolution order for a screen instance is:

```text
screen_template.config_json.module_data_json
  → screen_instance.module_data_json

screen_template.config_json.module_config_json
  → screen_instance.module_config_json

screen_template.config_json.module_tokens_override_json
  → screen_instance.module_tokens_override_json

screen_template.config_json.transform_json
  → screen_instance.transform_json
```

Implications:
- The renderer receives the merged/effective screen instance.
- The editor can show inherited template values and let the instance override individual fields.
- Local instance override documents should remain sparse where possible.
- Shot-specific content such as actual message text, timing, actors, and media can still live in `screen_instance.module_data_json`.

## D041 — Structured editors use friendly labels while preserving token paths

Status: accepted

The local app shell should expose app/module/theme JSON through structured editors rather than raw JSON-first forms. Editor tabs and conceptual groups use friendly labels derived from module hints or normalized JSON keys, while the token/path column may keep the internal JSON path visible so users can understand exactly which token or field is being edited.

Implications:
- Tabs and group labels should display `Header Title`, `Chat Bubbles`, and `Message`, not raw keys such as `headerTitle`, `chatBubbles`, or `message`.
- When a field is already inside a conceptual group, its visible property label should not repeat the group prefix. For example, inside `Typography → Header Title`, labels should read `Font family`, `Font size`, `Line height`, and `Font weight`.
- A visual group should only be created when it contains more than one editable row. Single-field groups should render as a normal row to avoid unnecessary UI nesting.
- The internal token/path column can intentionally remain raw, such as `headerTitle.fontSize`, because it identifies the stored token.
- Raw JSON editing is a recovery/fallback surface for invalid JSON or advanced inspection, not the primary editing mode.
- App, Theme, and Module token editors should share the same broad structure: General/Settings for identity, Tokens grouped by concept, and Colors as the central mode-aware color editor.

## D042 — Runtime token inheritance is Theme → App → Screen/Module

Status: accepted

The active architecture removes Screen Templates and does not add App Theme Configs or Screen Presets. Reusable app defaults live directly on `apps.config_json.tokens_json`; reusable module/screen defaults live in `module_theme_configs.tokens_json` scoped by `theme_id + app_id + module_id + module_schema_version`.

Runtime token resolution is:

```text
theme tokens
  → selected theme mode tokens
  → app tokens
  → selected app mode tokens
  → module/screen tokens
  → selected module/screen mode tokens
```

Color values may be mode-aware at App and Module levels. The editor should present mode-aware colors together with Light and Dark columns instead of scattering them through every conceptual group. The resolver collapses to one mode only in the final shot/screen render context.

Implications:
- `screen_instances` reference an `app_id` directly and no longer reference `screen_template_id`.
- `module_theme_configs` reference `app_id` directly.
- Screen/app/module instances do not carry visual overrides for colors, fonts, spacing, radii, shadows, or layout tokens.
- If a user wants to reuse a previous setup as a starting point, they duplicate an existing screen/screen instance and edit the duplicate.
- This is a design-stage breaking change; local development databases can be explicitly reset, but normal app startup must not reseed or overwrite edited data.

## D043 — Design tokens are authored in logical units and scaled for device render space

Status: accepted

Theme, App, and Module numeric visual tokens are authored in logical design units. Device metrics define the mapping to render pixels through `scaleToPixels` or the ratio between `renderSize` and `designSpace`. The resolver scales design-unit tokens before visual modules receive renderable props.

Implications:
- A Chat `fontSize` of `17` in a 430-point iPhone design space resolves to `51px` for a 1290-pixel render size.
- Spacing, padding, line heights, header heights, radii, avatar sizes, tail geometry, and shadow dimensions scale with the device.
- Ratios such as `maxWidthRatio` and frame counts are not scaled. Font weight variants are named font-face selections from the active family and are not scaled.
- The UI should present authored token values in design units; preview/render uses resolved scaled values.

## D044 — App shell uses inspector-first accordions and module-instance content editors

Status: accepted

The local authoring shell is moving toward an inspector-first, Figma-collections-like UI. The left workspace uses accordion sections rather than mixing tabs and trees. The central editor uses accordion cards for major areas and grouped cards for nested token/content concepts.

Shot-specific module payloads are presented as `Module Content` in the module-instance editor. The active design-stage model stores this data in `module_instances.content_json`, with per-instance runtime behavior in `module_instances.behavior_json` and per-frame parameter animation in `module_instances.animation_json`.

Implications:
- `Module Content` is not App data and should not be presented as App-level configuration.
- `Screen Instance` remains responsible for placement, timing, transform, layer order, app/module reference, device/theme/mode context, and shot ownership.
- `Module Instance` remains responsible for the module payload and behavior attached to that screen instance.
- Chat participants and messages are edited through structured content cards, not as raw JSON strings.
- Collapsed content rows should show useful summaries such as participant display name/role or message sender/type/text/timing.
- Major Project/App/Production Data areas use accordion sections with trees inside, avoiding mixed tab/tree metaphors.
- Token and color editors use friendly group labels and logical icons; raw/internal token names remain useful only where they identify a token path.
- Raw JSON remains a fallback/recovery surface, not the normal UI for module content.

## D045 — Module instances own content and behavior; instances do not own visual token overrides

Status: accepted

The design-stage model now has an explicit `module_instances` table. A screen instance may have one or more module instances, ordered by `sort_order`. The primary Chat case uses one module instance per chat screen instance.

Canonical module-instance fields:

```text
module_instances.content_json
module_instances.behavior_json
module_instances.animation_json
module_instances.metadata_json
```

`content_json` stores shot-specific module data such as Chat participants, header copy, messages, timings, and media references. `behavior_json` stores per-shot behavior such as showing the header, showing the keyboard, status bar visibility, initial scroll, and message grouping. `animation_json` stores per-shot module parameter animation: timeline/keyframe changes to values such as header subtitle, message status, or message text.

`animation_json` is intentionally separate from `animation_presets`. Presets remain reserved for reusable visual entrances/exits/transitions if needed. Parameter animation changes what value a module field has on a frame; reveal modes such as `writeDown` change how an existing text value is displayed.

Per-instance visual overrides are removed from the active editor/resolver model. Visual values are reusable defaults resolved from:

```text
Theme → App → Module Theme Config → selected mode
```

Implications:
- The Chat resolver reads `ChatModuleDataSchema` from `module_instances.content_json`.
- The Chat resolver reads `ChatModuleConfigSchema` from `module_instances.behavior_json`.
- Future timeline resolution will read module parameter keyframes from `module_instances.animation_json`.
- `screen_instances.module_data_json`, `screen_instances.module_config_json`, and `screen_instances.module_tokens_override_json` remain only as legacy/migration compatibility columns.
- The UI should not show an Overrides section for Screen Instances or Module Instances.
- Reset/seed paths should create module-instance rows directly.

## D046 — Field descriptors provide canonical UI paths without bloating stored JSON

Status: accepted

Stored JSON should remain compact and domain-oriented. The app should not store labels, canonical paths, section names, or UI metadata beside every value.

Instead, editor code owns a field-descriptor catalog that maps storage paths to canonical conceptual paths:

```text
storage: typography.message.fontSize
canonical: module.design.typography.message.size

storage: module_instances.content_json.messages[].text
canonical: moduleInstance.content.messages[].text

storage: apps.id
canonical: app.general.id
```

Descriptors may also define section, area, group, role, property, label, widget, options, numeric constraints, and collapsed-summary hints.

Implications:
- JSON documents stay light and readable.
- The UI can derive a consistent inspector structure from `section → group → role → property`.
- Scalar SQL fields and JSON fields can share one naming grammar.
- Future UI unification should render fields from descriptors rather than each editor inventing labels, grouping, restore buttons, and row layout separately.

## D047 — Theme colors are mode-aware; Theme tokens keep only non-color shared values

Status: accepted

The Theme editor separates chromatic values from non-color tokens.

Mode-dependent colors live under:

```text
themes.tokens_json.modes.light.*
themes.tokens_json.modes.dark.*
```

and are edited from the `Colors` surface with Light/Dark columns. This includes generic app colors, status/navigation bar colors, and notification colors such as notification background, title color, and body color.

Theme `Tokens` keeps non-color shared values such as:

- font family, sizes, line heights, and weight;
- notification blur;
- spacing;
- radii;
- shadows, with shadow color still edited as an alpha color control because it belongs to the shadow token itself.

Internal helper fields such as `fonts.source` are preserved in stored JSON but hidden from normal authoring UI.

Implications:
- A group like `Notifications` should not duplicate color fields already present in `Colors`.
- Alpha-capable colors use a swatch plus compact alpha editor in the UI, and may render a richer picker overlay.
- Theme fields do not show inherited/override state because Theme is the top of the design-token chain.

## D048 — Generic production UI baseline is closed for this phase

Status: accepted

The generic authoring UI now covers the reusable production-level tables enough to move focus to concrete Apps and Modules.

This phase establishes:

- Devices are production data records with user-facing `name`, `frame_asset_id`, and editable metrics. Manufacturer/model/OS family remain internal implementation fields for now.
- Development seed creates a small baseline device catalog: three iPhone models and three common Android models.
- Shots expose an editable `Episode` dropdown so a duplicated shot can be moved between episodes.
- Render Presets describe output/export behavior, not shot timing or dimensions. Width, height, and fps remain internal SQL placeholders until the schema is simplified; final values come from the Shot/render context.
- Render Presets expose `Format`, codec/image type, and editable `FFmpeg args`. Derived codec/color/quality/export JSON remains stored under the preset.
- The left production tree supports add/duplicate/delete where the operation is currently safe: shots, themes, devices, and render presets. Production and episode duplication remain intentionally disabled until their cascade semantics are designed.

Implications:
- The next phase should focus on app-specific and module-specific editors, starting from the now-stable generic shell.
- Schema cleanup can later remove placeholder render-preset dimensions/fps once render orchestration is mature.

## D049 — Typography is the visible UI nomenclature for type controls

Status: accepted

The editor uses `Typography` as the user-facing card/section label for all type controls. Stored JSON may still use `fonts` for generic base tokens such as family, size, line height, and weight, while `typography` remains available for module-specific text roles such as message, header title, and subtitle.

The UI therefore treats `fonts` as an internal/raw key and presents it as `Typography` in cards, accordions, and grouped field labels. This keeps the interface coherent without forcing a noisy schema rename during the current design phase.

The renderer UI also keeps one shared in-memory font catalog cache per session. All font pickers reuse the same lazy-loaded system-font list and the same in-flight load promise.

## D050 — App does not own status/navigation bar visual tokens

Status: accepted

Apps do not define visual `statusBar` or `navigationBar` tokens. Those values remain Theme-owned and are only concretized once the render context knows the shot owner, device, theme, and mode.

Per-page visibility is still authored at the module instance behavior level, for example `module_instances.behavior_json.showStatusBar`. This keeps page-specific behavior close to the concrete screen while avoiding app-level one-off visual overrides.

The App editor therefore filters inherited Theme status/navigation tokens out of App Tokens and App Colors. App color roles should stay generic or genuinely app-specific, and should not duplicate navigation/status chrome that belongs to Theme/device resolution.

## D051 — App Wallpaper owns its own color UI; App does not expose inherited Shadows

Status: accepted

App wallpaper color is edited only inside the Wallpaper card, even though it is stored as mode-aware data under `modes.light.wallpaper.color` and `modes.dark.wallpaper.color`. The App Colors surface filters the `wallpaper` group to avoid showing the same values twice.

App-level Shadows are hidden from the editor for now. Current shadow tokens are only used for broad Theme-level notification defaults or module-specific component shadows such as Chat bubbles. App should inherit Theme shadows instead of creating production one-offs.

Global Theme `spacing` is currently a broad scale and is not directly consumed by the Chat render path. The Chat render uses module-level layout/message spacing tokens such as `layout.screenGutter` and `messages.spacing`.

## D052 — Chat message direction is explicit content, not inferred from sender

Status: accepted

Chat message layout uses `message.direction` to decide visual alignment:

- `incoming` aligns left;
- `outgoing` aligns right;
- `system` aligns center.

`senderParticipantId` identifies who the message belongs to and may still drive labels, avatars, participant-specific state, and future metadata, but it no longer decides horizontal placement. This lets a conversation represent sent/received/system messages directly without coupling alignment to a participant role heuristic.

## D053 — UI CSS is organized by ownership layers

Status: accepted

The debug UI CSS is currently transitional, with older debug-shell styles and newer inspector-first styles coexisting. New CSS must be placed under an explicit ownership layer: global shell, left browser, central editor, shared field system, JSON/token editors, preview shell, or render surface.

Implications:
- Future cleanup should consolidate selectors by layer rather than relying on late-file overrides.
- Render surface styles must remain separate from preview shell chrome.
- Shared field rows should become the default path for editor inputs.
