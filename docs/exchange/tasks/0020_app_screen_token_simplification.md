# Codex Task 0020 — App/Screen token simplification and Screen Template removal

## Goal

Simplify the authoring and token inheritance model around the product concepts we actually want in MOCKUPS.

The working model should become:

```text
Theme
  → App
  → Screen / Module
  → Module Instance content/behavior/parameter animation at render time
```

Do not add `App Theme Config`.

Remove `Screen Template` as a primary concept. Do not replace it with `Screen Preset`. If a future workflow needs a screen as a starting point, it should duplicate an existing screen/screen instance and then modify it.

This is a design-stage breaking change. Backward compatibility with the current local dev database, previous screen template tables, or old fixture JSON is not required. Prefer a cleaner schema/model over migration complexity.

## Context

The previous ChatGPT-generated task suggested:

```text
Theme
  → App Theme Config
  → Module Theme Config
  → Screen Template
  → Screen Instance
```

That is too many levels for the product direction.

The clarified model is:

### Theme

Defines reusable design-system values and concrete mode values:

- base token definitions;
- light/dark concrete color values;
- generic typography families/styles where truly global;
- generic surfaces/accent values.

### App

Represents a product/app context such as:

- WhatsApp-like;
- Phone;
- Instagram-like;
- Banking app;
- Video player;
- Custom fictional app.

The App owns reusable app-level tokens/defaults inherited by its screens:

- generic app typography defaults that are still app-wide, not module roles;
- app wallpaper/background roles;
- wallpaper supports `solid` or `image`, shared decimal `opacity` (`0–1`), light/dark solid colors, and direct production-relative image media rendered cover/center;
- app accent behavior;
- app icon references;
- shared app surfaces;
- badge styles;
- shared app-specific color roles.

App tokens must stay generic. They should not know about module-specific roles
such as Chat `message`, `headerTitle`, or `headerSubtitle`. A module may derive
its own role defaults from generic Theme/App tokens, then store module-specific
overrides under its own token paths.

For now, use the existing `apps` entity. Do not create `app_theme_configs`.

### Screen / Module

Represents a concrete functional screen/module inside an App:

- `core.chat`;
- `core.lock_screen`;
- `phone.incoming_call`;
- `phone.in_call`;
- `instagram.feed`;
- `instagram.profile`;
- `banking.transfer`.

The screen/module owns module-specific design defaults:

- Chat bubble geometry;
- sent/received bubble token roles;
- message spacing;
- typing cursor;
- media bubble mask;
- call button layout;
- feed post card layout.

### Screen Instance

Represents one screen inside one shot.

It owns structural shot placement:

- frame timing;
- transform/layering;
- owner/device/theme/mode context through the shot/screen context.

It does not own reusable visual overrides. Visual values flow from Theme → App → Screen/Module defaults.

### Module Instance

Represents the concrete module attached to a screen instance in a shot.

It owns:

- shot-specific module content, such as Chat participants, header copy, messages, timing and media;
- per-shot module behavior, such as `showHeader`, `showKeyboard`, `showStatusBar`, `initialScroll`, or `messageGrouping`.

It does not own visual overrides for colors, typography, spacing, radii, shadows, or other reusable design tokens.

## Important color/token clarification

Correction after review: App and Screen/Module levels do need mode-aware color values.

For example, a WhatsApp-like App may override inherited `colors.background` or
`colors.accent` using the same token path. It should not duplicate inherited
roles as `appBackground` / `appAccent`. It should also not own visual status bar
or navigation bar tokens; those are resolved from Theme and device/mode context.
A Chat Screen/Module may define reusable
received/sent bubble colors for light and dark. Those values should apply to
every shot using that app/module unless a lower level overrides them.

The key rule is not "no light/dark before the shot". The key rule is:

```text
preserve both light and dark values until render
  → select/collapse one mode only in the final shot/screen render context
```

Before rendering a shot/screen instance, mode-aware colors are still reusable defaults, not one final selected value. The shot/screen render context chooses the mode because only then do we know:

- shot owner actor;
- device;
- theme;
- selected mode;
- local overrides.

The same shot/screen instance should be renderable in light or dark without duplicating the shot.

Conceptually:

```text
Theme
  defines global/base token values and optional mode-aware values

App
  defines app roles/defaults, including light/dark color values

Screen / Module
  defines screen/module roles/defaults, including light/dark color values

Screen Instance
  provides placement/timing/render context

Module Instance
  provides shot-specific content and behavior

Render context
  selects one mode and resolves roles/tokens into concrete values
```

Preferred conceptual shape for color roles is token-first and mode-aware:

