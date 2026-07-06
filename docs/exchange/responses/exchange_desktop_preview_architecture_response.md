# Proposed response: Desktop Preview Architecture Boundaries and Enforcement

## Main position

I think the priority of this document should be to make the architecture boundaries explicit and enforceable before we keep adding more components.

The recurring issue is not only implementation detail drift. The problem is that different layers can easily start sharing vocabulary and responsibilities. Once that happens, each new component increases coupling, and the preview path slowly turns back into a central component-aware bridge.

The goal should be to force the architecture, not merely describe it.

Core invariant:

```text
Component-specific knowledge stays in the component module.
Generic rendering stays generic.
The registry only routes.
```

The document should primarily confirm that everyone understands:

```text
1. What each layer is allowed to know.
2. What each layer is not allowed to know.
3. What data is passed between layers.
4. Which scripts prevent future drift.
```

---

## Shared mental model

The pipeline should be understood as:

```text
Editor / catalog schema
  -> component resolver
  -> component renderable
  -> generic paint tree / renderable tree
  -> generic HTML/CSS/SVG adapter
  -> WebView
```

I would avoid using `bridge` as the conceptual name for the middle layer.

The old bridge was removed intentionally. The remaining intermediate object should be treated as a **paint tree** or **renderable tree**, not as a semantic bridge.

The simple rule is:

```text
The component understands semantics.
The component converts semantics into geometry.
The generic renderer only paints geometry.
```

For example:

```text
Audio waveform
```

is component semantics.

It should cross the component/render boundary as something like:

```text
group
  surface
  text
  image
  rect
  rect
  rect
  path
```

The generic web renderer should not know that those rects or paths represent a waveform. It should only know how to paint shapes, text, images, paths, shadows, opacity, clipping, and layout primitives.

---

## Layer responsibilities

### 1. Editor / catalog layer

This layer owns the editable component schema.

It may know:

```text
componentClass
component category
field names
field types
field dictionary
token references
inherit / override / restore state
```

Its purpose is to allow the editor to render a generic component editor.

It should not know how a component is visually drawn.

Allowed vocabulary:

```text
field
fieldType
dictionary
token reference
override
inherit
restore
System / Atom / Component category
```

Forbidden vocabulary:

```text
HTML
CSS
SVG
React
waveform layout
avatar layout
status bar layout
audio layout
bubble layout
```

---

### 2. Component layer

This layer owns semantic interpretation.

A component resolver/renderable may know things like:

```text
audio
waveform
playbackProgress
avatar slot
badge slot
label slot
status bar icons
navigation bar home indicator
keyboard rows
```

This is the only layer that should understand:

```text
component fields
embedded slots
inheritance chains
overrides
token resolution
frame-specific component state
component layout semantics
```

The component output should be a generic paint tree.

Once data leaves the component renderable, it should no longer contain component field names or unresolved semantic concepts.

Correct conceptual model:

```text
Audio enters as audio.
Audio exits as geometry.
```

---

### 3. Paint tree / renderable payload

This is the main boundary.

The payload should contain only visual instructions:

```text
x
y
width
height
children
text
font
imageUri
path
radius
opacity
shadow
clip
fill
stroke
resolved light/dark color values
```

It should not contain:

```text
waveform
badge
labelSlot
audio
avatar
statusBar
navigationBar
keyboard
component field names
unresolved token names
inheritance state
override state
database records
```

Colors can still be represented as light/dark pairs, but they should already be resolved values:

```ts
fill: {
  light: "#FFFFFF",
  dark: "#111111"
}
```

Not:

```ts
fillToken: "theme.surface.primary"
```

The generic renderer may switch between dark and light using already-resolved values. It should not resolve tokens.

---

### 4. Generic web adapter

The web adapter converts paint primitives to HTML/CSS/SVG.

It may know:

```text
group
surface
rect
circle
path
text
image
icon/image asset
shadow
clip
opacity
scaleToPixels
```

It must not know:

```text
label
avatar
buttonIcon
audio
waveform
statusBar
navigationBar
keyboard
bubble
component field names
slot names
token names
inheritance
overrides
```

The generic web adapter should not resolve component configuration, theme token names, palette names, database records, inheritance, overrides, or component-specific layout rules.

It should only convert generic paint nodes into DOM/CSS/SVG output.

---

### 5. WebView

The WebView should only display the generated HTML and allow mode switching if the HTML already contains resolved light/dark values.

It should not resolve:

```text
layout
tokens
inheritance
animation state
component semantics
```

It displays the result. It does not interpret the design system.

---

## System / Atom / Component categories

I would divide component classes into three semantic categories:

```text
System
  statusBar
  navigationBar
  keyboard

Atoms
  label
  icon
  image
  text

Components
  avatar
  audio
  bubble
  video
  media
```

But this should be **metadata in the component manifest**, not an architectural branching mechanism.

Correct:

```ts
statusBar: {
  category: "system",
  resolver: statusBarComponentResolver,
  renderable: statusBarComponentRenderable
}
```

Not desirable:

```text
system renderer
atom renderer
component renderer
```

All categories should use the same pipeline:

```text
componentClass
  -> resolver
  -> contract
  -> renderable
  -> paint tree
  -> generic web adapter
```

The category is useful for:

```text
editor grouping
documentation
validation
UX
filtering
future tooling
```

It should not create separate render paths.

---

## Status Bar / Navigation Bar / Keyboard

Status Bar and Navigation Bar should move into the normal component class model.

They may represent system chrome, but architecturally they behave like components:

```text
they have fields
they have defaults
they need resolver logic
they need layout logic
they produce renderable output
they should emit generic paint primitives
```

Target model:

```ts
{
  kind: "componentClass",
  componentClass: "statusBar"
}
```

```ts
{
  kind: "componentClass",
  componentClass: "navigationBar"
}
```

Later:

```ts
{
  kind: "componentClass",
  componentClass: "keyboard"
}
```

Temporary compatibility shims for:

```ts
kind: "statusBar"
kind: "navigationBar"
```

are fine during migration, but they should not be the final architecture.

These should become:

```text
statusBarComponentContract.ts
statusBarComponentResolver.ts
statusBarComponentRenderable.ts

navigationBarComponentContract.ts
navigationBarComponentResolver.ts
navigationBarComponentRenderable.ts

keyboardComponentContract.ts
keyboardComponentResolver.ts
keyboardComponentRenderable.ts
```

System means category. It should not mean a different rendering architecture.

---

## Registry vs manifest

`componentClassRenderableRegistry.ts` is acceptable as a routing file, but I would introduce a component manifest as the single source of truth.

Example:

```ts
export const desktopPreviewComponents = {
  label: {
    category: "atom",
    contract: "./labelComponentContract.js",
    resolver: "./labelComponentResolver.js",
    renderable: "./labelComponentRenderable.js",
    embeds: []
  },

  avatar: {
    category: "component",
    contract: "./avatarComponentContract.js",
    resolver: "./avatarComponentResolver.js",
    renderable: "./avatarComponentRenderable.js",
    embeds: ["label"]
  },

  audio: {
    category: "component",
    contract: "./audioComponentContract.js",
    resolver: "./audioComponentResolver.js",
    renderable: "./audioComponentRenderable.js",
    embeds: ["avatar", "buttonIcon"]
  },

  statusBar: {
    category: "system",
    contract: "./statusBarComponentContract.js",
    resolver: "./statusBarComponentResolver.js",
    renderable: "./statusBarComponentRenderable.js",
    embeds: []
  },

  navigationBar: {
    category: "system",
    contract: "./navigationBarComponentContract.js",
    resolver: "./navigationBarComponentResolver.js",
    renderable: "./navigationBarComponentRenderable.js",
    embeds: []
  }
} as const;
```

Then:

```text
componentClassRenderableRegistry.ts
```

can either be generated from the manifest or validated against it.

The enforcement script should also read the manifest instead of keeping a separate hardcoded embedded dependency allowlist. That prevents drift between documentation, routing, and guardrails.

---

## Generic node types

Removing `component_*` renderable node types is the correct direction.

Allowed paint node types should be generic primitives only, for example:

```text
group
surface
rect
circle
path
text
image
icon
line
```

I would avoid node type names such as:

```text
waveform_bar
avatar
icon_token
```

unless they are carefully defined as generic primitives. They still carry semantic leakage.

Prefer:

```text
waveform_bar -> rect / bar / path / barSeries
avatar       -> image with mask/fallback, or group of image/text/surface
icon_token   -> icon/image with already resolved assetUri/dataUri
```

Debug metadata is acceptable:

```ts
metadata: {
  sourceComponent: "audio",
  sourcePart: "waveform"
}
```

but metadata must not affect rendering.

---

## Asset and icon resolution

`componentRenderableCommon.ts` is currently too broad.

It can remain temporarily, but I would split it into clearer modules:

```text
previewColorHelpers.ts
previewGeometryHelpers.ts
previewSurfaceHelpers.ts
previewShadowHelpers.ts
previewAssetResolver.ts
previewIconResolver.ts
```

The most important extraction is asset/icon resolution.

A helper named `iconUriForToken` should not live indefinitely inside `componentRenderableCommon.ts`, especially if it performs filesystem reads.

It is generic, but it is asset resolution, not renderable composition.

