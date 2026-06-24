# UI CSS layers map

This document maps the current debug UI CSS ownership. It is intentionally a stabilization aid, not a final design-system spec.

The app is in a transitional state: older dark debug-shell styles still exist, then newer inspector-first light styles override them later in `src/debug-ui/styles.css`. The preview subsystem has started moving out of the monolithic stylesheet into `src/debug-ui/preview/preview.css`. Future cleanup should consolidate these layers gradually, with visual checks after each small move.

## Layer ownership

### Shared UI tokens

The transitional global stylesheet exposes a small set of CSS variables in `:root` for values that must stay consistent across left, central, and preview panels:

- `--app-panel-padding`: outer panel gutter.
- `--accordion-chevron-size`: closed accordion chevron size.
- `--accordion-chevron-open-size`: open accordion chevron size.
- `--accordion-chevron-weight`: accordion chevron weight.
- `--card-border`: shared top-level card border.
- `--card-radius`: shared top-level card radius.
- `--card-background`: shared top-level card background.
- `--card-shadow`: shared top-level card shadow.
- `--card-header-background`: card header background.
- `--card-header-title-color`: card header title color.
- `--card-header-summary-color`: card header summary color.
- `--card-header-icon-color`: card header glyph color.
- `--card-header-chevron-color`: card header chevron color.
- `--card-body-border`: card body separator border.
- `--card-header-icon-column`: card header icon column width.
- `--card-header-gap`: card header grid gap.
- `--card-header-min-height`: card header minimum height.
- `--card-header-padding-block`: card header vertical padding.
- `--card-header-padding-inline`: card header horizontal padding.
- `--card-header-icon-size`: card header icon frame size.
- `--card-header-icon-radius`: card header icon frame radius.
- `--card-header-icon-border`: card header icon frame border.
- `--card-header-icon-background`: card header icon frame background.
- `--card-header-icon-font-size`: card header glyph size.
- `--card-header-icon-font-weight`: card header glyph weight.
- `--card-header-font-family`: card header title and summary family.
- `--card-header-title-font-size`: card header title size.
- `--card-header-title-font-weight`: card header title weight.
- `--card-header-summary-font-size`: card header summary size.
- `--card-header-summary-font-weight`: card header summary weight.

New panel/card work should prefer these tokens over local hardcoded values.

### 1. Global shell

Owns document defaults, full-window layout, resize gutters, and the main three zones:

- `.core-app-shell`
- `.left-app-panel`
- `.authoring-workspace`
- `.navigation-rail`
- `.editor-workspace`
- `.panel-resizer`

This layer must not style individual record fields or renderable content.

### 2. Left project browser

Owns production/app/data navigation, tree cards, tree rows, and left-side action buttons:

- `.project-browser`
- `.project-tree-view`
- `.project-tree`
- `.tree-node`
- `.tree-children`
- `.workspace-accordion-*` when used by the left browser
- `.record-list`

This layer may indicate selection/editing state, but should not own central editor card styles.

### 3. Central record editor

Owns the editor card, sections, subsection cards, tabs/accordions, and direct field stacks:

- `.record-editor`
- `.editor-section-*`
- `.editor-subsection-*`
- `.field-stack`
- `.nested-editor-stack`
- `.flat-json-*`

This layer should call into the shared field system for actual rows and controls.

### 4. Shared field system

Owns one-row field layout, labels, controls, restore buttons, read-only state, dirty/override state, and consistent spacing:

- `.inspector-field-row`
- `.inspector-field-label`
- `.inspector-field-control`
- `.inspector-field-restore`
- `.app-field`
- `.content-field-row`
- `.state-override`
- `.json-override`

New editors should prefer this layer instead of custom row markup.

### 5. JSON and token editors

Owns generic tree editing, token overrides, mode-aware color rows, color pickers, and module-content JSON structures:

- `.json-tree-*`
- `.json-array-*`
- `.json-primitive-row`
- `.token-override-*`
- `.mode-color-*`
- `.color-value-editor`

This layer should eventually reuse the shared field system more strictly.

### 6. Preview shell

Owns preview panel chrome, selection controls, status chips, PNG render feedback, and container sizing. These rules live in `src/debug-ui/preview/preview.css`, imported by `RightPreviewShell`.

- `.right-preview-shell`
- `.preview-header-card`
- `.preview-options-*`
- `.preview-header-meta`
- `.preview-header-controls`
- `.preview-context`
- `.preview-render-*`
- `.preview-output-*`
- `.preview-message-*`
- `.preview-viewport-host`

This layer measures and presents the preview but must not mutate render coordinates.

### 7. Render surface

Owns only the visual surface that displays the resolved renderable tree:

- `.preview-viewport`
- `.preview-scale`
- `.preview-phone-frame`

The preview subsystem is grouped under `src/debug-ui/preview/` and exported through `src/debug-ui/preview/index.ts`. The right panel shell is implemented by `RightPreviewShell`, `src/debug-ui/preview/preview.css` owns its CSS, and `RightPreviewShellProps` in `src/debug-ui/preview/types.ts` is the app-facing preview contract. The render surface is implemented by `src/debug-ui/preview/RenderSurface.tsx`. It may scale for browser display and draw the optional phone frame overlay, but it must not add padding/border to the renderable coordinate system.

## Known cascade risk areas

These selectors currently appear in multiple historical blocks and should be consolidated carefully:

- `:root`
- `body`
- `.project-tree-view`
- `.record-list`
- `.record-editor`
- `.json-tree-node`
- `.workspace-accordion-card`
- `.inspector-field-row`

The current rule is: do not remove or reorder these selectors casually. First identify which layer owns the final visual behavior, then move one small selector group at a time.

## Working rule for future UI CSS

New CSS must be added under an explicit layer comment in `styles.css`.

Avoid using late-file overrides as the default strategy. If a selector needs `!important`, document why in the nearby block or prefer fixing the component markup so it can use the shared field/card system.