```json
{
  "colors": {
    "receivedBubbleBackground": {
      "light": "#E9E9EB",
      "dark": "#2C2C2E"
    }
  }
}
```

If the implementation keeps the current `modes.light` / `modes.dark` envelope, that is also acceptable in this pass, as long as:

- App color roles can store light and dark values.
- Screen/Module color roles can store light and dark values.
- The resolver does not collapse to a single mode until render/preview resolution.

## Remove Screen Template

Remove `Screen Template` from the primary architecture and implementation.

Requirements:

- remove `screen_templates` from app-shell navigation;
- remove `screen_template_id` from the screen instance editing model;
- remove screen-template inheritance from the resolver;
- remove screen-template docs as a primary concept;
- remove screen-template fixture dependence;
- remove `ScreenTemplateSchema` from active runtime/domain paths if practical;
- remove SQLite `screen_templates` table if practical in this breaking phase;
- do not add `Screen Preset`.

If we later need to use one screen as a base for another, the workflow is:

```text
duplicate an existing screen/screen instance
  → copy its module data/config/overrides
  → preserve references to reusable resources
  → modify the duplicate
```

## Required implementation direction

### Data model

Use existing `apps` records as the app-level token/default layer.

Likely shape:

```json
apps.config_json = {
  "tokens_json": {},
  "metadata_json": {}
}
```

or another clean shape if implementation shows a better fit.

Screen instances/modules must be linkable to an App. Add the smallest clear field needed, likely:

```text
screen_instances.app_id
```

or module/theme config app scoping if required.

Given the current direction, `module_theme_configs` may need to be scoped by:

```text
theme_id + app_id + module_id + module_schema_version
```

Use judgment, but avoid introducing a separate App Theme Config entity.

### Token resolution

Update token resolution to include the App layer:

```text
theme tokens
  → app tokens/defaults
  → module/screen tokens/defaults
  → selected mode collapse for render
```

Mode-specific concrete values should still be selected at render time. Before that point, keep both light and dark values available wherever they exist.

### Design-unit scaling

Theme/App/Module visual tokens are authored in logical design units. The resolver must scale render-unit values using the selected device metrics before passing props to visual modules.

For example, the seeded iPhone fixture uses a 430-point design space and a 1290-pixel render size, so:

```text
fontSize 17 design units
  → scaleToPixels 3
  → resolved render fontSize 51px
```

Scale values such as typography size/lineHeight, header heights, spacing, padding, radii, avatar sizes, tail dimensions, and shadow dimensions.

Do not scale:

- ratios such as `maxWidthRatio`;
- font weight variants, which are named font-face selections such as `Regular`
  or `Semibold`;
- frame counts/durations;
- colors;
- enum/string tokens.

### UI

Update the app shell to reflect the simplified model:

- Apps remain in the Apps workspace.
- Screen Templates are removed.
- Screen Instances edit structural placement/timing/context.
- Module Instances edit module content and behavior.
- App records expose structured app tokens/defaults.
- Module Theme Configs continue to expose module/screen-specific design tokens.

### Colors UI

Mode-dependent colors should be centralized into a `Colors` tab/section at the relevant level instead of being scattered across Header, Chat Bubbles, Cursor, etc.

The `Colors` editor should list all color roles used at that level, grouped by concept such as Header, Chat Bubbles, Cursor, or App Background.

Visible labels should be friendly and should not repeat the active group prefix. The token/path column may remain internal so users can see what token role they are editing.

For mode-aware color values, prefer a compact presentation with both modes visible together when concrete values are being edited:

```text
Property | Token | Light | Dark | Restore
```

However, keep the conceptual rule clear:

- App and Screen/Module levels define reusable token roles/defaults and may define both light and dark color values.
- Render/preview resolution selects the active mode and collapses the inherited mode-aware set to concrete values.

### Duplication

Do not implement duplicate/delete actions in this phase unless explicitly requested later.

Document the policy:

- Episode: create/delete, no duplicate.
- Shot duplication: duplicate child screen instances and their module instances; preserve references to reusable resources; allow moving duplicate to another episode later.
- Screen duplication: a future workflow may duplicate a screen/screen instance directly, but the normal workflow is duplication through shot duplication.
- Production duplication: copy reusable Library resources but not episodes, shots, or screen instances.

## Documentation updates

Update:

- `docs/architecture/01_data_model.md`
- `docs/architecture/05_decisions_log.md`
- `docs/architecture/07_initial_data_schema.md`
- `docs/architecture/08_visual_tokens_layout_contract.md`
- `docs/architecture/09_foundational_module_contracts.md`
- `docs/architecture/10_module_theme_configs.md`
- `PROJECT_STATUS.md`

Add or update response:

```text
docs/exchange/responses/0020_app_screen_token_simplification_response.md
```

