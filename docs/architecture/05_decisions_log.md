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
- Themes select installed font family/style/weight; no production font whitelist/table is introduced.

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
- `core.chat` schema version 1 uses `screen_instances.module_data_json` as its canonical runtime source.
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

Status: accepted

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

Module theme config JSON may reference, copy, or override compatible global theme values. Resolution merges global theme tokens, selected global mode, module theme config tokens, selected module mode, then instance overrides.

Implications:
- Modules receive a render-ready merged token object.
- Module configs can stay compact where global tokens are sufficient.
- Light/dark behavior can exist at both global and module-specific levels.

## D029 — Screen instance token overrides remain local exceptions

Status: accepted

`screen_instances.module_tokens_override_json` remains the canonical place for one-off per-shot/per-instance visual exceptions.

Implications:
- Local overrides do not belong in the reusable global theme or reusable module theme config.
- Removing an override should return the screen instance to inherited global/module defaults.

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
- `module_data_json.messages[]` may include both `text` and `mediaAssetId`/`media`.
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

## D039 — Production-owned workspace and abstract screen templates

Status: accepted

All authoring data belongs to a production. The app should not present Apps, Screen Templates, Shots, or Screen Instances as unrelated global tables. The UI should start from the selected production, then expose production setup/library data and the episode/shot/screen hierarchy underneath it.

Implications:
- The top-level app shell selects one production first.
- Production actions are grouped separately from the episode/shot tree.
- Project navigation is organized as `Episodes -> Shots -> Screens -> per-screen module theme/data`.
- Apps and Screen Templates are production-owned library records, not global assets.
- Other tables such as actors, themes, devices, device states, media assets, render presets, and animation presets are production-owned setup data.
- Future production duplication can copy the full production tree and its library/setup records.

Screen Templates remain abstract. They should store token bindings and optional fixed overrides, not resolved pixel/color values tied to a specific actor, device, or theme mode.

Implications:
- A template property can expose a token label plus an empty override field.
- Empty override means "resolve this token later from the shot owner/device/theme context".
- A filled override means "use this fixed value for every inheriting screen instance unless the instance overrides it again".
- Screen Instances can show inherited template values and, once attached to a shot, may also show resolved render values.

## D040 — Screen instances inherit module defaults from screen templates

Status: accepted

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

The local app shell should expose module/template/theme JSON through structured editors rather than raw JSON-first forms. Editor tabs and conceptual groups use friendly labels derived from module hints or normalized JSON keys, while the token/path column may keep the internal JSON path visible so users can understand exactly which token or field is being edited.

Implications:
- Tabs and group labels should display `Header Title`, `Chat Bubbles`, and `Message`, not raw keys such as `headerTitle`, `chatBubbles`, or `message`.
- When a field is already inside a conceptual group, its visible property label should not repeat the group prefix. For example, inside `Typography → Header Title`, labels should read `Font family`, `Font size`, `Line height`, and `Font weight`.
- A visual group should only be created when it contains more than one editable row. Single-field groups should render as a normal row to avoid unnecessary UI nesting.
- The internal token/path column can intentionally remain raw, such as `headerTitle.fontSize`, because it identifies the stored token.
- Raw JSON editing is a recovery/fallback surface for invalid JSON or advanced inspection, not the primary editing mode.
