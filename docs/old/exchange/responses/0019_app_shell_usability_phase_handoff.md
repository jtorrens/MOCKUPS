# Codex Response 0019 — App shell usability phase handoff

## Summary

This phase significantly improved the local MOCKUPS app shell from a raw debug/admin surface into a more usable production-oriented authoring shell.

The app is still not the final editor, but the current foundation is now much closer to the intended product direction:

- production-first workflow;
- Project hierarchy separated from reusable Library resources;
- screen-instance and screen-template editors organized by conceptual tabs;
- module theme config editing with typed token controls;
- inherited/default/override concepts visible in the UI;
- SQLite persistence protected from accidental reseeding;
- a minimal Electron shell with a narrow native bridge;
- resizable panels and responsive preview fitting.

This handoff describes the current behavior, the key implementation choices, and the known remaining gaps.

## Current app shape

The app shell now starts from a selected production. Everything is scoped to that production.

The main layout is:

```text
Production selector

Left/navigation/editor workspace          Right preview workspace
--------------------------------          -----------------------
Project / Apps / Production data          current resolved preview
selected entity editor                    frame + preview controls
```

The left/editor side and the preview side are resizable. Each major panel has independent scrolling. The preview scales to fit both available width and height.

The preview pane no longer shows the resolver output and adapter input blocks by default. Those are derived/debug outputs and were visually overwhelming during authoring. The current surface prioritizes editing records and seeing the rendered result.

## Project and Library organization

The UI no longer treats productions, shots, screens, apps, and resources as one flat set of tabs.

The left panel has three workspace tabs:

1. `Project`
   - Episodes
   - Shots
   - Screens
   - per-screen module theme configs / data context

2. `Apps`
   - Apps
   - Screen Templates

3. `Production data`
   - Actors
   - Themes
   - Devices
   - Device States
   - Media Assets
   - Render Presets
   - Animation Presets

The current hierarchy direction is:

```text
Production
  → Episode
    → Shot
      → Screen Instance
```

Shots have an owner actor. The owner supplies default device/theme context for the shot unless a screen instance explicitly overrides it.

## Persistence and startup safety

Normal startup must not overwrite edited data.

Implemented/fixed:

- `npm run app`, `npm run debug`, `npm run app:build`, Electron startup, and normal validation do not reseed/overwrite an existing SQLite database.
- `npm run db:seed` is non-destructive when productions already exist.
- `npm run db:reset` is the explicit destructive reset command.
- `validate:sqlite` uses isolated in-memory SQLite rather than the persistent development database.
- `app:persistence-check` verifies scalar and JSON edits survive database reopen and affect preview resolution.

The local database was intentionally reset during this UI phase after permission was given, so the dev fixture data reflects the current shape.

## Electron shell

Added a minimal Electron development shell around the existing app/debug workflow.

Important constraints:

- React/UI still does not access SQLite directly.
- Visual modules still do not access persistence.
- Electron renderer Node integration is disabled.
- Context isolation is enabled.
- Native capabilities are exposed only through `window.mockupsNative`.

Current native bridge:

- `pickFile`
- `listFonts`

`listFonts` uses `queryLocalFonts` when available and a macOS `system_profiler` fallback through Electron. If neither path is available, the UI uses safe fallback font families/styles.

Also added `MOCKUPS App.command` as a clickable launcher from the repo root.

## Screen instance editor

Screen instances are now edited through tabs instead of one long JSON/scalar stack:

```text
General
Content
Behavior
Overrides
```

`General` contains stable screen-instance fields such as shot/template/module/timing/transform context.

`Content` exposes module data. For `core.chat@1`, it currently uses nested tabs such as:

- Participants
- Header
- Messages

`Behavior` edits `module_config_json`.

`Overrides` edits `module_tokens_override_json` as a sparse local override document.

Inherited values from the screen template are shown where available. Local differences are marked in amber, and restore removes sparse instance overrides when absence means "inherit".

## Screen template editor

Screen Templates now use a screen/module-like editing pattern, but without shot-specific content.

Tabs:

```text
General
Behavior
Overrides
```

`General` edits template metadata and `default_props_json`.

`Behavior` edits reusable module behavior defaults stored in:

```text
screen_templates.config_json.module_config_json
```

`Overrides` edits reusable template token bindings/fixed overrides stored in:

```text
screen_templates.config_json.module_tokens_override_json
```

Conceptually:

