# Desktop Preview Architecture: Updated Alignment Response

This response incorporates the v4 Codex feedback into the previous proposal.

The goal is not to implement the next component yet. The goal is to make the architecture boundaries explicit enough that the implementation is forced to stay on the intended path.

The priority of the architecture document should be:

```text
1. Confirm that everyone shares the same definitions.
2. Define what each layer is allowed to know.
3. Define exactly what crosses each boundary.
4. Add scripts/tests that fail when responsibilities leak across layers.
```

The architecture should prevent accidental drift before the component set grows.

---

## 1. Summary position

I agree with the Codex v4 corrections.

The v3 proposal was directionally aligned, but four clarifications should be folded into the final architecture document:

```text
1. Components own token references and semantic token usage.
   They must not own final theme/palette token lookup.

2. Removing the old central bridge must not mean duplicating generic translation
   logic inside every component.

3. Colors should not be modeled as fixed light/dark pairs.
   They should be resolved named mode variants.

4. Components are recursive.
   A component may embed child components, but only through declared ownership.
```

The fourth point is especially important. The correct rule is not:

```text
No component may import another component.
```

The correct rule is:

```text
A component may import and compose another component only when that child is
explicitly declared as an owned embedded slot in the manifest.
```

---

## 2. Corrected mental model

The clean model is:

```text
Editor / catalog schema
  -> component resolver
  -> component contract
  -> component renderable
  -> generic preview helpers
  -> generic paint tree / renderable tree
  -> generic HTML/CSS/SVG adapter
  -> WebView
```

The short version is:

```text
The editor edits fields.
The component understands semantics.
Generic helpers resolve shared values.
The component renderable emits paint primitives.
The generic renderer paints primitives.
The WebView displays HTML.
```

The old central `webPreviewBridge` failure mode must not return. But avoiding a component-aware bridge does not mean removing all generic translation logic.

The architecture needs a generic helper layer for shared operations:

```text
token and palette resolution
mode variant resolution
alpha and neutral tint handling
asset/icon resolution
design-unit to pixel scaling
surface / shadow / text / image helpers
generic placement math
generic diagnostics
```

Those helpers must remain generic. They must not import concrete components or contain component-specific rules.

---

## 3. Layer responsibilities

### 3.1 Catalog / editor schema layer

This layer owns editable component definitions.

Allowed vocabulary:

```text
componentClass
component category
field
field type
dictionary type
dictionary metadata
inherit
restore
override
token reference
```

Purpose:

```text
Render generic editors from field definitions.
Group fields into panels or sections.
Commit field edits through the standard edit path.
```

Forbidden responsibilities:

```text
No component layout.
No HTML/CSS/SVG knowledge.
No custom rendering logic.
No duplicated controls for typed fields.
No token lookup to final paint values.
```

Panels may decide which fields to show and how to group them. They must not decide how a typed field is edited.

---

### 3.2 Dictionary field control layer

This boundary should be explicit.

Rule:

```text
No dictionary field type may exist without a public registered field control.
```

A UI panel must not invent controls for typed values.

Correct:

```text
field definition
  -> dictionary field type
  -> registered field control
  -> generic commit path
```

Incorrect:

```text
panel sees field.type === "colorToken"
  -> creates a local token picker
```

This applies to every UI surface:

```text
field editor
component panels
embedded component editors
inspectors
property sheets
preview input panels
dialogs
bulk editors
future editor surfaces
```

Generic UI controls are allowed, but only below registered dictionary field controls.

For example, this is acceptable:

```text
ColorTokenFieldControl      -> GenericTokenPicker
IconTokenFieldControl       -> GenericTokenPicker
TypographyTokenFieldControl -> GenericTokenPicker
EnumFieldControl            -> GenericSelect
MediaFieldControl           -> GenericAssetPicker
```

This is not acceptable:

```text
AudioPanel -> GenericTokenPicker
AvatarPanel -> GenericSelect
StatusBarPanel -> locally built color picker
```

Even if a field type internally delegates to a generic picker, the public control must be field-type-specific. That lets us change the editing behavior for a dictionary type later without touching every panel that uses it.

---

### 3.3 Component resolver layer

The resolver owns component semantics.

Allowed vocabulary:

```text
component fields
embedded slots
component defaults
inheritance chains
overrides
visibility
layout intent
animation frame state
semantic token references
```

Examples:

```text
Audio resolver may know waveform, progress, avatar slot and button icon slot.
Avatar resolver may know label slot.
Status Bar resolver may know status icons and clock semantics.
Keyboard resolver may know key rows and key labels.
```

