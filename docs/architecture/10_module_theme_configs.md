# Module theme configs

## Purpose

MOCKUPS now separates reusable visual language from module-specific design defaults:

```text
themes.tokens_json
  → shared/global theme language

module_theme_configs.tokens_json
  → module-specific defaults for one theme + module + schema version

screen_instances.module_tokens_override_json
  → local exceptions for one screen instance
```

This keeps the global Theme tab from becoming the place where every internal Chat setting is edited. A global theme may own typography, base colors, surfaces, accents, shared status-bar appearance, and light/dark base modes. Chat-specific values such as bubble geometry, tails, message spacing, cursor behavior, and chat header defaults belong to the Chat module theme config.

## Data model

The initial table is:

```text
module_theme_configs
--------------------
id
production_id
theme_id
module_id
module_schema_version
name
tokens_json
metadata_json
```

The lookup key used by the resolver is:

```text
theme_id + module_id + module_schema_version
```

There may be more than one named config for a theme/module in the future. The current implementation seeds one default Chat config for `theme_ios_light` + `core.chat` schema version `1`.

## Token resolution order

For a screen instance, module tokens resolve in this order:

```text
1. global theme tokens
2. selected global theme mode overrides
3. module theme config tokens for theme_id + module_id + module_schema_version
4. selected module theme config mode overrides, if present
5. screen_instance.module_tokens_override_json
```

The result is a render-ready module token object passed to the visual module pipeline. Visual modules still do not access SQLite, repositories, or raw persistence rows.

## Light/dark mode

The selected mode comes from `screen_instances.theme_mode` when present, otherwise from `themes.tokens_json.defaultMode`, otherwise `light`.

Both global themes and module theme configs may define:

```json
{
  "modes": {
    "light": {},
    "dark": {}
  }
}
```

Global mode overrides are merged before module config tokens. Module mode overrides are merged after base module config tokens.

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
- `avatars` chat sizes/gaps;
- `radii.bubble`;
- `cursor` typing/reveal tokens;
- future chat media message defaults.

These values may reuse global color/typography concepts, but the stored data shape remains ordinary JSON.

## Editor placement

Module theme configs are reusable Library resources. They are not part of the Project hierarchy of episodes/shots/screens, though a selected screen instance resolves through one of them based on its theme/module/schema context.

The generic JSON tree can edit module config tokens, but module-specific editor hints should provide friendly labels and widgets. For example, `core.chat@1` exposes Chat typography and message/header token labels without requiring the generic JSON tree to know Chat semantics directly.

## Screen instance overrides

`screen_instances.module_tokens_override_json` stores local exceptions for one instance of one module. It should stay sparse. If a value is removed from the override JSON, resolution falls back to the inherited global/module value.

## Override detection

The app API provides inherited parent JSON for fields that support inheritance:

- `module_theme_configs.tokens_json` receives resolved global theme tokens plus its seeded module defaults as its parent.
- `screen_instances.module_tokens_override_json` receives resolved global + module theme config tokens as its parent.

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
- `screen_instances.module_tokens_override_json` restores by removing the local key and pruning empty object containers. These documents are sparse exceptions, so absence cleanly means “inherit parent/default”.

## Why module configs are separate

Module-specific design values should be edited in a module context because they are not globally meaningful. A Chat bubble tail is part of Chat; a lock-screen notification card, call button grid, or home-screen app icon grid will each have their own module-specific design model. Keeping those tokens in module theme configs avoids polluting the global theme and makes future specialized module editors cleaner.
