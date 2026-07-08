# Desktop Preview Architecture Alignment Response v4

This response reviews `exchange_desktop_preview_architecture_response_v3.md`
against the architecture currently defined in the Avalonia/Suki desktop editor
cleanup branch.

The goal is not to implement anything yet. The goal is to align terminology,
responsibilities, and enforcement rules before the next preview/component
migration phase.

## Summary Position

The v3 proposal is mostly aligned with the direction of the project:

- component-specific knowledge stays inside the owning component module;
- the registry routes only;
- the generic renderer paints only generic visual primitives;
- typed editor fields must be rendered through dictionary field controls;
- Status Bar, Navigation Bar, and Keyboard should be system-category component
  classes, not separate render architectures;
- enforcement should move beyond lexical checks toward manifest/import/schema
  validation.

However, four points need clarification before adopting the proposal as-is:

1. Components must not resolve theme/palette tokens to final color values.
2. Avoiding the old central `bridge` does not mean duplicating generic
   translation logic inside each component.
3. Color variants must not be hardcoded as only `light` / `dark`.
4. Components are explicitly allowed to embed other components recursively, but
   only through declared ownership relationships.

The fourth point is the most important conceptual correction.

## 1. Token Resolution Responsibility

The v3 document says that the component layer owns `token resolution`.

That should be corrected.

In the current architecture, component resolvers may decide which token a
semantic field uses, but they must not resolve that token to a concrete value.

Correct responsibility split:

```text
component resolver
  -> understands component semantics
  -> merges inheritance / overrides
  -> decides slots, layout, visibility, order
  -> keeps token references in the component contract

generic preview helpers / renderable translation
  -> resolve theme tokens
  -> resolve palette colors
  -> apply neutral tint
  -> apply alpha
  -> apply selected mode variant
  -> apply design-unit to pixel scaling

generic renderer
  -> paints final resolved values
```

So the component may produce:

```ts
backgroundColor: { token: "theme.colors.surface" }
```

but the final paint/renderable tree should receive:

```ts
backgroundColor: {
  variants: {
    light: "#F5F5F5",
    dark: "#181818"
  }
}
```

or the equivalent resolved structure used by the final paint contract.

The component should not perform the token lookup itself.

Recommended wording:

```text
The component layer owns token references and semantic use of tokens.
Generic preview helpers own token, palette, tint, alpha, and mode resolution.
The final paint tree contains only resolved values.
```

## 2. Bridge Terminology vs Generic Translation

The v3 proposal recommends avoiding `bridge` as the conceptual name for the
middle layer. That is reasonable.

The previous central bridge became dangerous because it started accumulating
component-specific functions such as:

```text
labelComponentToRenderable
avatarComponentToRenderable
audioComponentToRenderable
statusBarToRenderable
navigationBarToRenderable
```

That pattern must not return.

However, removing the name `bridge` must not mean moving generic translation
work into every component.

The architecture still needs a generic translation/helper layer for operations
that are common to all components:

- token and palette resolution;
- alpha and neutral tint handling;
- generic placement math;
- design pixels to final pixels;
- shadow, relief, surface, text, image and icon helpers;
- generic diagnostics for missing required resolved values;
- asset/icon reference resolution through dedicated generic asset helpers.

Recommended wording:

```text
Avoid a central component-aware bridge.
Keep a generic renderable/paint-tree helper layer.
Component renderables may call generic helpers, but those helpers must not
import concrete components or contain component-specific rules.
```

This keeps the old bridge failure mode out of the system without duplicating
generic algorithms inside each component.

## 3. Color Mode Variants

The v3 document uses `light` and `dark` pairs in examples.

That is acceptable as an example, but it should not become the contract.

The current design direction is more general: a color can provide a set of named
mode variants. Today those variants may be `light` and `dark`; later they may
include `alternate`, `set`, `outdoor`, or any other production mode.

Prefer a mode-variant map:

```ts
color: {
  variants: {
    light: "#FFFFFF",
    dark: "#111111",
    alternate: "#F8D36A"
  }
}
```

over a fixed pair:

```ts
color: {
  light: "#FFFFFF",
  dark: "#111111"
}
```

Renderer responsibility stays narrow:

```text
The renderer may select an already-resolved variant by mode.
The renderer must not resolve token names or palette names.
```

## 4. Recursive Embedded Components

The main concept that v3 does not fully capture is that components are not
isolated leaf nodes.

The current architecture intentionally supports recursive embedded components.

This is central to the design system.

For example:

```text
audio
  embeds avatar
    embeds label

audio
  embeds buttonIcon
```

That means:

- Audio owns an Avatar slot.
- Audio owns a Button Icon slot.
- Avatar owns a Label slot.
- Label remains its own component with its own fields, controls, resolver and
  renderable path.

The system is recursive, but ownership must be explicit.

Correct rule:

```text
A component may import and compose another component only when that child is
declared as an embedded component in the parent's manifest entry.
```

Incorrect rule:

```text
No component may import another component.
```

That would be too strict and would break the embedded component system.

Correct enforcement should be:

```text
Allowed:
  parent component -> declared child component

Forbidden:
  component -> undeclared sibling component
  component -> grandchild component by skipping its direct child owner
  common helper -> concrete component
  generic renderer -> concrete component
  registry -> component internals beyond routing entrypoints
```

