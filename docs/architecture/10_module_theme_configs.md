# Module theme configs

## Purpose

MOCKUPS now separates reusable visual language from module-specific design defaults:

```text
themes.tokens_json
  → shared/global theme language

apps.config_json.tokens_json
  → app-level defaults for one app/product context

module_theme_configs.tokens_json
  → module-specific defaults for one theme + app + module + schema version
```

This keeps the global Theme tab from becoming the place where every internal Chat setting is edited. A global theme may own broad typography, base colors, surfaces, accents, shared status-bar appearance, and light/dark base modes. An App owns generic product-level defaults such as solid/image wallpaper roles with decimal `0–1` opacity, accent colors, direct app icon media/crop metadata, shared app surfaces, and app-wide typography tokens. Chat-specific values such as message/header typography roles, bubble geometry, tails, message spacing, cursor behavior, and chat header defaults belong to the Chat module theme config.

## Data model

The initial table is:

```text
module_theme_configs
--------------------
id
production_id
theme_id
app_id
module_id
module_schema_version
name
tokens_json
metadata_json
```

The lookup key used by the resolver is:

```text
theme_id + app_id + module_id + module_schema_version
```

There may be more than one named config for a theme/app/module in the future. The current implementation seeds one default Chat config for `theme_ios_light` + `app_messages` + `core.chat` schema version `1`.

## Token resolution order

For a screen instance, module tokens resolve in this order:

```text
1. global theme tokens
2. selected global theme mode overrides
3. app tokens for app_id
4. selected app mode overrides, if present
5. module theme config tokens for theme_id + app_id + module_id + module_schema_version
6. selected module theme config mode overrides, if present
```

The result is a render-ready module token object passed to the visual module pipeline. Visual modules still do not access SQLite, repositories, or raw persistence rows.

## Light/dark mode

The selected mode comes from `screen_instances.theme_mode` when present, otherwise from `themes.tokens_json.defaultMode`, otherwise `light`.

Themes, apps, and module theme configs may define:

```json
{
  "modes": {
    "light": {},
    "dark": {}
  }
}
```

Global mode overrides are merged before app tokens. App mode overrides are merged before module config tokens. Module mode overrides are merged after base module config tokens. The editor should keep light and dark values visible together for color roles; the resolver selects one mode only at render time.

## Global theme tokens

Global theme tokens should contain reusable language shared across modules, such as:

- installed font selection and general typography values;
- base light/dark colors;
- background/text/accent colors;
- shared status-bar appearance;
- broad spacing/radius/shadow scales;
- shared notification/surface tokens where they are not module internals.

## Module theme config tokens

Module theme configs should contain values that only make sense inside one module. For Chat, this includes:

- `layout.screenGutter`;
- `header` height/background/separator;
- `messages.spacing`;
- `messages.groupSpacing`;
- `typography` for message text and Chat header text;
- `chatBubbles` colors, padding, width ratio, shadow, and tail geometry;
- `header` avatar size/mask/border/shadow and header icon layout;
- `chatBubbles` message avatar size/gap;
- `radii.bubble`;
- `cursor` typing/reveal tokens;
- future chat media message defaults.

These values may reuse global color/typography concepts, but the stored data shape remains ordinary JSON.

Mode-aware module colors should be centralized in a `Colors` editor with Light and Dark columns, rather than scattered through Header, Bubbles, Cursor, and other design groups. Non-color values remain in conceptual groups such as Layout, Header, Typography, Chat Bubbles, Avatars, Radii, and Cursor.

## Editor placement

Module theme configs are reusable Library resources. They are not part of the Project hierarchy of episodes/shots/screens, though a selected screen instance resolves through one of them based on its theme/app/module/schema context.

The structured editor can edit module config tokens, but module-specific editor hints should provide friendly labels, widgets, collapsed row summaries, and grouping cues. For example, `core.chat@1` exposes Chat typography and message/header token labels without requiring the generic JSON tree to know Chat semantics directly.

Current editor conventions:

- Tabs and groups use friendly names, e.g. `Header Title`, `Chat Bubbles`, and `Message`.
- The visible property label omits the active group prefix, e.g. inside `Typography → Header Title`, the rows show `Font family`, `Font size`, `Line height`, and `Font weight`.
- The token/path column intentionally keeps the internal token path so the user can see which stored token is being changed.
- A conceptual group is only rendered as a section when it has multiple rows; single-field groups stay as direct rows.
- Raw JSON is treated as a fallback/recovery path, not the primary authoring surface.
- Color values use color controls plus a hex field; font family and weight-variant controls prefer installed system fonts when the runtime can discover them.

## Module instances and visual override policy

Module instances own per-shot module content, behavior, and parameter animation:

```text
module_instances.content_json
module_instances.behavior_json
module_instances.animation_json
```

They do not own reusable visual design defaults or sparse local visual exceptions. `animation_json` changes module parameter values over time; it is not a visual token override layer. Colors, typography, spacing, radii, shadows, layout, and other visual design values are edited at Theme/App/Module Theme Config level.

Screen Template overrides are no longer an active layer. If a future workflow needs to use one screen as a starting point, the user duplicates an existing screen/screen instance and edits the duplicate.

## Override detection

The app API provides inherited parent JSON for fields that support inheritance:

- `module_theme_configs.tokens_json` receives inherited theme + app tokens plus its seeded module defaults as its parent.

The JSON editor compares paths using deep JSON equality:

```text
local value !== inherited value
  → mark row amber
  → show Restore inherited
```

Primitive values use exact equality. Objects and arrays use deep equality. Color normalization is intentionally not implemented yet; for example, `#fff` and `#FFFFFF` may compare as different.

## Restore behavior

Restore behavior depends on whether the edited JSON is a sparse override document or an explicit defaults document.

- `module_theme_configs.tokens_json` restores by writing the inherited module default value at the edited path. These documents are explicit module defaults, so removing a key could remove the editable default itself.

## Why module configs are separate

Module-specific design values should be edited in a module context because they are not globally meaningful. A Chat bubble tail is part of Chat; a lock-screen notification card, call button grid, or home-screen app icon grid will each have their own module-specific design model. Keeping those tokens in module theme configs avoids polluting the global theme and makes future specialized module editors cleaner.
