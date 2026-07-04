# Avalonia Visual IR integration plan

Status: Avalonia branch planning document.

This document defines how the desktop editor branch should adopt Visual IR
without copying the React renderer into Avalonia and without letting preview
logic leak back into editors or `MainWindow`.

Source references:

- `/Volumes/SD_02/PROYECTOS/MOCKUPS_REACT/docs/architecture/02_render_architecture.md`
- `/Volumes/SD_02/PROYECTOS/MOCKUPS_REACT/docs/architecture/15_target_system_architecture.md`
- `/Volumes/SD_02/PROYECTOS/MOCKUPS_REACT/docs/architecture/17_base_routines.md`
- `/Volumes/SD_02/PROYECTOS/MOCKUPS_REACT/docs/architecture/18_visual_ir_preview_contract.md`
- `/Volumes/SD_02/PROYECTOS/MOCKUPS_REACT/docs/architecture/19_avalonia_visual_ir_handoff.md`
- `/Volumes/SD_02/PROYECTOS/MOCKUPS_REACT/src/visual/ir/types.ts`
- `/Volumes/SD_02/PROYECTOS/MOCKUPS_REACT/src/visual/ir/bridge/renderableToVisualIr.ts`
- `/Volumes/SD_02/PROYECTOS/MOCKUPS_REACT/src/visual/adapters/react/VisualIrReactAdapter.tsx`

## Decision

Avalonia should consume Visual IR as a renderer contract.

The Avalonia renderer must not consume SQLite rows, editor field definitions,
component class records, themes, screen modules, chat/status/header concepts or
legacy React renderables directly.

Target flow:

```text
SQLite / editor data
  -> resolver services
  -> resolved model
  -> frame model
  -> Visual IR document
  -> renderer adapter
```

Temporary compatibility flow:

```text
legacy renderable/design preview payload
  -> isolated bridge
  -> Visual IR document
  -> Avalonia or web adapter
```

The temporary bridge is allowed to know legacy names. The renderer is not.

## Current Avalonia state

The current desktop editor has two preview panes:

- `RuntimeWebPreviewPane`: placeholder/runtime shell.
- `DesignWebPreviewPane`: HTML preview fed through
  `DesignPreviewPayloadFactory` and `WebDesignPreviewRenderer`.

`DesignPreviewPayloadFactory` still assembles domain-specific preview payloads:

- `statusBar`
- `navigationBar`
- `componentClass`
- theme tokens and palette colors
- icon theme asset root and mapping JSON

This is acceptable as a transitional preview bridge, but it is not the target
renderer boundary. It should be renamed or wrapped conceptually as a legacy
design-preview bridge when Visual IR enters the branch.

## Target project structure

Recommended Avalonia-side folders:

```text
spikes/desktop-editor-shell/VisualIr/
  VisualIrDocument.cs
  VisualIrNodes.cs
  VisualIrPaint.cs
  VisualIrResources.cs
  VisualIrValidation.cs

spikes/desktop-editor-shell/Preview/
  IVisualIrFrameProvider.cs
  IVisualIrRenderer.cs
  VisualIrPreviewController.cs

spikes/desktop-editor-shell/Preview/Bridges/
  DesignPreviewToVisualIrBridge.cs
  LegacyRenderableToVisualIrBridge.cs

spikes/desktop-editor-shell/Preview/Avalonia/
  AvaloniaVisualIrRenderer.cs
  AvaloniaVisualIrPaintMapper.cs
  AvaloniaVisualIrTextMapper.cs
  AvaloniaVisualIrMediaResolver.cs

spikes/desktop-editor-shell/Preview/Web/
  WebVisualIrRenderer.cs
```

The important boundary is not the exact folder name. The important boundary is:

```text
Bridge/resolver may know app concepts.
Renderer may know only Visual IR primitives.
```

## Visual IR model to port first

Port the contract from `src/visual/ir/types.ts` almost one-to-one:

- `VisualIrDocument`
- `VisualIrViewport`
- `VisualIrResources`
- `VisualIrNode`
- `VisualIrGroupNode`
- `VisualIrRectNode`
- `VisualIrEllipseNode`
- `VisualIrPathNode`
- `VisualIrTextNode`
- `VisualIrImageNode`
- `VisualIrVideoNode`
- `VisualIrSvgNode`
- paint, stroke, clip, transform, effects and source types

Do not port React adapter decisions into the contract.

The C# model should be serializable, immutable where practical, and easy to
validate. It should not reference Avalonia types such as `Brush`, `Control`,
`Geometry`, `Image`, `Typeface` or `Canvas`.

## Renderer primitives

The renderer knows only these node kinds:

- `group`
- `rect`
- `ellipse`
- `path`
- `text`
- `image`
- `video`
- `svg`

Forbidden renderer concepts:

- `chat`
- `message`
- `bubble`
- `statusBar`
- `navigationBar`
- `keyboard`
- `avatar`
- `componentClass`
- `themeToken`
- `paletteColor`
- `SQLite`
- editor field ids