Target direction:

```text
component resolver/renderable
  -> asks generic asset resolver
  -> paint tree receives resolved assetUri/dataUri
  -> web adapter paints image/path/icon
```

The web adapter should not resolve icon tokens.

---

## Contracts

Contracts can stay colocated for now:

```text
labelComponentContract.ts
labelComponentResolver.ts
labelComponentRenderable.ts
```

This is clear during migration.

A `contracts/` subfolder is only worth it if the directory becomes too large.

The important rule is not the folder. The important rule is:

```text
resolver and renderable share a neutral contract file
resolver must not import renderable
renderable must not import resolver
```

---

## Answers to Codex review questions

### 1. Does the layer split avoid circular or hidden dependencies?

Mostly yes, but only if we make the boundary vocabulary explicit and enforce it.

The main hidden-coupling risks are:

```text
unresolved tokens leaking downward
component field names leaking into paint nodes
generic helpers becoming component-specific in behavior
system bars remaining as special top-level preview kinds
```

---

### 2. Is `componentClassRenderableRegistry.ts` acceptable?

Yes, as a routing file.

Longer term, use a manifest as the source of truth and either generate or validate the registry from it.

---

### 3. Should Status Bar and Navigation Bar become normal component classes?

Yes.

They should be component classes with category `system`.

Top-level `kind: "statusBar"` and `kind: "navigationBar"` should be migration shims only.

---

### 4. Is `componentRenderableCommon.ts` too broad?

Yes.

It should be split gradually.

Asset/icon resolution is the first thing I would extract.

---

### 5. Should icon file resolution happen inside renderable common?

Not long term.

It should move to a generic asset resolver/helper.

The paint tree should receive resolved asset references or data URIs.

---

### 6. Contracts colocated or subfolder?

Colocated is fine for now.

Enforce the contract/resolver/renderable naming and dependency pattern instead.

---

### 7. Is the current script too lexical?

Yes.

Keep lexical checks as smoke tests, but add TypeScript AST/module graph checks.

---

### 8. Should embedded imports be modeled as a manifest?

Yes.

The manifest should declare `embeds`, and the script should validate imports against it.

---

### 9. Should naming conventions be enforced?

Yes.

Required triplet:

```text
*ComponentContract.ts
*ComponentResolver.ts
*ComponentRenderable.ts
```

Every manifest entry should have all three.

---

### 10. Should sibling component contract imports fail?

Yes by default.

A component should not import another component's contract unless the dependency is explicitly declared in the manifest.

Even then, the preferred model is that parent components call the child resolver/renderable rather than inspect child internals.

---

### 11. Should `DesignPreviewPayload.device` be renamed?

Yes.

It should become something like:

```text
previewFrame
renderFrame
frameGeometry
```

I prefer:

```text
previewFrame
```

because it describes what it is without implying actual device semantics.

---

### 12. Should `component_label` etc. be renamed?

Yes, and this is already the correct direction.

The remaining thing is to make the primitive allowlist strict and avoid new semantic primitive names that reintroduce the same issue under softer names.

---

## Safeguard script plan

I would split enforcement into several focused checks instead of growing one large lexical script forever.

---

### 1. `check:preview-boundaries`

Purpose: prevent central files from becoming component-aware.

Checks:

```text
webPreviewBridge.ts must not exist

renderDesignPreviewHtml.tsx must not contain component class names

RenderableReactAdapter.tsx must not contain component class names

common helper files must not contain component class names or field/slot names

central files must not import concrete component resolver/renderable files
```

This is the current guardrail, expanded to include the generic renderer and all central/common files.

---

### 2. `check:preview-manifest`

Purpose: make the component catalog explicit.

Checks:

```text
every componentClass is listed in desktopPreviewComponents manifest

every entry has:
  category: system | atom | component
  contract
  resolver
  renderable
  embeds

every listed file exists

every component has the required naming triplet:
  nameComponentContract.ts
  nameComponentResolver.ts
  nameComponentRenderable.ts

componentClassRenderableRegistry.ts matches the manifest
```

This makes the manifest the source of truth.

---

### 3. `check:preview-import-graph`

Purpose: replace hardcoded import allowlists with AST/module graph rules.

Checks using TypeScript AST:

```text
resolver may import:
  own contract
  DesignPreviewPayload / narrowed resolver context
  resolver common helpers
  declared child resolvers

renderable may import:
  own contract
  RenderableNode types
  renderable common helpers
  declared child renderables

renderable must not import sibling contracts unless manifest explicitly allows it

resolver must not import renderable

renderable must not import resolver

common helpers must not import concrete components

generic renderer must not import desktop-preview component modules

registry may import component entrypoints only for routing
```

