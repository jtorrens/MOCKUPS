# Desktop Preview Component Architecture

This document is the internal final architecture for the desktop design preview
component route.

It consolidates the review in:

- `docs/exchange/responses/exchange_desktop_preview_architecture_response_v5.md`
- `docs/architecture/23_embedded_component_composition_contract.md`
- `docs/architecture/editor_shell_non_negotiables.md`

The goal is to keep component rendering recursive and reusable without
recreating a central component-aware bridge.

## Core Invariants

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
There must not be a shared `systemBar*` contract, resolver or renderable layer.
```

```text
Typed fields are edited through their registered dictionary field controls.
Panels compose fields; they do not invent controls for field types.
Every dictionary field type must expose a registered public control, even if that
control delegates internally to a generic control.
```

## Pipeline

The design preview path is:

```text
Editor / catalog schema
  -> selected component variant
  -> runtime input values
  -> component resolver
  -> component contract
  -> component renderable
  -> generic preview helpers
  -> final paint tree / renderable tree
  -> generic HTML/CSS/SVG adapter
  -> WebView
```

Short form:

```text
The editor edits fields.
Variant selection provides the effective base config.
The component understands semantics.
Generic helpers resolve shared values.
The component renderable emits paint primitives.
The generic renderer paints primitives.
The WebView displays HTML.
```

The desktop `Preview` / `Design` surface is a WebView displaying HTML/CSS.
That makes the web result the preview truth for component design, not an
Avalonia approximation.

Text rendering must therefore stay aligned with the web path. If native
HTML/CSS font handling is not stable enough for a production use case, the
candidate replacement is a generic shared text-rendering service, not a
component-specific preview workaround:

```text
text + typography + emoji policy
  -> native HTML text or generated SVG text primitive
  -> same output usable by desktop preview, web preview and final web render
```

Potential libraries/strategies to evaluate:

- Satori-style HTML/CSS to SVG text rendering;
- text-to-path libraries such as fontkit/opentype.js for normal font outlines;
- Twemoji/SVG emoji replacement for stable color emoji;
- raster output only for export paths where vector/editability is not required.

Any generated-SVG text path must be introduced as a generic paint primitive or
generic text helper. It must not give the renderer or bridge component-specific
knowledge.

The old central `webPreviewBridge` failure mode must not return. Removing the
old bridge does not mean removing all generic translation logic. It means the
generic translation layer must not become component-aware.

## Layer Responsibilities

### Catalog / Editor Schema Layer

Allowed responsibilities:

- component class metadata;
- component variant metadata;
- field names and field definitions;
- field value kinds / dictionary types;
- inheritance, override, variant selection and restore editor state;
- component category for grouping and UX;
- token references as editable data.

Forbidden responsibilities:

- HTML, CSS, SVG, React or DOM assumptions;
- component-specific visual drawing rules;
- waveform, avatar, status bar, navigation bar, bubble or keyboard layout;
- choosing concrete controls for typed fields outside the dictionary registry.

### Dictionary Field Control Layer

Any UI that edits a typed field must use the registered field control for that
dictionary field type.

This applies to:

- normal editors;
- embedded component editors;
- preview input panels;
- dialogs;
- inspectors;
- future bulk editors.

Allowed panel responsibilities:

- choose which fields are shown;
- choose grouping, order and section layout;
- show inheritance/override affordances around a field.

Forbidden panel responsibilities:

- switch on field type to create local controls;
- import concrete token/color/icon/media controls directly;
- duplicate enum options;
- duplicate parsing, validation or serialization;
- use generic pickers directly for typed fields.

Correct route:

```text
FieldDefinition
  -> ValueKind / dictionary type
  -> registered field control
  -> generic commit path