If one of those names is needed, the work belongs in a bridge, resolver or base
routine before the Visual IR boundary.

## Renderer adapter contract

Initial interface:

```csharp
internal interface IVisualIrRenderer
{
    Control Render(VisualIrDocument document, VisualIrRenderOptions options);
}
```

Initial frame provider:

```csharp
internal interface IVisualIrFrameProvider
{
    VisualIrDocument GetFrame(VisualIrFrameRequest request);
}
```

`VisualIrFrameRequest` may include selected frame, mode, device id and preview
flags, but the returned document must already contain concrete visual values.

The renderer should not call database services from inside `Render`.

## Resolved model and frame model

Avalonia should preserve the React architecture split between resolved model
and frame model.

The resolved model answers:

```text
What are the final values after ownership, inheritance and resource resolution?
```

Examples:

- concrete theme colors for the active mode;
- resolved font family, weight, style, size and line height;
- component class properties plus local overrides;
- resolved media/icon/font references;
- device metrics and orientation;
- design-space values converted to render-space values.

The frame model answers:

```text
At frame N, what exactly should be visible?
```

Examples:

- write-on text at the current frame;
- current subtitle/message animation state;
- current video frame or audio progress;
- current status text, ticks, battery and signal values.

Preview may change frame selection frequently. It should not re-resolve the
entire database for values that only require frame evaluation.

## Preview shell boundaries

Preview chrome is outside the render document.

The preview shell may own:

- selected frame;
- screen navigation;
- zoom;
- device-frame overlay;
- reference overlay controls;
- debug comparison mode;
- render-current-frame command.

The preview shell must not own:

- layout;
- theme token resolution;
- component inheritance;
- media scaling;
- keyboard/status behavior;
- Visual IR coordinate-space changes.

Device frames, borders, shadows, debug overlays and preview controls must never
affect module layout or Visual IR bounds. They are display overlays around the
document, not part of the document.

## Units and output scale

Stored visual values are design-space values until the resolver says otherwise.

Target flow:

```text
stored design value
  -> device scale / render-space conversion in resolver
  -> Visual IR bounds/style values
  -> output scale in renderer/export
```

Visual modules and renderers should not guess whether a value has already been
scaled. If a legacy binding mixes scaled and unscaled values, the adapter must
make that conversion explicitly before Visual IR.

## Resource and packaging rules

Visual IR resources should be concrete and portable:

- production media paths should be stored relative to the production media
  root;
- file dialogs may use absolute paths temporarily, but stored production data
  should not;
- icon tokens resolve through the active icon theme before render;
- portable payloads should inline icon SVG markup where possible, rather than
  requiring the renderer to read the icon theme directory;
- fonts should be resolved to approved production font families and resource
  face sources before render.

Render reproducibility should eventually be captured in a render manifest with
production, shot, screen order/durations, theme, icon theme, device, render
preset, output scale, source media and frame range. The immediate rule is
simpler: preview and render must use the same resolved/frame model.

## Resolver boundaries

Resolvers should be small and composable:

- resource resolvers: palette, theme colors, fonts, icon themes, media paths;
- field resolvers: inherited/concrete/default values;
- component resolvers: component classes plus overrides;
- module resolvers: module-specific runtime props;
- frame evaluators: animation, write-on, video/audio progress;
- screen resolvers: device metrics, orientation, ordering and transitions.

Resolvers may validate stored JSON, resolve references, compute timing and scale
design-space values. They should not draw, call preview APIs, contain editor UI
layout, or carry long-term legacy fallbacks.

## Transitional bridge

The current `DesignPreviewPayloadFactory` can become the first bridge source:

```text
DesignPreviewPayload
  -> DesignPreviewToVisualIrBridge
  -> VisualIrDocument
  -> renderer adapter
```

This lets us migrate preview behavior in small steps:

1. Keep existing web design preview working.
2. Add Visual IR model classes.
3. Add a bridge for one preview kind, starting with status bar or navigation
   bar.
4. Add a debug renderer that lists/draws primitive bounds.
5. Add real Avalonia rendering for primitive nodes.
6. Switch one design preview pane from legacy HTML payload to Visual IR.

Do not start with full chat screen preview. Status/navigation/component design
previews are the safest first targets because they already have small payloads.

## Compound geometry rules

Avalonia must follow the same geometry rules as the React handoff:

- Generated battery, signal, navigation buttons and bubble chrome arrive as
  finished SVG.
- Icon-set glyphs arrive as normalized SVG glyphs inside explicit bounds.
- Wallpaper/media arrive as explicit `image` or `video` nodes, not CSS-like
  background fields.
- Bubble contents can be normal IR nodes, but bubble body + tail + border is a
  single composed SVG node.
- Shape-aware shadows for SVG/image/media/avatar-like objects use alpha/drop
  shadow semantics, not rectangular box shadows.

Avalonia should place these objects. It should not recreate their internal
geometry.

## Status, navigation and icon SVG parity

The React implementation reached parity between the legacy renderer and the IR
path by sharing the same SVG preparation before the renderer boundary.
Avalonia must preserve that split.

