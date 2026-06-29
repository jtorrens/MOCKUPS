# Theme editor dictionary audit

Status: Phase 1 started — General fields catalogued

This document records the first field-by-field audit for the `themes` editor.
It follows the mandatory cleanup rule: inventory fields first, then map them to
`FieldDefinition` → `ValueRegistry` → `ValueKindControlRegistry`, and only then
change UI.

## Current editor structure

Theme editing is split across:

- `src/debug-ui/editors/ThemeRecordEditor.tsx`
- `src/debug-ui/editors/ThemeEditor.tsx`
- `src/debug-ui/editors/ThemeFields.tsx`
- `src/debug-ui/components/json-editor/ModeColorEditor.tsx`
- `src/debug-ui/field-descriptors/themeDescriptors.ts`

The visual shell is already using shared editor primitives:

- `EditorHeader`
- `EditorSections`
- `EditorSectionCard`
- `EditorSectionButton`
- `EditorSubsectionAccordion`
- `InspectorFieldRow`

So the main remaining issue is not card chrome. The remaining issue is field
ownership and control selection.

## Summary

The Theme editor is functional but not yet fully dictionary-driven.

There is now a dedicated `THEME_FIELDS` catalog for Theme SQL/general fields.
Theme token JSON fields still come from a mix of:

- generic SQL table metadata from `debugService.ts`;
- a small legacy descriptor list in `themeDescriptors.ts`;
- ad-hoc token normalization and hand-authored controls in `ThemeFields.tsx`;
- heuristic color discovery in `ModeColorEditor.tsx`.

This is acceptable as current state, but the cleanup target is to move Theme
fields to explicit field definitions before changing their UI further.

## General card

| Field | Storage | Current control | Dictionary status | Target |
| --- | --- | --- | --- | --- |
| `id` | SQL column | generic readonly field | `theme.id`, kind `text` | complete for General |
| `production_id` | SQL column | dictionary relation/readonly field | `theme.productionId`, kind `recordReference` | complete for General, hidden/internal policy still editor-owned |
| `name` | SQL column | dictionary text field | `theme.name`, kind `text` | complete for General |
| `family` | SQL column | dictionary enum/readonly field | `theme.family`, kind `enum` | complete for General |
| `icon_theme_id` | SQL column | dictionary record select | `theme.iconThemeId`, kind `recordReference` | complete for General |
| `status_bar_id` | SQL column | dictionary record select | `theme.statusBarId`, kind `recordReference` | complete for General |
| `navigation_bar_id` | SQL column | dictionary record select | `theme.navigationBarId`, kind `recordReference` | complete for General |
| `version` | SQL column | dictionary text field | `theme.version`, kind `text` | complete for General |
| `tokens_json` | SQL JSON column | theme-specific editors | `theme.tokens`, kind `jsonObject` | parent field catalogued; child token fields still pending |

### Notes

The relation controls for icon theme, status bar and navigation bar now get
their relation metadata from field definitions:

- `tableId`
- `labelColumn`
- `allowEmpty`

`RecordFieldRenderer` still owns generic relation option lookup and production
filtering. That is acceptable because it is reusable editor infrastructure, not
Theme-specific UI.

## Tokens card

The Tokens card currently renders all object token groups except:

- `statusBar`
- `navigationBar`
- `keyboard`
- `cursor`
- `neutralTint`
- `surfaceRelief`

Most token groups are still edited through `JsonTreeEditor` via
`renderField(tokensField, rawOverride)`.

### Current partially described fields

`themeDescriptors.ts` currently describes only:

| Field | Storage | Widget | Status |
| --- | --- | --- | --- |
| body family | `fonts.family` | `font` | legacy descriptor, not FieldDefinition |
| body size | `fonts.bodySize` | `number` | legacy descriptor, not FieldDefinition |
| body line height | `fonts.bodyLineHeight` | `number` | legacy descriptor, not FieldDefinition |
| body weight | `fonts.fontWeight` | `select` | legacy descriptor, should become typography field |
| background light | `modes.light.colors.background` | `color` | legacy descriptor |
| background dark | `modes.dark.colors.background` | `color` | legacy descriptor |

### Target

Create a dedicated Theme token field catalog. Suggested structure:

```text
THEME_FIELDS
  theme.fonts.family
  theme.fonts.weight
  theme.fonts.style
  theme.fonts.bodySize
  theme.fonts.bodyLineHeight
  theme.neutralTint.hueDeg
  theme.neutralTint.saturation
  theme.cursor.width
  theme.cursor.blinkFrames
  theme.cursor.color.light
  theme.cursor.color.dark
  theme.surfaceRelief.default.angleDeg
  theme.surfaceRelief.default.extension
  theme.surfaceRelief.default.spread
  theme.surfaceRelief.default.upperIntensity
  theme.surfaceRelief.default.lowerIntensity
  theme.statusBar.type
  theme.statusBar.iconScale
  theme.navigationBar.type
  theme.navigationBar.iconScale
  theme.keyboard.background.light
  theme.keyboard.background.dark
  theme.keyboard.keyBackground.light
  theme.keyboard.keyBackground.dark
  ...
```