```

### Component Resolver Layer

The resolver owns component semantics.

Allowed responsibilities:

- component fields and semantic defaults;
- embedded slots;
- inheritance and override merge for owned direct child slots;
- visibility and ordering;
- component-local layout intent;
- animation frame state for the requested frame;
- semantic token references;
- runtime input interpretation.

Examples:

- Audio resolver may know waveform, progress, avatar slot and button icon slot.
- Avatar resolver may know label slot.
- Status Bar resolver may know status icons and clock semantics.
- Keyboard resolver may know key rows and key labels.

Forbidden responsibilities:

- final token lookup to concrete values;
- palette lookup;
- neutral tint application;
- selected mode application;
- design-unit to pixel scaling;
- HTML/CSS generation;
- SQLite/editor service access;
- plausible development fallbacks for missing current-model data.

Correct:

```ts
backgroundColor: { token: "theme.colors.surface" }
```

Incorrect inside component-specific resolver logic:

```ts
backgroundColor: "#181818"
```

unless the literal is an intentional fixed component value, not the result of
theme/palette lookup.

### Component Contract Layer

The component contract is the neutral boundary between resolver and renderable.

It may contain:

- resolved component structure;
- embedded child contracts;
- semantic layout decisions;
- token references;
- asset references;
- validated numbers and booleans;
- runtime input values already interpreted by the resolver.

It must not contain:

- HTML/CSS/React/DOM implementation details;
- editor control state;
- database records;
- ad hoc renderer-specific branches.

Component contracts may still contain token references. The final paint tree may
not.

### Component Renderable Layer

The renderable converts the component contract into generic paint primitives.

It may know:

- its own component contract;
- owned child component renderables declared in the manifest;
- generic paint helpers;
- generic asset/color/geometry helpers.

It must not know:

- editor controls;
- DB/editor storage;
- HTML implementation details;
- React internals;
- undeclared sibling components;
- unowned grandchildren.

Correct:

```text
Audio renderable
  -> knows how audio becomes groups, text, images and bars
  -> calls generic color/asset/geometry helpers
  -> calls Avatar renderable because Audio owns Avatar slot
```

Incorrect:

```text
Audio renderable
  -> imports Label renderable directly by skipping Avatar
  -> implements its own palette lookup
  -> emits node type "waveform_bar"
```

### Generic Preview Helper Layer

This layer replaces the useful generic parts of the old bridge without becoming
a central component-aware bridge.

Allowed responsibilities:

- theme token lookup;
- palette color lookup;
- neutral tint;
- alpha;
- mode variant resolution;
- asset/icon reference resolution;
- design unit to pixel scaling;
- generic box/placement math;
- generic surface/shadow/relief helpers;
- generic text/image/icon helpers;
- generic diagnostics.

Forbidden responsibilities:

- component class names;
- component field names;
- concrete component imports;
- component-specific layout rules;
- embedded override merging;
- registry routing.

### Final Paint Tree / Renderable Tree

After the component renderable/helper boundary, preview data should contain only
generic paint primitives and resolved values.

The allowed node primitive types are intentionally small and explicit:

```text
group
surface
path
text
image
icon
```

The shared `renderableNodeTypes` list is the code-level source of truth for
these primitives. The TypeScript `RenderableNodeType` type, the runtime Zod
schema and the HTML adapter supported-type list must derive from it. Adding a
new node type is allowed only when it is a reusable visual primitive, not a
shortcut for a component or a legacy render node.

Allowed:

- x, y, width, height;
- children;
- text;
- font data;
- resolved image/icon asset references;
- path data;
- radius;
- opacity;
- shadow;
- clip;
- fill/stroke with resolved mode variants.

Forbidden:

- component field names;
- slot names;
- unresolved token names;
- inheritance state;
- override state;
- DB/editor state;
- component-specific node types.
- component identity metadata such as `componentType` or `systemBarType`.

Forbidden examples:

```text
component_label
component_audio
status_bar_item
waveform_bar
icon_token
```

Color values should use named mode variants, not a fixed light/dark-only shape:

```ts
color: {
  variants: {
    light: "#FFFFFF",
    dark: "#111111",
    alternate: "#F8D36A"
  }
}
```

### Generic Renderer / HTML Adapter

The renderer paints final primitives.

Allowed responsibilities:

- render generic primitive node types;
- convert final paint primitives into HTML/CSS/SVG;
- select an already-resolved mode variant;
- apply CSS effects described by the paint contract;
- render debug marks generically.

Forbidden responsibilities:

- component class config;
- database/editor state;
- inheritance or overrides;
- token or palette resolution;
- component-specific layout rules;
- component-specific timers or interpolation;
- switching on debug metadata.

Animation is frame data. The renderer must not run component behavior timers,
CSS animations, countdowns or component-specific interpolation. The preview
shell may maintain a generic clock and request successive frames from the
resolver path.

The desktop editor uses `src/desktop-preview/DesktopRenderableHtmlAdapter.tsx`
as its HTML adapter. It may use React server rendering as an implementation
detail, but it must only paint the final generic desktop primitives. It must not
import or delegate to any restored legacy runtime adapter; the old
React/debug/remotion route has been removed from this repository.

## Recursive Embedded Components

Components are not isolated leaf nodes. The system intentionally supports
recursive component composition.

Example:

```text
audio
  embeds avatar
    embeds label