The resolver may decide which token reference a semantic field uses.

It must not perform final token lookup to concrete values.

Correct:

```ts
backgroundColor: { token: "theme.colors.surface" }
```

Incorrect inside component-specific resolver logic:

```ts
backgroundColor: "#181818"
```

unless that literal is itself a component-defined fixed value and not a resolved token/palette lookup.

The resolver should output a component contract.

---

### 3.4 Component contract layer

The contract is the neutral boundary between resolver and renderable.

It may contain:

```text
resolved component structure
embedded child contracts
semantic layout decisions
token references
asset references
numbers and booleans already validated by the resolver
```

It should not contain:

```text
HTML
CSS
React
DOM assumptions
editor control state
DB records
ad hoc renderer-specific branches
```

Important correction:

```text
Component contracts may still contain token references.
The final paint tree may not.
```

So the contract is not necessarily the final resolved visual payload. It is the component-specific resolved contract.

---

### 3.5 Component renderable layer

The renderable converts the component contract into generic paint primitives.

It may know:

```text
the component contract
owned child component renderables
generic paint helpers
generic asset/color/geometry helpers
```

It may not know:

```text
editor controls
DB/editor storage
HTML implementation details
React internals
undeclared sibling components
unowned grandchildren
```

The renderable can call generic helpers to resolve shared values. The important point is that the resolution logic lives in generic helpers, not as duplicated component-specific lookup logic.

Correct:

```text
Audio renderable
  -> knows how audio becomes groups, text, images and bars
  -> calls generic color/asset/geometry helpers
  -> calls Avatar renderable only because Audio owns Avatar slot
```

Incorrect:

```text
Audio renderable
  -> imports Label renderable directly by skipping Avatar
  -> implements its own palette lookup
  -> emits node type "waveform_bar"
```

---

### 3.6 Generic preview helper layer

This layer is necessary.

Allowed responsibilities:

```text
theme token lookup
palette color lookup
neutral tint
alpha
mode variant resolution
asset/icon reference resolution
design unit to pixel scaling
generic box/placement math
generic surface/shadow helpers
generic text/image/icon helpers
generic diagnostics
```

Forbidden responsibilities:

```text
No component class names.
No component field names.
No concrete component imports.
No component-specific layout rules.
No embedded override merging.
No registry routing.
```

This is the replacement for the useful generic parts of the old bridge, without the component-aware centralization problem.

---

### 3.7 Final paint tree / renderable tree

This is the critical boundary.

After the component renderable/helper boundary, the preview data should contain only paint primitives and resolved values.

Allowed:

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
resolved mode-variant values
```

Forbidden:

```text
component field names
component slot names
unresolved token names
inheritance state
override state
database/editor state
component-specific node types
```

Correct:

```ts
{
  type: "surface",
  fill: {
    variants: {
      light: "#FFFFFF",
      dark: "#111111",
      alternate: "#F8D36A"
    }
  }
}
```

Incorrect:

```ts
{
  type: "audio_waveform",
  fillToken: "theme.audio.waveform.active"
}
```

---

### 3.8 Generic renderer / React adapter

The generic renderer paints paint primitives.

Allowed vocabulary:

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
barSeries, only if genuinely generic
clip
shadow
opacity
mode variant selection
scaleToPixels
```

Forbidden vocabulary:

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

Renderer responsibility:

```text
Select an already-resolved mode variant.
Paint the primitive.
```

Renderer non-responsibility:

```text
Resolve token names.
Resolve palette names.
Interpret component metadata.
Advance animations.
Run component timers.
Branch on sourceComponent/sourcePart metadata.
```

Debug metadata is allowed only if it cannot affect rendering:

```ts
metadata: {
  sourceComponent: "audio",
  sourcePart: "waveform"
}
```

The renderer must not switch on that metadata.

---

### 3.9 WebView

The WebView displays the generated HTML.

It may know:

```text
HTML string
current mode name
viewport/window size
```

It must not know:

```text
component semantics
token lookup
layout rules
inheritance rules
override rules
field controls
```

---

## 4. Token and color responsibility

Codex is right that the previous wording was too broad when saying the component owns token resolution.

Corrected wording:

```text
The component owns token references and semantic use of tokens.
Generic preview helpers own token, palette, tint, alpha and mode resolution.
The final paint tree contains only resolved values.
```

The resolver can say:

```text
this audio background uses the component's background token reference
```

But it should not implement:

```text
look up this token in the theme and produce final color variants
```

That lookup belongs to the generic helper layer.

---

## 5. Mode variants are not fixed light/dark pairs