Typography triples should use the shared typography control/class:

- `fontFamily`
- `fontWeight`
- `fontStyle`

They should not be rebuilt locally in the Theme editor.

## Colors card

The Colors card is rendered by `ModeColorEditor`.

Current behavior:

- discovers color-looking paths by heuristic;
- groups paths by top-level group;
- uses `ColorValueEditor`;
- supports palette-token colors and palette+alpha colors;
- supports light/dark columns;
- supports restore when inherited values exist.

Current limitation:

- the editor discovers fields rather than receiving a list of
  `FieldDefinition`s;
- color role type is inferred from path names;
- alpha support is inferred from path names;
- semantic theme color tokens and palette color tokens are not cleanly separated
  at the field-definition layer.

Target:

- color roles should be explicit theme field definitions;
- each color field should say whether it stores:
  - `paletteColorToken`;
  - `themeColorToken`;
  - `paletteColorToken + alpha`;
- `ModeColorEditor` can remain as the reusable light/dark layout component, but
  its fields should come from dictionary metadata, not path heuristics.

## Theme-specific manual editors

### `NeutralTintGroupEditor`

Fields:

- `neutralTint.hueDeg`
- `neutralTint.saturation`

Current controls:

- custom hue slider plus number input;
- raw number input for saturation.

Dictionary status:

- missing field definitions.

Target:

- add `theme.neutralTint.hueDeg`, kind `integer` or future `hueDegrees`;
- add `theme.neutralTint.saturation`, kind `alpha` or future `unitInterval`;
- register hue slider as the canonical control for hue-like fields.

### `ThemeCursorGroupEditor`

Fields:

- `cursor.width`
- `cursor.blinkFrames`
- mode color currently lives under `modes.light.cursor.color` /
  `modes.dark.cursor.color`.

Current controls:

- hand-authored number inputs.

Dictionary status:

- missing field definitions.

Target:

- `theme.cursor.width`, kind `integer`;
- `theme.cursor.blinkFrames`, kind `integer`;
- cursor colors handled by Colors card through explicit field definitions.

### `ThemeSurfaceReliefGroupEditor`

Fields:

- `surfaceRelief.default.angleDeg`
- `surfaceRelief.default.extension`
- `surfaceRelief.default.spread`
- `surfaceRelief.default.upperIntensity`
- `surfaceRelief.default.lowerIntensity`

Current controls:

- hand-authored number inputs.

Dictionary status:

- missing field definitions.

Target:

- add field definitions for all surface relief values;
- consider future value kinds:
  - `angleDegrees`;
  - `signedIntensity`;
  - `decimal`;
- use existing number control until specialized controls are justified.

### `ThemeChromeGroupEditor`

Fields:

- `statusBar.type`
- `statusBar.iconScale`
- `navigationBar.type`
- `navigationBar.iconScale`

Current controls:

- hand-authored `select`;
- hand-authored number input.

Dictionary status:

- missing field definitions.

Target:

- add `enum` field definitions with options;
- add `decimal` field definitions for `iconScale`.

## Recommended next cleanup phases

### Phase 1 — Theme field catalog

Created `src/domain/fields/themeFields.ts` with:

- SQL column definitions;
- parent `tokens_json` definition;
- column bindings for Theme general fields.

This should mirror the actor cleanup pattern.

### Phase 2 — Generic dictionary rendering for Theme general fields

Extend `RecordFieldRenderer.dictionaryFieldForColumn` beyond Actors, or replace
it with a registry keyed by table id.

Target:

```text
themes.name → THEME_FIELDS.name → text control
themes.icon_theme_id → THEME_FIELDS.iconThemeId → recordSelect control
```

### Phase 3 — Replace `themeDescriptors.ts`

Convert the legacy descriptors into field definitions or derive descriptors from
field definitions.

Do not keep two independent lists long-term.

### Phase 4 — Convert manual ThemeFields controls

For each Theme subsection:

1. list fields;
2. map to `THEME_FIELDS`;
3. assert expected controls like Actor does;
4. keep only layout/update logic locally.

### Phase 5 — Make `ModeColorEditor` dictionary-fed

Keep the useful reusable layout, but feed it explicit field definitions rather
than discovered color paths.

## Audit result

Theme editor encapsulation state:

- visual card shell: good;
- shared color picker usage: good but still path-heuristic;
- SQL general fields: need Theme field definitions;
- tokens JSON fields: need Theme field definitions;
- manual controls: need replacement with dictionary-selected controls;
- resolver path: functional, but should be audited after field definitions are
  added.

No UI rewrite should happen before Phase 1 is complete.