- empty template override means "resolve this token later from the shot owner/device/theme";
- filled template override means "freeze this value for inheriting screen instances";
- screen instances may still override template values locally.

Screen Templates do not expose shot-specific data such as Chat messages, message timings, concrete actors, or media content.

## Module theme config editor

Module Theme Configs now have a more purposeful editor:

```text
Design
Theme
```

`Design` exposes module-specific tokens from:

```text
module_theme_configs.tokens_json
```

For `core.chat@1`, design tabs follow token groups such as:

- Layout
- Header
- Messages
- Typography
- Chat Bubbles
- Avatars
- Radii
- Cursor

`Theme` edits stable module-theme-config identity fields and `metadata_json` notes/default-token information.

The Design and Template Override editors intentionally use the same conceptual token editor, so reusable module defaults, template overrides, and instance overrides feel consistent.

## Token/override editor conventions

The structured token editor now shows:

```text
Property | Token / inherited | Override
```

Rules:

- `Property` is friendly and compact.
- `Token / inherited` intentionally keeps internal token/path names, because that helps identify the actual stored token.
- If a value is already inside a conceptual group, the property label does not repeat the group name.
  - Example: inside `Typography → Header Title`, rows show `Font family`, `Font size`, `Line height`, `Font weight`.
- Internal tabs/groups use friendly names:
  - `headerTitle` → `Header Title`
  - `headerSubtitle` → `Header Subtitle`
  - `chatBubbles` → `Chat Bubbles`
- A conceptual group is only rendered as a visual group when it has more than one row.
- Single-field groups render as loose rows to avoid unnecessary UI nesting.

This is currently a UI convention, not a storage rename. The JSON token paths remain stable.

## Typed controls

Override/value controls now respect hints and inferred token types:

- boolean → true/false select;
- number → numeric input;
- color → native color control plus hex field;
- text → text input;
- textarea → larger multiline control;
- enum/select → dropdown;
- font family → installed-font/fallback dropdown;
- font style/weight → family-specific styles when available, fallback styles otherwise.

Font styles are intended to be the actual values exposed by the font family (`Regular`, `Medium`, `Bold`, `Condensed Bold`, etc.), not generic numeric weights when the runtime can discover styles.

## JSON/raw behavior

The app no longer presents `Tree` / `Raw JSON` buttons as the normal primary UI.

The structured editor is the default surface. Raw JSON remains a fallback/recovery path when JSON is invalid or for advanced debugging surfaces that still use raw editors.

Manual object key add/rename/delete remains hidden by default. Array structural controls remain available where they are useful, especially line-like module data such as Chat participants/messages.

## Screen-template inheritance

Screen instances now inherit reusable defaults from their selected template.

Merge order:

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

The resolver receives the merged/effective screen instance. The editor can still show inherited parent values and local differences.

## Documentation updated

Updated:

- `PROJECT_STATUS.md`
  - Captures the current app-shell state, Electron/persistence status, structured editor improvements, and next-step options.
- `docs/architecture/05_decisions_log.md`
  - Added D041 for structured editor label/group conventions.
- `docs/architecture/10_module_theme_configs.md`
  - Added editor placement, template override, grouping, token/path, and font/control conventions.

Earlier docs already included D037–D040 for non-destructive SQLite startup, Electron shell boundary, production-owned workspace/screen templates, and screen-template inheritance.

## Validation run

Validated after this phase with:

```text
npm run typecheck
npm run app:build
npm run electron:check
git diff --check
```

Previous persistence validation for this phase:

```text
npm run app:persistence-check
```

## Known remaining gaps

Important things not completed yet:

- screen-instance creation workflow from template/module;
- production duplicate/delete/cascade policy;
- episode/shot/screen deep duplicate policy;
- native asset registration/file picker flow;
- final asset manager;
- specialized Chat content editor;
- final typography system/render-font verification;
- final export/render pipeline;
- Electron menu/package/installer polish;
- advanced module-specific editors beyond hint-driven structured JSON;
- schema migrations for any future real JSON key renames.

## Recommended next steps

Recommended next decision point:

1. Implement screen-instance creation from selected shot + selected screen template.
2. Then implement specialized Chat content editing for Participants/Header/Messages, replacing the generic JSON editor where it starts to get awkward.
3. In parallel or after that, add native asset registration/file picker through the existing Electron bridge.

Avoid implementing production duplicate/delete until the copy policy is explicit. It needs decisions for child IDs, references, asset reuse/copying, module JSON references, screen templates, themes, devices, and shots.
