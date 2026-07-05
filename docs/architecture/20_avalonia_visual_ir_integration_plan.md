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

## Component, bridge and renderer contracts

Visual IR is a document contract, not a component API. Each layer has a narrow
responsibility.

Component resolvers own component decisions. They receive already resolved
editor data, inheritance, component overrides, device metrics, current frame
state and resource references, then emit standard design atoms:

- `group`
- `rect`
- `text`
- `svg`
- `image`
- `video`
- future primitive atoms such as `path`, `ellipse`, `clip`, `mask` or effects

Those atoms must already contain:

- bounds in design units;
- z/order through child ordering;
- text content;
- generated SVG markup when internal geometry matters;
- normalized icon SVG markup or explicit asset references;
- media references;
- opacity, clipping and fit;
- chosen semantic color references.

Component resolvers decide layout. That includes positions, sizes, gaps,
padding, text alignment, generated shape dimensions, icon boxes, media boxes and
component-specific fallback geometry. If a value changes where something is or
what shape it has, it belongs before the bridge.

Reusable resolver behavior belongs in shared/base routines, not inside the
first component resolver that needs it. SVG normalization, generated SVG
primitives, icon tint conventions, theme token catalogs, color variant math and
device metric conversions must be common routines. Component resolvers may call
those routines, but they must not fork the algorithms locally.

The Visual IR bridge owns representation conversion. It receives design atoms
and creates a valid `VisualIrDocument`. It may:

- map standard atoms to Visual IR node types;
- resolve semantic color references to concrete variant color values;
- package resources and asset identifiers;
- preserve debug metadata;
- validate the final document.

Color fallback values must be visibly diagnostic. Do not use plausible theme
colors as fallback values for unresolved references, because they can hide
broken token paths. The desktop seed provides the protected palette token
`debug_red` for this purpose; bridges should resolve that token to concrete hex
before emitting Visual IR.

The bridge must not compute component layout. It must not decide status-bar item
gaps, navigation-button dimensions, bubble geometry, avatar image placement,
text alignment or media crop rules. If a temporary legacy bridge needs those
decisions, they must live in an isolated resolver next to the bridge and remain
searchable as transitional code.

The renderer owns pixels. It receives `VisualIrDocument` plus render options and
draws only Visual IR primitives. It may choose selected color variant, zoom, fit,
device-frame overlay, debug bounds and target output scale. It must not call the
database, resolve themes, inspect component classes, or know names such as
`statusBar`, `navigationBar`, `chat`, `bubble`, `themeToken` or `paletteColor`.

Short rule:

```text
component resolver -> geometry and content in design units
bridge             -> Visual IR shape and concrete color variants
renderer/viewer    -> presentation, zoom and output pixels
```

## Resolved model and frame model

Avalonia should preserve the React architecture split between resolved model
and frame model.

The resolved model answers:

```text
What are the final values after ownership, inheritance and resource resolution?
```

Examples:

- semantic theme color references selected after inheritance and overrides;
- resolved font family, weight, style, size and line height;
- component class properties plus local overrides;
- resolved media/icon/font references;
- device metrics and orientation;
- design-space values used for component layout.

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

Visual IR uses design-space units.

Stored visual values, component layout, Visual IR bounds and Visual IR font
metrics stay in the logical design coordinate space of the document. The Visual
IR document declares that space through its viewport/root dimensions. The
renderer or viewer maps the document to a concrete panel, device frame, pixel
ratio, export size or render preset.

Target flow:

```text
stored design value
  -> resolver/component layout in design units
  -> Visual IR bounds/style values in design units
  -> renderer/viewer output scale
```

This lets the debug/design viewer change zoom, available panel size, selected
display frame or final output scale without requesting a new Visual IR payload.
A new payload is required only when scene data changes: content, component
layout, selected logical device, frame state, assets, theme, color variants,
text, language or animation state.

`scaleToPixels`, device pixel ratio and render-preset scale are output mapping
inputs. They must not be baked into Visual IR geometry. If a legacy binding
mixes scaled and unscaled values, the transitional resolver must normalize them
before the bridge and mark the exception explicitly.

The current Avalonia design-preview bridge starts from legacy device preview
metrics whose canvas/screen values are render pixels. Its transitional resolver
divides those values by `scaleToPixels` before creating design atoms. Future
device metric services should expose design-space metrics directly to avoid
this adapter step.

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

## Variant color contract

Visual IR may carry color variants so a playback client can switch display mode
without receiving duplicate frame payloads.

The motivating case is on-set playback: the same shot can be displayed in a
variant suitable for the current lighting conditions without asking the editor
or resolver to resend every plane.

This is a color-only feature. A variant switch may affect:

- solid fill colors;
- gradient stop colors;
- stroke paint colors;
- text fill colors;
- SVG tint colors;
- shadow or glow colors.

A variant switch must not affect:

- geometry;
- bounds;
- transforms;
- font selection or metrics;
- icon token selection;
- media asset selection;
- visibility;
- text/content;
- layout, padding or spacing.

If one of those non-color values changes between modes, the resolver must emit a
new Visual IR document or frame sequence.

The contract should not hardcode `light` and `dark`. The document declares the
available variant names:

```json
{
  "resources": {
    "colorVariants": ["set_day", "set_night", "high_contrast"],
    "defaultColorVariant": "set_night"
  }
}
```

Any color slot may then be either a concrete color string or a variant color:

```json
{
  "kind": "solid",
  "color": {
    "kind": "variant",
    "values": {
      "set_day": "#111111",
      "set_night": "#f7f7f7",
      "high_contrast": "#ffffff"
    },
    "fallback": "#f7f7f7"
  }
}
```

This applies everywhere a color appears in `VisualIrPaint`, including gradient
stops and SVG tint.

Theme and palette tokens do not cross this boundary. The resolver/interpreter
may start from theme tokens, palette colors and mode-specific app rules, but it
must resolve them to concrete hex values for every advertised variant before
creating the IR document.

Renderer behavior is deliberately small:

1. Receive the selected color variant as render option.
2. If a variant color has that key, use that hex value.
3. Otherwise use `fallback`.
4. If no fallback exists, emit a diagnostic and use a safe visible color.

Debug metadata may optionally keep the original token name for inspection, but
rendering must not depend on it.

## Resolver boundaries

Resolvers should be small and composable:

- resource resolvers: palette, theme colors, fonts, icon themes, media paths;
- field resolvers: inherited/concrete/default values;
- component resolvers: component classes plus overrides;
- module resolvers: module-specific runtime props;
- frame evaluators: animation, write-on, video/audio progress;
- screen resolvers: device metrics, orientation, ordering and transitions.

Resolvers may validate stored JSON, resolve references, compute timing and
produce component layout in design units. They should not draw, call preview
APIs, contain editor UI layout, output-scale Visual IR geometry, or carry
long-term legacy fallbacks.

## Transitional bridge

The current `DesignPreviewPayloadFactory` can become the first bridge source:

```text
DesignPreviewPayload
  -> DesignPreviewFrameResolver
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
