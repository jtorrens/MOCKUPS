# Lock Screen

Status: active module on the module resolver -> renderable route.

Source of truth: `src/desktop-preview/lockScreenModuleContract.ts`,
`lockScreenModuleResolver.ts`, `lockScreenModuleRenderable.ts` and
`spikes/desktop-editor-shell/Data/SpikeDatabase.LockScreenModule.cs`.

## Composition

Lock Screen owns four ordered visual layers:

1. the selected Actor wallpaper;
2. one concrete Component Stack Variant;
3. the selected Status Bar Variant when visible;
4. the selected Navigation Bar Variant when visible.

The Stack is the content container for future Lock Screen components. Status
Bar, Navigation Bar and Stack are explicit embedded slots containing
`presetId` plus local `overrides`.

Component Stack keeps its own public runtime contract. At the Lock Screen
composition boundary, `stackInputs` binds that contract as module Variant data:
the Stack scalar inputs and ordered `items` collection are authored in the
normal Lock Screen card. Any child runtime field may be promoted explicitly to
the Lock Screen runtime through Runtime Input Forwarding. Lock Screen does not
copy a component-specific field catalog or require a new Stack Variant for each
composition.

## Runtime inputs

- `actor`: selects the Actor whose wallpaper contract supplies the background;
- `showStatusBar`: independently includes/excludes Status Bar;
- `showNavigationBar`: independently includes/excludes Navigation Bar;
- dynamically forwarded Stack or nested item fields chosen by the module
  designer.

`sizingMode`, `startGapToken`, `endGapToken` and `items` remain runtime inputs of
Component Stack itself. They are not automatically runtime inputs of Lock
Screen. The parent binds them as Variant values unless the designer activates
Forward. Design Test Values and module instances consume only the resulting
effective Lock Screen runtime contract.

## Layout

Wallpaper covers the whole screen. Visible bars keep their normal screen-edge
placement. Lock Screen calculates the remaining box between them and supplies
that box to Component Stack through the generic child preview-frame helper.
Consequently `fill` means the available content region, not the whole device
behind the bars. When either bar is hidden, Stack receives the released space.

Bars render after Stack so system chrome stays above any child visual overflow.
The module root clips the complete composition to the device screen.

## Wallpaper contract

Wallpaper belongs to Actor, not to Lock Screen or the System app. The preview
resolves the current Actor record before rendering and passes its complete
wallpaper contract (`kind`, `opacity`, Light/Dark images and Light/Dark fallback
colors). Persisted resolved Actor objects are removed during normalization so
stale partial previews cannot shadow the authoritative Actor record.

If an image path for the active appearance mode is empty, wallpaper rendering
uses that mode's Actor background color. Missing required current-model fields
remain errors; Lock Screen adds no opacity, image or color fallback.

## Preview boundary

Lock Screen resolver owns visibility and concrete Variant references. The module
renderable owns layer order and the available Stack frame. Component Stack owns
its child collection and deterministic placement. Each selected child resolver
receives the requested frame state before preview. The registry only routes,
and the generic renderer receives final groups, boxes, images and surfaces.
