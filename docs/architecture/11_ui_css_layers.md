# UI CSS layers map

This document maps the current debug UI CSS ownership. It is intentionally a stabilization aid, not a final design-system spec.

The app is in a transitional state: older dark debug-shell styles still exist, then newer inspector-first light styles override them later in `src/debug-ui/styles.css`. Future cleanup should consolidate these layers gradually, with visual checks after each small move.

## Layer ownership

### 1. Global shell

Owns document defaults, full-window layout, resize gutters, and the main three zones:

- `.core-app-shell`
- `.left-app-panel`
- `.authoring-workspace`
- `.navigation-rail`
- `.editor-workspace`
- `.right-preview-shell`
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

Owns preview panel chrome, selection controls, status chips, PNG render feedback, and container sizing:

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

The preview subsystem is grouped under `src/debug-ui/preview/` and exported through `src/debug-ui/preview/index.ts`. The right panel shell is implemented by `RightPreviewShell`, and `RightPreviewShellProps` in `src/debug-ui/preview/types.ts` is the app-facing preview contract. The render surface is implemented by `src/debug-ui/preview/RenderSurface.tsx`. It may scale for browser display and draw the optional phone frame overlay, but it must not add padding/border to the renderable coordinate system.

## Known cascade risk areas

These selectors currently appear in multiple historical blocks and should be consolidated carefully:

- `:root`
- `body`
- `.right-preview-shell`
- `.preview-viewport-host`
- `.preview-viewport`
- `.preview-scale`
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