audio
  embeds buttonIcon
```

This means:

- Audio owns an Avatar slot.
- Audio owns a Button Icon slot.
- Avatar owns a Label slot.
- Label keeps its own component identity.

Correct import rule:

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

Allowed:

```text
Audio -> Avatar
Avatar -> Label
```

Forbidden:

```text
Audio -> Label
```

unless Audio directly declares a Label slot in the manifest.

## Embedded Override Semantics

An embedded child is not copied field groups. It is a component instance/slot
whose selected variant values can be locally overridden by the owning parent
slot.

Effective config order:

```text
component class
  -> selected variant
  -> parent slot overrides
  -> runtime inputs
```

The component class owns the variant list. A variant is a named config snapshot
stored with the class, not a separate component implementation. Every component
class must have a protected `Default` variant. `Default` cannot be renamed or
deleted. User-created variants can be duplicated, renamed and deleted only when
usage checks allow it.

Composition never uses a parent component class as the reusable visual value.
The parent class owns schema, field catalog, resolver identity, variant list and
declared child slots. Any concrete placement in another component, theme,
screen, module or future batch renderer references a concrete variant of that
class.

Selecting a component class in the editor tree must select a concrete variant:

- first selection in a session uses `Default`;
- returning to a component class uses the last selected variant for that class;
- the selected variant node is the active blue tree node;
- design preview uses the selected variant config;
- editor fields shown while a variant is selected read and write the selected
  variant config, while the owning component class supplies the field layout;
- saving a new variant from an active variant copies that variant config;
- embedded restore/inherit restores to the selected variant value.

Saving a variant is only valid from a selected variant. It must never clone a
mutable "current class values" config, because that reintroduces an ambiguous
base layer outside the variant contract.

Variant references stored inside component config are full references:

```text
componentClassId::preset::presetId
```

Short references such as `default` are migration input only. New stored config,
preview payloads and runtime composition payloads must use full references.

Invariant:

```text
An override remains an override even if the selected variant value later changes
to that same value by coincidence.
```

The override only returns to inherited state through explicit restore/inherit.

Parent components may merge overrides only for their direct owned child slots.
They must not skip child owners and merge grandchild overrides directly.

## Runtime Inputs

Component inputs are runtime inputs, not preview-only controls.

They are values supplied from outside the component while composing a frame:

- actor;
- playback state;
- duration;
- text content;
- record state;
- module-provided message/media/status data;
- future screen/shot timing data.

The design preview panel is only one producer of sample values for isolated
inspection.

Rules:

- input declarations belong to the component contract/data;
- input declarations have an explicit source:
  - `Runtime`: supplied from outside the component and visible in Design sample
    inputs;
  - `Variant`: stored by the owning component variant, edited in the parent
    variant when the parent intentionally fixes that value;
  - `Calculated`: produced by the parent resolver and hidden from editor input
    panels;
- preview may edit sample values, but must not define component-specific
  catalogs;
- record inputs must use the generic `recordReference` kind with a `tableId`,
  not specialized kinds such as `ActorReference`;
- screen/module composition must use the same declarations and provide real
  frame values;
- when a component embeds a child, the child's `Runtime` inputs automatically
  become runtime inputs of the parent unless the parent deliberately converts a
  specific value into a `Variant` decision;
- parent-owned variant bindings for embedded children must be edited through the
  generic component input-bindings dictionary control, not by copying child
  scalar fields into the parent field catalog;
- resolvers decide how input values affect component atoms;
- generic preview shell may hold generic clock/play state only;
- helpers and renderer must not infer missing inputs or know which concrete
  component declared them.

## Component Manifest

The manifest is the source of truth for:

- component class name;
- category: `system`, `atom` or `component`;
- contract entrypoint;
- resolver entrypoint;
- renderable entrypoint;
- declared embedded children;
- allowed component-to-component imports;
- migration completeness;
- registry validation.

Example:

```ts
export const desktopPreviewComponents = {
  label: {
    category: "atom",
    migrationStatus: "functional",
    contract: "./labelComponentContract",
    resolver: "./labelComponentResolver",
    renderable: "./labelComponentRenderable",
    embeds: []
  },

  avatar: {
    category: "component",
    migrationStatus: "functional",
    contract: "./avatarComponentContract",
    resolver: "./avatarComponentResolver",
    renderable: "./avatarComponentRenderable",
    embeds: ["label"]
  },

  buttonIcon: {
    category: "atom",
    migrationStatus: "functional",
    contract: "./buttonIconComponentContract",
    resolver: "./buttonIconComponentResolver",
    renderable: "./buttonIconComponentRenderable",
    embeds: ["label"]
  },

  iconRow: {
    category: "atom",
    migrationStatus: "structural",
    contract: "./iconRowComponentContract",
    resolver: "./iconRowComponentResolver",
    renderable: "./iconRowComponentRenderable",
    embeds: ["buttonIcon"]
  },

  audio: {
    category: "component",
    migrationStatus: "functional",
    contract: "./audioComponentContract",
    resolver: "./audioComponentResolver",
    renderable: "./audioComponentRenderable",
    embeds: ["avatar", "buttonIcon"]
  },

  textInputBar: {
    category: "system",
    migrationStatus: "structural",
    contract: "./textInputBarComponentContract",
    resolver: "./textInputBarComponentResolver",
    renderable: "./textInputBarComponentRenderable",
    embeds: ["surface", "iconRow"]
  },

  keyboard: {
    category: "system",
    migrationStatus: "structural",
    contract: "./keyboardComponentContract",
    resolver: "./keyboardComponentResolver",
    renderable: "./keyboardComponentRenderable",
    embeds: []
  },

  video: {
    category: "component",
    migrationStatus: "structural",
    contract: "./videoComponentContract",
    resolver: "./videoComponentResolver",
    renderable: "./videoComponentRenderable",
    embeds: []
  },

  status_bar: {
    category: "system",
    migrationStatus: "functional",
    contract: "./statusBarComponentContract",
    resolver: "./statusBarComponentResolver",
    renderable: "./statusBarComponentRenderable",
    embeds: []
  },

  navigation_bar: {
    category: "system",
    migrationStatus: "functional",
    contract: "./navigationBarComponentContract",
    resolver: "./navigationBarComponentResolver",
    renderable: "./navigationBarComponentRenderable",
    embeds: []
  }
} as const;
```

The registry may continue to exist, but it should be generated from or validated
against the manifest.

Current migrated component routes:

- `label`, `avatar`, `buttonIcon` and `audio` are active functional examples of
  the recursive route.
- `iconRow` is a structural atom that embeds `buttonIcon`; its declared inputs
  can stay runtime or be fixed by an owning parent as variant bindings. In
  `textInputBar`, size/gap/orientation are parent variant decisions and the
  ordered icon token list remains runtime data supplied by preview/screen input.
- `status_bar` and `navigation_bar` are system components in the same manifest
  route.
- `textInputBar`, `keyboard` and `video` are structurally migrated: they have
  contracts, resolvers, renderables, registry entries and dictionary-backed
  fields, but their final runtime behavior is intentionally deferred to later
  feature phases.

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
- variant selection or variant merge semantics;
- renderable construction;
- component business rules.

## System / Atom / Component Categories

`System`, `Atom` and `Component` are manifest categories only.

They are useful for:

- editor grouping;
- documentation;
- validation;
- UX;
- filtering;
- future tooling.

They must not create separate render paths.

Status Bar, Navigation Bar, Text Input Bar and Keyboard are normal component
classes with category `system`.

All categories use:

```text
componentClass
  -> resolver
  -> contract
  -> renderable
  -> generic paint tree
  -> generic renderer