## Validation

Run at least:

```text
npm run typecheck
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm run validate:sqlite
npm run app:check
npm run app:build
npm test
npm run remotion:check
npm run electron:check
git diff --check
```

If any command fails because fixtures intentionally changed, update fixtures/contracts and rerun.

## Acceptance criteria

- `Screen Template` is no longer a primary runtime/editor concept.
- No `Screen Preset` replacement is added.
- App records now carry meaningful app-level tokens/defaults.
- Screen/module token resolution inherits from Theme → App → Screen/Module. Screen/Module Instances do not own visual overrides.
- Screen instances no longer depend on `screen_template_id`.
- App shell no longer shows Screen Templates as a main resource.
- Color editing is more centralized and mode-aware where concrete mode values are edited.
- Duplication policy is documented but not implemented.
- Existing preview still works after resetting/reseeding the design-stage database.
- Documentation and response file are updated.

## Implementation status / UI handoff — 2026-06-22

This phase was implemented as a breaking design-stage pass and then iterated through the local app shell UI.

### Architecture/model state

- The active inheritance direction is Theme → App → Screen/Module → Screen Instance.
- `Screen Template` is removed from the active authoring model.
- `Screen Preset` was not introduced.
- `apps.config_json.tokens_json` is the App-level reusable token/default layer.
- `module_theme_configs.tokens_json` is the Screen/Module reusable token/default layer, scoped by app/module/theme/schema.
- `module_instances.content_json` is the shot-specific module content layer.
- `module_instances.behavior_json` is the per-shot module behavior layer.
- `module_instances.animation_json` is the per-shot module parameter animation layer.
- App/screen/module instances no longer expose visual token override layers.
- Mode-aware colors are kept as reusable light/dark values until preview/render resolves one mode.
- Authored numeric design tokens stay in logical design units and are scaled through device metrics at render/preview resolution.

### App-shell UI state

- The UI now follows an inspector-first / Figma-collections-like direction.
- The left workspace no longer mixes top tabs with trees. `Project`, `Apps`, and `Production data` are accordion cards with their trees inside.
- The central editor uses accordion cards instead of permanently stacked panels.
- Token/design subgroups use compact accordion cards with logical icons and friendly labels.
- Color roles are centralized into `Colors` sections where possible and grouped by concept.
- Redundant table-style headers such as `Property / Override` were removed where the context is already obvious.
- Panel backgrounds and borders were softened toward a light inspector UI; preview remains in the right pane.

### Module content editor

The screen-instance editor now labels shot-specific module payloads as `Module Content`.

Important nuance:

```text
Current storage:
  module_instances.content_json

Conceptual ownership:
  module instance content for the module attached to that screen instance
```

This is not App-level data, and it is no longer stored on `screen_instances`.

Per-instance behavior is stored separately:

```text
module_instances.behavior_json
```

Visual design tokens are not stored as per-instance overrides. They come from Theme/App/Module Theme Config resolution.

Parameter animation is reserved as a separate module-instance layer:

```text
module_instances.animation_json
```

This layer stores timeline/keyframe changes to module parameter values. It is intentionally separate from visual `animation_presets`: presets are for entrances, exits, fades, slides, and other reusable visual transitions; `animation_json` is for value changes such as a Chat message text being rewritten, a header subtitle changing, or a message status changing. Text reveal modes such as `writeDown` define how the current text is shown; parameter animation defines what the current text value is at a given frame.

For `core.chat@1`:

- `participants` render as structured content cards, not raw JSON.
- `messages` render as structured content cards, not raw JSON.
- collapsed participants summarize display name, role, and linked actor where available;
- collapsed messages summarize sender, message kind, text/media summary, and frame timing;
- row controls allow add, duplicate, delete, and move for array-like content;
- fields use module editor hints for friendly labels and widgets such as textarea/select.

### Editor behavior fixes from this phase

- JSON strings that contain serialized JSON are unwrapped before structured rendering where safe.
- Root JSON arrays can be edited when a grouped editor is editing a module-data group such as `messages`.
- Group-context hints are applied to nested paths, so editing `messages[].text` still resolves the `messages.[].text` hint even when only the `messages` array is mounted.
- Content inputs/selects/textareas were restyled so editable fields do not look disabled.
- Collapsed content row summaries were restored after the accordion UI pass.

### Validation performed

The following checks were run repeatedly during the phase:

```text
npm run typecheck
npm run debug:build
npm run debug:check
git diff --check
```

Broader validation should still be run before treating this as a release checkpoint:

```text
npm test
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm run validate:sqlite
npm run app:check
npm run app:build
npm run remotion:check
npm run electron:check
```
