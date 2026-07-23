# Visual tokens and layout contract

This contract assigns every visual/layout value to one canonical source. Resolvers combine those sources into render-ready props; visual modules consume them without repository access.

## Ownership rules

| Canonical source | Owns | Must not own |
| --- | --- | --- |
| `theme.tokens_json` | Reusable global visual language, mode variants, typography/font selection, base colors/surfaces/accent, shared status bar defaults, broad spacing/radius/shadow scales | Device geometry, live device state, shot timing, narrative content, module-internal component design |
| `apps.config_json.tokens_json` | Reusable app-level defaults such as generic app typography, solid/image wallpaper roles with `0–1` opacity, app icon references, shared surfaces, and inherited color overrides using the same token path | Shot content, module-specific roles such as Chat message/header tokens, visual status/navigation bar tokens, duplicated inherited roles such as `appBackground` / `appAccent`, module-specific internals, device geometry, or per-instance exceptions |
| `module_theme_configs.tokens_json` | Module-specific defaults for one theme + app + module + schema version, such as Chat bubble geometry, message spacing, Chat typography, header defaults, cursor behavior, and module-specific mode colors | Shot content, device geometry, live state, or one-off screen instance exceptions |
| `device.metrics_json` | Logical design space, internal pixel render size, geometry, and scale mapping | Actor content, component styling, external plate placement |
| `device_states.state_json` | Live status values displayed by the device | Base geometry or reusable style |
| `module_instances.content_json` | Shot-specific module content such as Chat header, messages, actor references, timings, and media references | Reusable visual defaults, device geometry, or render output |
| `module_instances.behavior_json` | Module-owned behavior/visibility for one module instance | Shot placement, canonical theme values, reusable design defaults, or device geometry |
| `module_instances.animation_json` | Per-frame changes to module parameter values | Visual token overrides, reusable transition presets, or base content |
| Resolved props | Final values needed by a module for its frame | Database references that still require lookup |
| Renderable metadata | Diagnostics, approximation warnings, provenance, and debug timing | Required canonical style/layout configuration |

## Theme tokens

Reusable global visual values belong in `theme.tokens_json`:

- `fonts`: installed family, text sizes, line heights, and one generic named
  `weight` variant.
- `colors`: screen and text colors.
- `statusBar`: foreground/background and icon scale.
- `notifications`: non-color notification defaults such as blur, radius, and
  related component behavior. Notification background/title/body colors live in
  `modes.light.notifications` and `modes.dark.notifications` and are edited from
  the Colors surface.

Tokens use logical design-space units unless explicitly documented otherwise. Ratio values such as `maxWidthRatio` use the inclusive range `0..1`. A theme is reusable across shots and actors.

A theme may define base tokens, `modes.light`, `modes.dark`, and `defaultMode`. It also stores the selected installed font family and a generic named weight variant. Resolution order is:

```text
theme base tokens
  → theme modes[selected theme_mode]
  → app base tokens
  → app modes[selected theme_mode]
  → module theme config base tokens
  → module theme config modes[selected theme_mode]
```

The theme and app editors should select from approved production font families.
Importing a family may use installed system fonts as a source, but the approved
family is copied into the production root and registered in `production_fonts`.
Weight fields are named variants exposed by that approved family, for example
`Regular`, `Semibold`, `Bold`, or a variable-font entry. If a family changes and
a previous variant no longer exists, the editor falls back to the first
available variant from the copied family files.

Color authoring is moving toward the same approved-resource model. The
production-scoped `palette_colors` table stores primitive RGB colors only:
`token -> #RRGGBB`. Theme, app, and module tokens remain the semantic layer and
should reference palette tokens rather than raw RGB values once the color-picker
UI is migrated. Alpha is not part of the primitive palette; transparent fields
store a palette token plus a separate numeric `0–1` alpha.

Theme colors may already store palette tokens directly. The resolver replaces
matching palette token strings with concrete HEX values after merging
theme/app/module tokens and before visual scaling/rendering, so preview and
render modules still receive paintable CSS color values.

Before wider token conversion, persisted JSON colors are normalized so any
direct physical HEX/RGB/RGBA color is converted to the closest primitive
palette token. RGBA values are represented as a palette color token plus a
numeric alpha in the `0–1` range, keeping transparency outside the primitive
palette.

Mode-aware color values may exist in Theme, App, and Module defaults. The editor should keep both light and dark columns available at authoring time; the resolver collapses to one mode only for preview/render. Module-specific values belong in `module_theme_configs.tokens_json`. For Chat, this includes message list gutter, header height/background/separator/icon/avatar defaults, message spacing/grouping distances, message/header typography, bubble colors/padding/radius/tails/shadows, bubble avatar size/gap, cursor behavior, and future chat media defaults.