Example manifest:

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
  }
} as const;
```

This allows recursive composition while preventing arbitrary cross-component
coupling.

## Embedded Override Semantics

Embedded components must keep their own component identity.

An embedded Label inside Avatar is not copied label fields. It is a Label
component instance/slot whose base class values can be overridden locally by the
owning parent slot.

Important rule:

```text
An override remains an override even if the parent/base value later changes to
the same value by coincidence.
```

The override exists because the user made an explicit local decision. It only
returns to inherited state when the user explicitly restores inheritance.

This should be enforced with tests.

## Manifest Role

The manifest should be more than a component list.

It should be the authoritative map for:

- component category;
- contract/resolver/renderable entrypoints;
- declared embedded children;
- allowed component-to-component imports;
- migration completeness checks;
- registry validation.

The registry may still exist, but it should be generated from or validated
against the manifest.

Allowed registry responsibility:

```text
componentClass -> owning component entrypoint
```

Forbidden registry responsibilities:

- layout decisions;
- style decisions;
- token resolution;
- default values;
- embedded override merging;
- renderable construction;
- component business rules.

## Status Bar, Navigation Bar and Keyboard

The v3 proposal is aligned here.

Status Bar, Navigation Bar and Keyboard should become component classes with
category `system`.

System means metadata/category, not a separate rendering architecture.

Correct:

```text
statusBar
  category: system
  contract/resolver/renderable
  emits generic paint primitives
```

Incorrect:

```text
system renderer
atom renderer
component renderer
```

All categories use the same route:

```text
componentClass
  -> resolver
  -> contract
  -> renderable
  -> generic paint tree
  -> generic renderer
```

## Generic Paint Tree

The v3 proposal is aligned with removing component-specific node types.

The final paint/renderable tree should not contain node types like:

```text
component_label
component_avatar
component_button_icon
component_audio
waveform_bar
statusBar
navigationBar
```

Preferred primitives:

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
barSeries, only if it is genuinely generic
```

Debug metadata is acceptable only if it cannot affect rendering:

```ts
metadata: {
  sourceComponent: "audio",
  sourcePart: "waveform"
}
```

The renderer must not switch on that metadata.

## Field Control Boundary

The v3 proposal is aligned and should be accepted.

Any UI surface that edits a typed field must use the dictionary field
type/control registry.

That includes:

- normal editors;
- embedded component editors;
- preview input panels;
- dialogs;
- inspectors;
- future bulk editors.

Panels may choose which fields to show and how to group them, but they must not
create ad hoc controls for typed values.

Correct:

```text
field definition
  -> value kind / dictionary type
  -> registered field control
  -> generic commit path
```

Incorrect:

```text
panel sees field.type === "colorToken"
  -> creates a color/token picker directly
```

## Enforcement Adjustments

The v3 enforcement plan is useful, with these adjustments:

### Accept

- `check:preview-boundaries`
- `check:preview-manifest`
- `check:field-control-boundaries`
- `check:preview-import-graph`
- `check:paint-tree-schema`
- `check:renderer-purity`
- `check:payload-shape`
- `check:asset-boundaries`
- `check:override-semantics`
- `check:component-migration-completeness`

### Adjust

Import graph checks must allow parent-to-child imports declared in `embeds`.

They must reject:

```text
undeclared component imports
grandchild imports that skip the declared child owner
concrete component imports from common helpers
concrete component imports from the generic renderer
component-specific rules in registries
```

Paint-tree checks should reject unresolved tokens in the final paint tree, but
not in earlier component contracts.

Color checks should accept resolved named mode variants, not only fixed
`light` / `dark` pairs.

## Recommended Updated Invariants

Add these invariants near the top of the architecture document:

```text
Component-specific knowledge stays in the owning component module.
Components may embed other components recursively only through declared slots.
The manifest is the source of truth for allowed embedded dependencies.
The registry routes only.
Generic helpers translate standard atoms and values only.
The generic renderer paints final resolved primitives only.
```

```text
After the component renderable/helper boundary, preview data must not contain
component field names, component slot names, unresolved token names, inheritance
state, override state, database/editor state, or component-specific node types.
```

```text
Token references belong above the generic resolution boundary.
Resolved mode-variant values belong below it.
The renderer may select a resolved mode variant but must not resolve tokens.
```

```text
System, Atom and Component are manifest categories.
They must not create separate render paths.
```

```text
Typed fields are edited through their registered dictionary field controls.
Panels compose fields; they do not invent controls for field types.
```

## Recommended Implementation Order

If this direction is accepted, the safest order is:

1. Update the architecture document with the corrected boundary language.
2. Introduce the `desktopPreviewComponents` manifest.
3. Validate the current registry against the manifest.
4. Add manifest-based allowed embedded imports.
5. Rename `DesignPreviewPayload.device` to `previewFrame`.
6. Split asset/icon resolution out of broad renderable common helpers.
7. Tighten paint-tree primitive allowlist.
8. Add renderer-purity and paint-tree-schema checks.
9. Move Status Bar and Navigation Bar to manifest entries with category
   `system`.
10. Before Bubble migration, enforce that all owned subcomponents migrate with
    it or stay out of the new route.

## Final Position

The v3 document is a good base, but it should not be adopted without clarifying
recursive embedded components and token-resolution ownership.

The corrected mental model is:

```text
component contract/resolver
  -> may own declared embedded child components recursively
  -> produces standard component atoms/contracts with token references

component renderable + generic preview helpers
  -> resolve generic tokens/assets/geometry/modes
  -> produce generic paint primitives with final resolved values

generic renderer
  -> paints those primitives only
```

This keeps the system reusable and recursive without recreating a central
component-aware bridge.