```

## Generic Paint Primitive Allowlist

The final paint tree should use generic primitives only.

Current executable desktop preview primitive node types:

```text
group
surface
path
text
image
icon
```

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

Avoid:

```text
component_preview_unsupported
design_preview_surface
component_label
component_avatar
component_button_icon
component_audio
icon_token
waveform_bar
statusBar
navigationBar
keyboard
bubble
```

A waveform is audio semantics converted into generic primitives. It is not a
renderer primitive.

## Enforcement Checks

The architecture must be enforced by focused checks. Some checks may begin as
lexical checks, but the important ones should become AST/module graph checks.

### `check:preview-boundaries`

Purpose: prevent central files from becoming component-aware.

Checks:

- `webPreviewBridge.ts` must not exist;
- central preview files must not contain component class names;
- common helpers must not contain component class names or field/slot names;
- central/common files must not import concrete component modules.

### `check:preview-manifest`

Purpose: make the component catalog explicit.

Checks:

- every component class is listed in the manifest;
- every entry has category, contract, resolver, renderable and embeds;
- every listed file exists;
- every entry follows naming conventions;
- the registry matches the manifest without a second hardcoded key list;
- system/atom/component are categories only.

### `check:preview-import-graph`

Purpose: enforce component dependency boundaries.

Allowed:

- resolver -> own contract;
- resolver -> generic resolver helpers;
- resolver -> declared child resolvers;
- renderable -> own contract;
- renderable -> generic renderable helpers;
- renderable -> declared child renderables;
- registry -> component entrypoints for routing.

Forbidden:

- resolver -> renderable;
- renderable -> resolver;
- component -> undeclared sibling component;
- component -> unowned grandchild component;
- common helper -> concrete component;
- generic renderer -> concrete component;
- registry -> component internals beyond routing.

Allowed parent-to-child dependencies must come from the manifest `embeds` field.
The check derives concrete resolver/renderable entrypoints from the manifest
instead of maintaining a separate hardcoded component-name list.

### `check:paint-tree-schema`

Purpose: ensure the final paint tree stays generic.

Checks:

- node types are in the generic primitive allowlist;
- no component-specific node types;
- desktop preview renderables can emit only the generic allowlisted paint node
  types;
- no component field names;
- no slot names;
- no unresolved token names;
- no inheritance/override state;
- no DB/editor state;
- colors are resolved values with named mode variants.

### `check:renderer-purity`

Purpose: keep the web renderer generic.

Checks:

- renderer switches only on generic primitive node types;
- renderer does not import component modules;
- renderer does not contain component class names;
- renderer does not resolve token or palette names;
- renderer does not branch on debug metadata;
- renderer does not run component behavior timers.

### `check:token-resolution-boundary`

Purpose: enforce token responsibility split.

Checks:

- component contracts may contain token references;
- final paint tree may not contain unresolved token references;
- component-specific files must not implement ad hoc theme/palette lookup;
- generic helpers are the only allowed place for token/palette/tint/alpha/mode
  resolution;
- renderer must not resolve token or palette names.

### `check:asset-boundaries`

Purpose: prevent asset resolution from drifting into the renderer.

Checks:

- renderer must not read filesystem;
- renderer must not resolve icon tokens;
- component-specific files should not perform filesystem asset lookup directly;
- asset/icon resolution belongs to generic asset helpers;
- final paint tree receives resolved asset references or data URIs.

### `check:field-control-boundaries`

Purpose: prevent UI panels from inventing field controls.

Checks:

- every dictionary field type has a registered public control;
- panels do not switch on field type to create controls;
- panels do not import concrete token/color/icon/media controls directly;
- panels do not duplicate enum options;
- panels do not duplicate parsing/validation/serialization;
- panels do not use generic pickers directly for typed fields;
- registered field controls may delegate internally to generic controls.

### `check:payload-shape`

Purpose: keep host payloads free of component/device semantic leakage.

Checks:

- `DesignPreviewPayload.device` should become `previewFrame`;
- `previewFrame` contains only frame geometry and scale;
- `previewFrame` must not contain component-specific metrics.

Allowed:

- canvasWidth;
- canvasHeight;
- screenX;
- screenY;
- screenWidth;
- screenHeight;
- scaleToPixels.

Forbidden:

- statusBarHeight;
- safeAreaBottom;
- navigationBarHeight;
- keyboardHeight;
- component layout metrics.

If system chrome needs height/layout, its own component resolver/contract owns
it.

### `check:override-semantics`

Purpose: protect embedded override behavior.

Tests:

- override remains override even if base later matches the same value;
- restore/inherit explicitly removes override entry;
- embedded child identity is preserved;
- copied field groups are not treated as embedded components;
- parent merges only direct owned child overrides;
- parent cannot skip child owner and merge grandchild overrides directly.

### `check:component-preset-semantics`

Purpose: protect component variants as the effective base layer for component
editing and preview.

Checks:

- every component class has a protected `Default` variant;
- protected variants cannot be renamed or deleted;
- selecting a component class resolves to a concrete variant;
- first session selection resolves to `Default`;
- returning to a component class in the same session resolves to the last
  selected variant for that class;
- selected variant is the blue active tree node;
- design preview payload for a variant uses the variant config, not mutable class
  config;
- editor field commits for a selected variant write to that variant config, not
  mutable class config;
- saving a variant is only accepted from a selected variant and copies that
  variant config;
- persisted embedded variant references use `componentClassId::preset::presetId`,
  not short local preset ids;
- embedded restore/inherit uses the selected variant as base;
- deleting a variant is blocked while any component class slot or any component
  variant slot references it.

The check name and persisted reference spelling still use `preset` because the
storage contract has not yet been renamed. User-facing terminology and new
architecture prose should say `variant`.

### `check:component-migration-completeness`

Purpose: prevent partial migrations.

Checks:

- new components require manifest entry, contract, resolver, renderable and
  validation fixture;
- if a migrated component owns embedded child components required for its visual
  structure, those children must also have a manifest entry and participate
  through the declared component route;
- do not reimplement child visuals locally as a shortcut;
- Bubble migration must include all owned bubble subcomponents or stay on legacy
  path until complete.

## Implementation Roadmap

### Phase 1: Freeze and Document the Contract

Goal: make this architecture the source of truth before further component work.

Tasks:

- add this document;
- cross-reference it from `editor_shell_non_negotiables.md`;
- update `AGENTS.md` if needed so future agents read this document before
  preview/component changes;
- keep current code behavior unchanged.

Validation:

- documentation only;
- no UI review needed.

### Phase 2: Introduce Component Manifest

Goal: make component ownership explicit.

Tasks:

- create `desktopPreviewComponents` manifest;
- list current migrated components:
  - label;
  - avatar;
  - buttonIcon;
  - audio;
  - textInputBar;
  - keyboard;
  - video;
  - status_bar;
  - navigation_bar;
- include `category`, `contract`, `resolver`, `renderable`, `embeds`;
- validate current registry against the manifest;
- replace hardcoded embedded import allowlists with manifest-derived rules.

Validation:

- build;
- `npm run check:architecture`;
- no expected UI behavior change.

### Phase 3: Rename Preview Frame Shape

Goal: remove device semantics from the generic preview payload.

Tasks:

- rename `DesignPreviewPayload.device` to `previewFrame`;
- keep only frame geometry and scale;
- move any system chrome metrics into system component contracts;
- update callers and tests.

Validation:

- build;
- preview still opens for Label, Avatar, Button Icon and Audio;
- device/theme/mode selectors still work.

### Phase 4: Split Generic Preview Helpers

Goal: reduce the broad common helper module without changing behavior.

Tasks:

- split broad renderable common helpers into focused modules:
  - color/token helpers;
  - geometry/placement helpers;
  - surface/shadow/relief helpers;
  - asset/icon resolver;
  - diagnostics;
- split resolver-side helpers into focused modules for component contracts,
  JSON parsing and value validation;
- keep helpers generic;
- ensure no helper imports concrete components.
- enforce that shared helper names are imported from common modules rather than
  redefined locally in component/resolver/renderable files.

Validation:

- build;
- architecture check;
- visual smoke for Label, Avatar, Button Icon and Audio.

### Phase 5: Final Paint Primitive Allowlist

Goal: make the renderer primitive-only.

Tasks:

- define the final paint primitive schema;
- reject component-specific node types;
- convert any remaining semantic node types to generic primitives;
- keep debug metadata render-inert.

Validation:

- architecture check;
- renderer-purity check;
- visual review with marks on/off.

### Phase 6: Add Stronger Enforcement

Goal: prevent future drift.

Tasks:

- add or expand:
  - `check:preview-manifest`;
  - `check:preview-import-graph`;
  - `check:paint-tree-schema`;
  - `check:renderer-purity`;
  - `check:token-resolution-boundary`;
  - `check:asset-boundaries`;
  - `check:field-control-boundaries`;
  - `check:override-semantics`;
  - `check:component-migration-completeness`.

Validation:

- `npm run check:architecture`;
- checks fail on intentional temporary violations in local test fixtures, then
  pass after fixtures are removed or inverted.

### Phase 7: Move System Chrome Into Manifest Route

Status: complete for desktop preview routing.

Goal: make Status Bar and Navigation Bar normal system components.

Tasks:

- Status Bar is a manifest entry with category `system`;
- Navigation Bar is a manifest entry with category `system`;
- there is no shared `systemBar*` contract/resolver/renderable layer;
- their resolvers own their own layout metrics.

Non-preview follow-up resolved for the desktop spike:

- the desktop editor no longer seeds, edits or queries legacy
  `status_bars`/`navigation_bars` rows;
- the physical tables remain only as persistence/schema compatibility for
  non-desktop/runtime code until that layer is redesigned;
- status/navigation editing and theme selection must go through component class
  variants.
- component class record-class ids for migrated components use the current
  manifest/component names. For example, use `component.buttonIcon` and
  `component.textInputBar`, not legacy `component.button_icon` or
  `component.text_input_bar`.

Validation:

- design preview status/nav match current behavior;
- no status/nav branches in generic renderer or helpers.

### Phase 8: Component Migration Discipline

Goal: continue new components without reintroducing legacy paths.

Tasks:

- before adding/migrating a component, declare:
  - contract;
  - resolver;
  - renderable;
  - manifest entry;
  - embedded children;
  - runtime inputs;
  - visual validation fixture;
- migrate owned children together when they are required for visual structure;
- do not reimplement child visuals locally.

Validation:

- build;
- architecture check;
- UI review for the migrated component and its embedded children.

### Phase 9: Bubble Gate

Goal: avoid the highest-risk partial migration.

Tasks:

- do not migrate Bubble until its owned subcomponents are identified;
- document all owned children and runtime inputs first;
- migrate Bubble only when all required children can participate through the
  declared component route.

Validation:

- explicit migration checklist before code changes;
- no mixed legacy/new Bubble path.

## Practical Rule for Future Work

If a change appears to require:

```text
if componentType == ...
```

inside generic helpers, renderer, shell, registry logic or preview panels, stop.

Move the responsibility to:

- the component resolver;
- the component renderable;
- a generic parameterized helper;
- the manifest, if the issue is routing or declared ownership.

Do not add local exceptions to make one component work.