Examples may use `light` and `dark`, but the contract should be open-ended.

Preferred:

```ts
color: {
  variants: {
    light: "#FFFFFF",
    dark: "#111111",
    alternate: "#F8D36A",
    outdoor: "#FAFAFA"
  }
}
```

Not preferred as the formal contract:

```ts
color: {
  light: "#FFFFFF",
  dark: "#111111"
}
```

The renderer may select:

```text
variants[currentMode]
```

It must not resolve:

```text
theme.colors.surface
palette.neutral.700
```

---

## 6. Recursive embedded components

Components are not isolated leaf nodes.

The system intentionally supports recursive component composition.

Example:

```text
audio
  embeds avatar
    embeds label

audio
  embeds buttonIcon
```

That means:

```text
Audio owns Avatar slot.
Audio owns Button Icon slot.
Avatar owns Label slot.
Label keeps its own component identity.
```

The manifest must declare these relationships.

Correct rule:

```text
Parent component may import declared child component resolver/renderable.
```

Forbidden:

```text
component -> undeclared sibling component
component -> grandchild component by skipping the direct child owner
common helper -> concrete component
generic renderer -> concrete component
registry -> component internals beyond routing entrypoints
```

So this is allowed:

```text
Audio -> Avatar
Avatar -> Label
```

This is not allowed:

```text
Audio -> Label
```

unless Audio directly declares a Label slot in the manifest.

---

## 7. Embedded override semantics

Embedded components must keep their identity.

An embedded Label inside Avatar is not copied label fields. It is a Label component instance/slot whose base class values can be locally overridden by the owning parent slot.

Invariant:

```text
An override remains an override even if the parent/base value later changes to
that same value by coincidence.
```

The override only returns to inherited state through an explicit restore/inherit action.

This should be covered by tests.

---

## 8. System / Atom / Component categories

The category split is useful and should be kept.

Suggested categories:

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
  buttonIcon

Components
  avatar
  audio
  bubble
  video
  media
```

But these are manifest categories only.

Correct:

```ts
statusBar: {
  category: "system",
  resolver: statusBarComponentResolver,
  renderable: statusBarComponentRenderable
}
```

Incorrect:

```text
system renderer
atom renderer
component renderer
```

All categories use the same architecture:

```text
componentClass
  -> resolver
  -> contract
  -> renderable
  -> generic helpers
  -> generic paint tree
  -> generic renderer
```

System means category, not a separate render path.

---

## 9. Status Bar, Navigation Bar and Keyboard

Status Bar, Navigation Bar and Keyboard should become normal component classes with category `system`.

They may represent system chrome, but architecturally they are components:

```text
they have fields
they have defaults
they need resolver logic
they need layout logic
they emit paint primitives
```

Target:

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

```ts
{
  kind: "componentClass",
  componentClass: "keyboard"
}
```

Temporary shims for top-level `kind: "statusBar"` and `kind: "navigationBar"` are acceptable during migration, but they should not remain as the final architecture.

---

## 10. Component manifest as source of truth

The manifest should be authoritative for:

```text
component class name
category: system | atom | component
contract entrypoint
resolver entrypoint
renderable entrypoint
declared embedded children
allowed component-to-component imports
migration completeness
registry validation
```

Example:

```ts
export const desktopPreviewComponents = {
  label: {
    category: "atom",
    contract: "./labelComponentContract",
    resolver: "./labelComponentResolver",
    renderable: "./labelComponentRenderable",
    embeds: []
  },

  avatar: {
    category: "component",
    contract: "./avatarComponentContract",
    resolver: "./avatarComponentResolver",
    renderable: "./avatarComponentRenderable",
    embeds: ["label"]
  },

  buttonIcon: {
    category: "atom",
    contract: "./buttonIconComponentContract",
    resolver: "./buttonIconComponentResolver",
    renderable: "./buttonIconComponentRenderable",
    embeds: []
  },

  audio: {
    category: "component",
    contract: "./audioComponentContract",
    resolver: "./audioComponentResolver",
    renderable: "./audioComponentRenderable",
    embeds: ["avatar", "buttonIcon"]
  },

  statusBar: {
    category: "system",
    contract: "./statusBarComponentContract",
    resolver: "./statusBarComponentResolver",
    renderable: "./statusBarComponentRenderable",
    embeds: []
  },

  navigationBar: {
    category: "system",
    contract: "./navigationBarComponentContract",
    resolver: "./navigationBarComponentResolver",
    renderable: "./navigationBarComponentRenderable",
    embeds: []
  },

  keyboard: {
    category: "system",
    contract: "./keyboardComponentContract",
    resolver: "./keyboardComponentResolver",
    renderable: "./keyboardComponentRenderable",
    embeds: []
  }
} as const;
```

The registry may continue to exist, but it should be generated from or validated against the manifest.

Allowed registry responsibility:

```text
componentClass -> owning component entrypoint
```

Forbidden registry responsibilities:

```text
layout decisions
style decisions
token resolution
default values
embedded override merging
renderable construction
component business rules
```

---

## 11. Generic paint primitive allowlist

The final paint tree should use generic primitives.

Preferred primitive names:

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
barSeries, only if genuinely generic
```

