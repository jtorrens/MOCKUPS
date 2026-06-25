# Editor encapsulation contract

Status: accepted

This document fixes the debug-editor UI architecture rules after the first
encapsulation pass. The goal is to keep the interface extensible while avoiding
the old pattern where each editor introduced its own visual language.

## Core rule

Editors own data shape and workflow. Shared editor UI owns visual language.

That means an editor may decide which groups, cards, fields, rows, and domain
actions exist, but it should not invent local card, field, button, icon, or
accordion styling unless the interaction is truly unique.

## Layer responsibilities

### `src/debug-ui/editor-ui`

This is the common editor component layer. It owns reusable visual primitives:

- editor headers;
- section and subsection cards;
- section buttons and chevrons;
- glyph/icon containers;
- deferred inputs;
- shared row/field composition primitives;
- common compact controls when they are reusable.

These components should depend on shared CSS tokens. They should not know about
Chat, Themes, Devices, Actors, Icon Themes, Status Bars, or specific production
tables.

### `src/debug-ui/editors`

This layer owns editor-specific structure and behavior:

- which cards appear for a record;
- which fields are visible or internal;
- how table-specific add, duplicate, delete, and confirm flows work;
- how module-specific config JSON is grouped;
- how domain options are computed for dropdowns.

Editor-specific files may compose shared UI components, but should not create a
new styling system. If a visual pattern appears reusable, promote it to
`editor-ui` first.

### `src/debug-ui/editors/<module-or-feature>`

Module and feature editors own their own data semantics. For example, Chat can
have custom editors for messages, header content, text input config, keyboard
config, and future bubble/content controls.

These editors should still use shared card, field, row, icon, and control
components. Local code should express module meaning, not duplicate visual
chrome.

### `src/visual/modules`

Visual modules own preview/render drawing. They consume resolved props and
return renderable nodes. They do not access databases and do not depend on
debug-editor UI components.

If a value affects final output, it must flow through schema/resolver/resolved
props into the visual module. It should not be inferred from editor UI state.

### `src/domain`

Domain schemas and resolvers own persistent contracts, default resolution,
inheritance, and render-ready props.

They are the only place where editor-authored JSON becomes runtime meaning.

## Tokens and CSS

Shared visual concepts must use shared tokens:

- card background, border, radius, and shadow;
- card header title, summary, icon, and chevron colors;
- input background, border, focus, and text colors;
- readonly field presentation;
- selected/edited/override state;
- panel backgrounds and gutters;
- scrollbars;
- action/glyph button sizing and color.

Local CSS may exist only for layout that is genuinely specific to an editor's
data, such as a status-bar item grid or an icon-theme token table. Even there,
colors, borders, radii, shadows, and typography should read from common tokens.

## Data placement rules

Use these rules before adding or moving fields:

- Production/table identity fields live in SQL columns and are usually hidden or
  readonly in the editor.
- Shot-specific module content lives in `module_instances.content_json`.
- Per-shot behavior lives in `module_instances.behavior_json`.
- General module defaults that all instances inherit live in module config/theme
  data, not in per-instance content.
- Theme/system visual values live in Theme or Module Theme Config tokens.
- Visual modules receive resolved values only; they never read editor state or
  database records.

When adding a new capability, first decide whether it is content, behavior,
module default, theme token, device/state data, or renderer logic. Only then add
UI.

## New UI work checklist

Before implementing a new editor feature:

1. Reuse `editor-ui` primitives if an existing card/field/row pattern fits.
2. If a new visual pattern is needed, add it as a reusable primitive first.
3. Keep editor-specific code focused on structure, labels, options, and domain
   updates.
4. Use shared tokens for colors, borders, shadows, typography, icons, and
   spacing.
5. Verify that preview/render receives the value through resolver props, not
   through editor-only state.
6. Avoid moving values between instance and module levels unless the data rule
   above explicitly calls for it.

## Current accepted direction

The left panel, central editor, and right preview may remain separate
components, but analogous concepts should share tokens and primitives. The UI
does not need to be object-oriented in the classical sense; the equivalent in
this React codebase is a clear set of reusable components, explicit domain
editors, and a narrow resolver/render contract.