This is the most important long-term check.

---

### 4. `check:paint-tree-schema`

Purpose: ensure the component/renderable boundary stays clean.

Checks:

```text
RenderableNode type values must be in a generic primitive allowlist

forbidden node types:
  component_*
  *_waveform*
  *_avatar*
  *_audio*
  *_statusBar*
  *_navigationBar*

paint nodes must not contain:
  component field names
  slot names
  unresolved token names
  inheritance state
  override state
  database identifiers

colors must be:
  concrete values
  or resolved light/dark pairs
```

Allowed example:

```ts
{
  type: "rect",
  fill: { light: "#FFFFFF", dark: "#111111" }
}
```

Forbidden example:

```ts
{
  type: "waveform_bar",
  colorToken: "theme.audio.waveform.active"
}
```

---

### 5. `check:renderer-purity`

Purpose: ensure the generic web renderer remains generic.

Checks:

```text
RenderableReactAdapter.tsx only switches on generic primitive node types

it must not switch on metadata.sourceComponent

it must not import component modules

it must not contain component class names

it must not resolve theme token names

it must not resolve palette token names

it must not run component behavior timers:
  setTimeout
  setInterval
  requestAnimationFrame
  CSS animation for component behavior
```

Marks/bounds rendering is okay if generic.

---

### 6. `check:payload-shape`

Purpose: keep the Avalonia-to-preview request free of component/device semantic leakage.

Checks:

```text
DesignPreviewPayload should use previewFrame/renderFrame instead of device

preview frame may contain:
  canvasWidth
  canvasHeight
  screenX
  screenY
  screenWidth
  screenHeight
  scaleToPixels

preview frame must not contain:
  statusBarHeight
  safeAreaBottom
  keyboardHeight
  navigationBarHeight
  component layout metrics
```

If Status Bar, Navigation Bar, or Keyboard need those values, they should come from their own component config/resolver/contract.

---

### 7. `check:asset-boundaries`

Purpose: prevent asset resolution from drifting into the renderer.

Checks:

```text
RenderableReactAdapter must not read filesystem

RenderableReactAdapter must not resolve icon tokens

componentRenderableCommon should not perform filesystem reads once asset resolver is extracted

only previewAssetResolver / previewIconResolver may read iconMappingJson or asset roots
```

---

### 8. `check:override-semantics`

Purpose: protect the embedded override model.

Tests:

```text
an override remains an override even if the base value later becomes equal

restore/inherit explicitly removes the override entry

embedded component overrides are merged only by the parent that owns the slot

copied field groups are not treated as embedded components
```

This protects one of the most important editor semantics.

---

### 9. `check:component-migration-completeness`

Purpose: prevent partial migrations.

Checks:

```text
if bubble is migrated, all bubble-owned subcomponents must also be migrated

no component may mix legacy rendering with the new paint-tree path

no new component can be added without:
  manifest entry
  contract
  resolver
  renderable
  visual validation fixture
```

This is especially important for Bubble, because partial migration would recreate the same cross-layer coupling problem.

---

## Proposed final invariants for the document

Add this near the top:

```text
After the component renderable boundary, preview data must not contain component field names, component slot names, unresolved token names, inheritance state, override state, database/editor state, or component-specific node types.

The generic renderer consumes only paint primitives with resolved values.
```

And this:

```text
System, Atom, and Component are semantic categories in the component manifest.
They must not create separate render paths.
All component classes resolve through the same contract/resolver/renderable pipeline and emit the same generic paint tree.
```

And this:

```text
Status Bar, Navigation Bar, and Keyboard are system component classes.
They are system in category, not separate in architecture.
```

---

## Recommended implementation order

I would prioritize the work like this:

```text
1. Add the explicit layer vocabulary and boundary rules to the document.

2. Introduce desktopPreviewComponents manifest with:
   category
   contract
   resolver
   renderable
   embeds

3. Move Status Bar and Navigation Bar to componentClass entries with category "system".
   Keep old top-level kinds only as temporary shims.

4. Rename DesignPreviewPayload.device to previewFrame.

5. Define a strict generic paint primitive allowlist.

6. Add check:preview-manifest and check:preview-import-graph.

7. Add check:paint-tree-schema and check:renderer-purity.

8. Extract icon/asset resolution out of componentRenderableCommon.

9. Add override semantics tests.

10. Before migrating Bubble, enforce migration completeness for Bubble and all owned subcomponents.
```

The goal is not to slow implementation.

The goal is to stop architecture drift from becoming the default implementation pattern.

Once these guardrails exist, adding new components should become faster because every component has one obvious path:

```text
manifest entry
contract
resolver
renderable
paint tree
generic renderer
validation
```