Avoid component-specific node types:

```text
component_label
component_avatar
component_button_icon
component_audio
waveform_bar
statusBar
navigationBar
keyboard
bubble
```

A waveform is not a render primitive. It is audio semantics converted into paint primitives.

Correct:

```text
Audio waveform -> group of rect/path primitives
```

Incorrect:

```text
Audio waveform -> waveform_bar node type consumed by renderer
```

---

## 12. Enforcement script plan

The enforcement should be split into focused checks. Some can start lexical, but the important ones should become AST/module-graph checks.

### 12.1 `check:preview-boundaries`

Purpose: prevent central files from becoming component-aware.

Checks:

```text
webPreviewBridge.ts must not exist
renderDesignPreviewHtml.tsx must not contain component class names
RenderableReactAdapter.tsx must not contain component class names
common helpers must not contain component class names or field/slot names
central/common files must not import concrete component modules
```

---

### 12.2 `check:preview-manifest`

Purpose: make the component catalog explicit.

Checks:

```text
every component class is listed in the manifest
every entry has category, contract, resolver, renderable, embeds
every listed file exists
every entry follows naming convention
componentClassRenderableRegistry matches the manifest
system/atom/component are categories only
```

Required naming:

```text
*ComponentContract.ts
*ComponentResolver.ts
*ComponentRenderable.ts
```

---

### 12.3 `check:preview-import-graph`

Purpose: enforce component dependency boundaries using TypeScript AST.

Allowed:

```text
resolver -> own contract
resolver -> generic resolver helpers
resolver -> declared child resolvers

renderable -> own contract
renderable -> generic renderable helpers
renderable -> declared child renderables

registry -> component entrypoints for routing
```

Forbidden:

```text
resolver -> renderable
renderable -> resolver
component -> undeclared sibling component
component -> unowned grandchild component
common helper -> concrete component
generic renderer -> concrete component
registry -> component internals beyond routing
```

The allowed parent-to-child dependencies must come from the manifest `embeds` field.

---

### 12.4 `check:paint-tree-schema`

Purpose: ensure the final paint tree stays generic.

Checks:

```text
node types are in the generic primitive allowlist
no component_* node types
no waveform/status/navigation/bubble semantic node types
no component field names
no slot names
no unresolved token names
no inheritance state
no override state
no DB/editor state
```

Color checks:

```text
final paint tree colors must be resolved values
resolved colors should use named mode variants
fixed light/dark pair should not be the formal-only contract
```

---

### 12.5 `check:renderer-purity`

Purpose: keep the web renderer generic.

Checks:

```text
RenderableReactAdapter switches only on generic primitive node types
RenderableReactAdapter does not import desktop-preview component modules
RenderableReactAdapter does not contain component class names
RenderableReactAdapter does not resolve token or palette names
RenderableReactAdapter does not branch on debug metadata
RenderableReactAdapter does not run component behavior timers
```

Forbidden runtime behavior:

```text
setTimeout
setInterval
requestAnimationFrame for component behavior
CSS animations for component behavior
```

Generic UI effects and marks are acceptable only if they are not component semantics.

---

### 12.6 `check:token-resolution-boundary`

Purpose: enforce the corrected token responsibility split.

Checks:

```text
component contracts may contain token references
final paint tree may not contain unresolved token references
component-specific files must not implement ad hoc theme/palette lookup
generic helpers are the only allowed place for token/palette/tint/alpha/mode resolution
renderer must not resolve token or palette names
```

This check may start with import and symbol restrictions, then become stronger as token types stabilize.

---

### 12.7 `check:asset-boundaries`

Purpose: prevent asset resolution from drifting into the renderer.

Checks:

```text
renderer must not read filesystem
renderer must not resolve icon tokens
component-specific files should not perform filesystem asset lookup directly
asset/icon resolution belongs to generic asset helpers
final paint tree receives resolved asset references or data URIs
```

---

### 12.8 `check:field-control-boundaries`