Before a module receives renderable props, the resolver scales design-unit token values to the selected device render space using `device.metrics_json.scaleToPixels`, or the render/design width ratio when needed. For the seeded iPhone fixture, 430 logical points render at 1290 pixels, so a Chat message `fontSize` of `17` resolves to `51px`. Numeric values that are not design units, such as `maxWidthRatio` and frame counts, are not scaled. Font weight variants are named font-face selections and are not scaled.

## Device metrics and state

`device.metrics_json` owns logical `designSpace`, pixel `renderSize`, `scaleToPixels`, `canvas`, `screen`, `viewport`, `safeArea`, the status-bar area, notch/dynamic-island geometry, `cornerRadius`, `pixelRatio`, and `defaultScreenScale`. Layout occurs in design space; the renderer maps logical units to internal pixels. Output is normally the device render size. Render-preset output scale is distinct from both design-space mapping and external video placement.

`device_states.state_json` owns live display state: time/date, battery level and charging, signal strength, network label, Wi-Fi enabled/icon state, focus mode, orientation, and lock state.

## Module-instance content and behavior

`module_instances.content_json` contains shot-specific module content. For Chat, this includes header data, messages, actor IDs, direction, text/media references, and frame timings.

`module_instances.behavior_json` contains module behavior such as `showHeader`, `showKeyboard`, `showStatusBar`, `initialScroll`, `messageGrouping`, and debug flags. `module_instances.animation_json` is reserved for module parameter keyframes, such as changing text/status/subtitle values over time. `core.chat` reads only these canonical module-instance sources for content/behavior; it does not merge legacy `props_json`.

Known values are normalized to camelCase by resolvers. Precedence is:

```text
theme base tokens
  → selected theme mode tokens
  → app base tokens
  → selected app mode tokens
  → module theme config base tokens
  → selected module theme config mode tokens
```

Device metrics and live device state remain separate inputs and are not replaced by theme tokens.

## Resolved props

Resolved module input contains render-ready camelCase values derived from selected module JSON, theme/mode/overrides, device/state, owner actor, assets/icons, events, and local frame. Visual modules do not fetch storage rows, resolve file paths, or choose icon variants.

`ResolvedChatScreenProps.theme` carries the relevant `fonts`, `colors`, `layout`, `header`, `messages`, `typography`, `chatBubbles`, `statusBar`, and `cursor` groups. A legacy/resolved `avatars` compatibility group may still be present while older consumers are cleaned up, but canonical Chat avatar defaults now live under `header` for the header avatar and under `chatBubbles` for message avatars. `ResolvedMessageBubbleProps` carries the concrete style and layout values required by the atomic module, including font family, size, weight, line height, tail geometry, shadow, avatar sizing/gap, and maximum width.

## Renderable metadata

Renderable metadata may report token source paths, local timing, estimated line counts, approximate measurement strategy, debug bounds, and layout warnings. It must not be the only location of a value required to render correctly.

## Still implementation-defined

Text measurement currently uses an isolated average-glyph-width estimate. The contract now names two modes: approximate renderer-agnostic measurement and final renderer-assisted measurement shared by preview and export. Manual line breaks must be preserved before automatic wrapping. Exact font metrics, shaping, collision policy, bubble-tail construction, and icon assets remain future work.

## Future text reveal plans

Message reveal currently uses deterministic simple write-on timing. The general
animation model will animate a text field from ordinary consecutive keyframes:
the resolver finds their common grapheme prefix, deletes the old suffix and
writes the new suffix over the keyframe interval. That covers natural edits
such as `Hola Jorge` → `Hola Teresa` without storing per-character actions or
rigid SQL columns. A future flexible plan/preset is only for additional
semantics such as deliberate pauses or other non-derived typing behavior.

The resolver/module host supplies timings and configuration; Chat owns interpretation of its animation plan and emits render-ready `visibleText`, optional `cursorState`, and diagnostic typing metadata. Reveal should operate on grapheme clusters where possible. `MessageBubble` only renders the resolved result.

## Media and icon placement

Content media is an asset reference plus:

```text
media window: logical width, height, offsetX, offsetY
asset transform: ratio scale, translateX/Y, rotationDegrees
```

This supports framing/cropping without reducing the contract to cover/contain. Heavy assets remain external by preferably project-relative URI. Reusable and one-off content share asset records but carry usage/scope metadata. OS/app icons use theme/OS/mode-aware icon tokens resolved by the host. Final rendering defaults to a clear error when a required asset/icon cannot be resolved.
