# Visual tokens and layout contract

This contract assigns every visual/layout value to one canonical source. Resolvers combine those sources into render-ready props; visual modules consume them without repository access.

## Ownership rules

| Canonical source | Owns | Must not own |
| --- | --- | --- |
| `theme.tokens_json` | Reusable global visual language, mode variants, typography/font selection, base colors/surfaces/accent, shared status bar defaults, broad spacing/radius/shadow scales | Device geometry, live device state, shot timing, narrative content, module-internal component design |
| `module_theme_configs.tokens_json` | Module-specific defaults for one theme + module + schema version, such as Chat bubble geometry, message spacing, Chat typography, header defaults, cursor behavior, and module-specific colors | Shot content, device geometry, live state, or one-off screen instance exceptions |
| `device.metrics_json` | Logical design space, internal pixel render size, geometry, and scale mapping | Actor content, component styling, external plate placement |
| `device_states.state_json` | Live status values displayed by the device | Base geometry or reusable style |
| `screen_instance.module_config_json` | Module-owned behavior/visibility for one screen instance | Shot content, canonical theme values, or device geometry |
| `screen_instance.module_tokens_override_json` | Intentional local visual exceptions | Reusable design defaults |
| Resolved props | Final values needed by a module for its frame | Database references that still require lookup |
| Renderable metadata | Diagnostics, approximation warnings, provenance, and debug timing | Required canonical style/layout configuration |

## Theme tokens

Reusable global visual values belong in `theme.tokens_json`:

- `fonts`: families, sizes, weights, and line heights.
- `colors`: screen and text colors.
- `statusBar`: foreground/background and icon scale.
- `notifications`: background, blur, radius, and related component defaults.

Tokens use logical design-space units. Ratio values such as `maxWidthRatio` use the inclusive range `0..1`. A theme is reusable across shots and actors.

A theme may define base tokens, `modes.light`, `modes.dark`, and `defaultMode`. It also stores the selected installed font family/style/weight. Resolution order is:

```text
theme base tokens
  → theme modes[selected theme_mode]
  → screen_instance.module_tokens_override_json
```

The future theme editor selects these installed fonts through a font picker. There is no production font whitelist/table; the project assumes selected fonts are installed on the render machines.

Module-specific values belong in `module_theme_configs.tokens_json`. For Chat, this includes message list gutter, header height/background/separator, message spacing/grouping distances, message/header typography, bubble colors/padding/radius/tails/shadows, avatar sizes/gaps, cursor behavior, and future chat media defaults.

## Device metrics and state

`device.metrics_json` owns logical `designSpace`, pixel `renderSize`, `scaleToPixels`, `canvas`, `screen`, `viewport`, `safeArea`, the status-bar area, notch/dynamic-island geometry, `cornerRadius`, `pixelRatio`, and `defaultScreenScale`. Layout occurs in design space; the renderer maps logical units to internal pixels. Output is normally the device render size. Render-preset output scale is distinct from both design-space mapping and external video placement.

`device_states.state_json` owns live display state: time/date, battery level and charging, signal strength, network label, Wi-Fi enabled/icon state, focus mode, orientation, and lock state.

## Screen-instance module config and overrides

`screen_instance.module_config_json` contains module behavior such as `showHeader`, `showKeyboard`, `initialScroll`, `messageGrouping`, debug flags, and module defaults. `module_tokens_override_json` holds local gutter/header/bubble exceptions. `core.chat` reads only these canonical sources; it does not merge legacy `props_json`.

Known values are normalized to camelCase by resolvers. Precedence is:

```text
theme base tokens
  → selected theme mode tokens
  → module theme config base tokens
  → selected module theme config mode tokens
  → screen_instance.module_tokens_override_json
```

Device metrics and live device state remain separate inputs and are not replaced by theme tokens.

## Resolved props

Resolved module input contains render-ready camelCase values derived from selected module JSON, theme/mode/overrides, device/state, owner actor, assets/icons, events, and local frame. Visual modules do not fetch storage rows, resolve file paths, or choose icon variants.

`ResolvedChatScreenProps.theme` carries the relevant `fonts`, `colors`, `layout`, `header`, `messages`, `typography`, `chatBubbles`, `avatars`, `statusBar`, and `cursor` groups. `ResolvedMessageBubbleProps` carries the concrete style and layout values required by the atomic module, including font family, size, weight, line height, tail geometry, shadow, avatar sizing/gap, and maximum width.

## Renderable metadata

Renderable metadata may report token source paths, local timing, estimated line counts, approximate measurement strategy, debug bounds, and layout warnings. It must not be the only location of a value required to render correctly.

## Still implementation-defined

Text measurement currently uses an isolated average-glyph-width estimate. The contract now names two modes: approximate renderer-agnostic measurement and final renderer-assisted measurement shared by preview and export. Manual line breaks must be preserved before automatic wrapping. Exact font metrics, shaping, collision policy, bubble-tail construction, and icon assets remain future work.

## Future text reveal plans

Message reveal currently uses deterministic simple write-on timing. A future advanced plan may express actions such as type → pause → delete → type again in flexible JSON, preferably `messages.animation_override_json` or a versioned animation/text-reveal preset. It must not add rigid SQL columns per action.

The resolver/module host supplies timings and configuration; Chat owns interpretation of its animation plan and emits render-ready `visibleText`, optional `cursorState`, and diagnostic typing metadata. Reveal should operate on grapheme clusters where possible. `MessageBubble` only renders the resolved result.

## Media and icon placement

Content media is an asset reference plus:

```text
media window: logical width, height, offsetX, offsetY
asset transform: ratio scale, translateX/Y, rotationDegrees
```

This supports framing/cropping without reducing the contract to cover/contain. Heavy assets remain external by preferably project-relative URI. Reusable and one-off content share asset records but carry usage/scope metadata. OS/app icons use theme/OS/mode-aware icon tokens resolved by the host. Final rendering defaults to a clear error when a required asset/icon cannot be resolved.