Purpose: prevent UI panels from inventing field controls.

Checks:

```text
every dictionary field type has a registered public control
panels do not switch on field.type to create controls
panels do not import concrete token/color/icon/media controls directly
panels do not duplicate enum options
panels do not duplicate parsing/validation/serialization
panels do not use generic pickers directly for typed fields
registered field controls may delegate internally to generic controls
```

Allowed:

```text
IconTokenFieldControl -> GenericTokenPicker
```

Forbidden:

```text
SomePanel -> GenericTokenPicker for an iconToken field
```

---

### 12.9 `check:payload-shape`

Purpose: keep host payloads free of component/device semantic leakage.

Checks:

```text
DesignPreviewPayload.device should become previewFrame
previewFrame contains only frame geometry and scale
previewFrame must not contain component-specific metrics
```

Allowed:

```text
canvasWidth
canvasHeight
screenX
screenY
screenWidth
screenHeight
scaleToPixels
```

Forbidden:

```text
statusBarHeight
safeAreaBottom
navigationBarHeight
keyboardHeight
component layout metrics
```

If system chrome needs height/layout, its own component resolver/contract owns it.

---

### 12.10 `check:override-semantics`

Purpose: protect embedded override behavior.

Tests:

```text
override remains override even if base later matches the same value
restore/inherit explicitly removes override entry
embedded child identity is preserved
copied field groups are not treated as embedded components
parent merges only direct owned child overrides
parent cannot skip child owner and merge grandchild overrides directly
```

---

### 12.11 `check:component-migration-completeness`

Purpose: prevent partial migrations.

Checks:

```text
new component cannot be added without manifest entry, contract, resolver, renderable, and validation fixture
if a component is migrated, its owned subcomponents must either be migrated with it or kept out of the new route
Bubble migration must include all owned bubble subcomponents or stay on legacy path until complete
```

This is important for Bubble because it owns multiple subcomponents.

---

## 13. Updated invariants to add near the top of the architecture document

```text
Component-specific knowledge stays in the owning component module.
Components may embed other components recursively only through declared slots.
The manifest is the source of truth for categories, entrypoints and embedded dependencies.
The registry routes only.
Generic helpers translate standard values only.
The generic renderer paints final resolved primitives only.
```

```text
The component layer owns semantic token references.
Generic preview helpers own token, palette, tint, alpha, asset, scaling and mode resolution.
The final paint tree contains resolved mode-variant values, not unresolved token names.
```

```text
After the component renderable/helper boundary, preview data must not contain
component field names, component slot names, unresolved token names, inheritance
state, override state, database/editor state, or component-specific node types.
```

```text
System, Atom and Component are manifest categories.
They must not create separate render paths.
```

```text
Typed fields are edited through their registered dictionary field controls.
Panels compose fields; they do not invent controls for field types.
Every dictionary field type must expose a registered public control, even if that control delegates internally to a generic control.
```

---

## 14. Recommended implementation order

```text
1. Update the architecture document with the corrected boundary language.

2. Introduce desktopPreviewComponents manifest:
   category
   contract
   resolver
   renderable
   embeds

3. Validate the existing registry against the manifest.

4. Replace hardcoded embedded import allowlists with manifest-based allowed imports.

5. Rename DesignPreviewPayload.device to previewFrame.

6. Define the final paint primitive allowlist.

7. Add check:preview-manifest and check:preview-import-graph.

8. Add check:paint-tree-schema and check:renderer-purity.

9. Add check:token-resolution-boundary.

10. Split asset/icon resolution out of broad renderable common helpers.

11. Add check:field-control-boundaries.

12. Move Status Bar and Navigation Bar to manifest entries with category system.

13. Add override semantics tests.

14. Before Bubble migration, enforce migration completeness for Bubble and all owned subcomponents.
```

---

## 15. Final position

The corrected architecture is:

```text
Editor schema
  -> defines fields and dictionary field types
  -> uses registered controls for typed fields

Component resolver
  -> understands component semantics
  -> resolves inheritance/overrides/slots
  -> keeps semantic token references in contract

Component renderable + generic helpers
  -> convert semantic contract to generic paint primitives
  -> resolve tokens/palette/assets/tints/alpha/modes/scales using generic helpers

Final paint tree
  -> contains generic primitives and resolved mode-variant values

Generic renderer
  -> paints primitives only
  -> may select already-resolved mode variant
  -> does not resolve tokens or know components

WebView
  -> displays HTML
```

This keeps recursion, reuse and embedded components, while preventing the old central component-aware bridge from returning under another name.