Generated status and navigation items are not icon-set glyphs:

- status battery, status signal and generated navigation buttons are semantic
  resolved items;
- their runtime values are resolved before Visual IR is created;
- they are expanded through the generated SVG primitive contract;
- Visual IR receives final `svg.markup`, final bounds and `fit`;
- the renderer places the SVG and applies object-level effects only.

The renderer must not infer whether these SVGs are fill-based, stroke-based,
multi-part, padded, overflowing or semantically a battery/signal/button. Those
decisions belong to the generated primitive or bridge.

The exact React anchors are:

- `/Volumes/SD_02/PROYECTOS/MOCKUPS_REACT/src/base-routines/generatedSvgPrimitives.ts`
  - `generatedStatusSignalSvg`
  - `generatedStatusBatterySvg`
  - `generatedNavigationButtonSvg`
- `/Volumes/SD_02/PROYECTOS/MOCKUPS_REACT/src/visual/ir/bridge/renderableToVisualIr.ts`
  - `statusSignalNode`
  - `statusBatteryNode`
  - `navigationItemNode`
  - `statusItemWidth`
  - `inlineSvgMarkup`
  - `iconGlyphNode`

Status battery has an important occupied-size rule. The battery terminal is
part of the SVG geometry and may visually overflow the logical item width. Do
not turn that protrusion into layout gap or renderer padding.

Status icon-token items follow the icon glyph path instead:

```text
status item container -> square icon box -> normalized inline SVG glyph
```

The glyph path normalizes user/icon-set SVG before IR:

- remove XML and doctype declarations;
- preserve real `fill="none"` and `stroke="none"`;
- replace concrete fill/stroke colors with `currentColor`;
- remove root width/height/style/preserveAspectRatio;
- set root SVG to `width="100%"`, `height="100%"`,
  `preserveAspectRatio="xMidYMid meet"` and visible overflow;
- pass tint as an IR paint, not as renderer-specific icon logic.

Avalonia should port or faithfully reimplement these preparation routines in a
bridge/base-routine layer. It should not duplicate this logic inside
`AvaloniaVisualIrRenderer`.

## Base routines to preserve

Port or reimplement these contracts before depending on them in renderer code:

- `generatedSvgPrimitives.ts`: outputs finished SVG chrome.
- `svgReplacement.ts`: validates and normalizes user-provided SVG.
- `iconThemeFileOperations.ts`: owns icon-set copy/rename safety.
- `previewReference.ts`: owns reference media overlay/frame math.
- `liveTextFilter.ts`: owns normalized filtering behavior.

The renderer must not own these concerns. It may consume their outputs.

## Phased implementation

### Phase 1: contract only

Add Visual IR C# types and validation.

No UI change. No renderer change. No database change.

### Phase 2: debug renderer

Create a simple renderer that can draw:

- groups;
- rects;
- text labels for unknown/unsupported nodes;
- optional bounds overlays.

Purpose: verify document shape and coordinate rules before visual fidelity work.

### Phase 3: design preview bridge

Convert one existing design preview kind to Visual IR:

```text
status bar or navigation bar first
```

Keep the existing web preview path available while comparing output.

### Phase 4: real primitive adapter

Implement Avalonia mapping for:

- paint;
- stroke;
- clipping;
- transforms;
- text;
- image;
- inline SVG.

Only after this phase should the UI offer a true Avalonia Visual IR preview.

### Phase 5: runtime/frame model

Introduce resolved model and frame model services:

```text
records -> resolved model -> frame model -> Visual IR
```

This is where chat/screen/module semantics belong, not in the renderer.

### Phase 6: mobile package compatibility

Keep the Visual IR document serializable and self-contained enough that a future
mobile rodaje app can render the same frame without loading editor services.

## Guardrails

- `MainWindow` wires preview hosts only.
- `EditorPreviewController` may select mode/device/theme, but should delegate
  frame/document creation.
- `DesignPreviewPayloadFactory` is transitional and should not grow into a new
  renderer.
- No renderer method may accept `SpikeDatabase`.
- No renderer method may accept `ProjectTreeNode`.
- No renderer method may read icon theme directories.
- No renderer method may resolve palette/theme/component inheritance.
- Any bridge class with legacy/domain knowledge must live under `Preview/Bridges`
  or equivalent and be clearly temporary.

## Validation checklist

A Visual IR renderer is clean when all answers are yes:

- Can it render a document produced from a test fixture?
- Can it render without opening SQLite?
- Can it render without `ProjectTreeNode`?
- Can it render without knowing status bar, navigation bar, chat or component
  class names?
- Can the same document be sent to a React/web adapter, an Avalonia adapter or a
  future mobile adapter?
- Can missing assets/fonts be reported as diagnostics instead of silently
  querying editor state?

## Immediate next safe task

The safest implementation step is documentation plus C# model scaffolding:

```text
VisualIr/*.cs
VisualIrValidation.cs
one JSON fixture
one unit-style smoke test that validates and serializes a document
```

Do not wire it into the visible preview until the contract compiles and we have
a tiny debug renderer.
