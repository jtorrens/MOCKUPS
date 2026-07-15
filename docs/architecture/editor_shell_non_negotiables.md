# Editor shell non-negotiables

This document defines the architectural rules for the next editor shell. It is meant to be read before every implementation phase of the desktop editor spike.

The goal is not to describe the current code perfectly. The goal is to protect the model we want.

## 0. Read-before-change rule

Before modifying the Avalonia/Suki desktop editor spike, read this document and apply it as a checklist.

The short operational version also lives in the repository root as `AGENTS.md` so future Codex sessions see the same constraints.

For the cleanup order and migration guardrails, also read:

- `docs/architecture/editor_modernization_roadmap.md`
- `docs/architecture/23_embedded_component_composition_contract.md`
- `docs/architecture/24_desktop_preview_component_architecture.md`
- `docs/architecture/25_component_migration_status.md`

Two rules override local convenience:

1. `MainWindow` is shell-only. It must not accumulate editor-specific logic.
2. Editable fields go through `FieldDefinition` and dictionary controls. If the dictionary cannot express the field yet, extend the dictionary first.
3. Generic routines live in common/shared code. If an algorithm can be reused by more than one editor, resolver, bridge, renderer, importer, or repository, extract it before using it.
4. Before creating any helper that could be generic, check `spikes/desktop-editor-shell/Common` and the base-routines audit for an existing equivalent. Reuse or extend common first.
5. Component-specific preview decisions must not cross preview boundaries. Component resolvers own component composition; the bridge translates standard atoms; the web renderer paints final resolved nodes.
6. Component inputs are runtime component inputs, not preview-only controls. Preview may provide sample values for them, but the same input contract must feed screen/frame composition.

If a requested change appears to require breaking any of these rules, stop and clarify the architecture before implementing.

## 1. Editor and runtime are separate systems

The editor edits structured data. It does not own the final visual rendering.

The web runtime remains the source of truth for:

- preview;
- frame rendering;
- screen/module visual composition;
- animation playback;
- future runtime modules.

The desktop editor may embed, control, or feed the web runtime, but it must not duplicate it.

```text
Desktop editor shell
  tree
  property editors
  dictionary controls
  component override editors
  commands
  validation

        ↓ resolved data / frame model

Web runtime
  screens
  modules
  components
  preview
  render
```

## 2. FieldDefinition owns what the value is

Every editable field must have a field definition.

A field definition declares:

- stable field id;
- logical label;
- value kind;
- whether it is inheritable;
- default or inherited source;
- validation rules;
- editor hints that are truly semantic, not visual hacks.

If a value is editable, it should not appear in the UI without a field definition.

Internal/calculated fields may exist, but then they should be explicitly marked as internal/calculated and not silently edited as ordinary fields.

## 2A. Animatable state belongs to field definitions

Fields that can participate in animation must declare it in their field definition.

Animatable support is not an editor-specific decoration. It is metadata of the field.

The field definition should declare:

- whether the field is animatable;
- which interpolation modes are valid;
- whether the animated value is frame-relative, screen-relative, or module-relative;
- whether the value can be keyframed directly or only through a higher-level routine.

Examples:

- message text can be animated with text interpolation/write-on semantics;
- delivery status can be animated as hold-only enum changes;
- numeric offsets can support linear/ease interpolation;
- palette/theme token colors should usually be hold-only unless a future color interpolation system is explicitly designed.

The animation editor may show keyframe controls next to animatable fields, but it must derive that affordance from the field definition.

## 3. ValueKind owns how a value is edited

The field does not decide which control to paint manually.

The value kind resolves to an editor control through the value/control registry.

Examples:

- `string.singleLine`
- `string.multiline`
- `number.integer`
- `number.decimal`
- `boolean`
- `enum`
- `directoryPath`
- `filePath`
- `pair.xy`
- `pair.lightDarkColor`
- `paletteColorToken`
- `themeColorToken`
- `fontFamily`
- `fontWeight`
- `fontStyle`
- `iconToken`
- `iconTokenList`
- `recordReference` (`tableId`, record id, display name)
- `surfaceStyle`
- `componentOverride`

Adding a new kind means adding its validation and its control in the dictionary layer, not inside a random editor.

Padding and gap fields must use theme spacing tokens. Do not introduce raw numeric padding fields for component/editor values that represent visual spacing. If a spacing value needs X/Y axes, use a spacing-token pair.

## 4. Controls own their own visual invariants

If a control comes from the dictionary, its visual invariants are owned by that control.

That includes:

- control border;
- control radius;
- internal layout;
- restore button placement;
- multi-control layout;
- debug styling during migration;
- focus behavior;
- multiline behavior;
- picker/modal trigger placement.

An editor may provide layout slots and section structure. It may not restyle the internals of a dictionary control.

If a control looks wrong, fix the control class, not the editor that happens to use it.

## 5. No parallel manual control route

There must not be two ways to paint the same concept:

- dictionary route;
- legacy/manual editor route.

During migration, a field may temporarily be unavailable rather than being recreated by hand.

Manual controls are allowed only for genuinely custom editor chrome, not for value editing.

Examples of allowed custom chrome:

- section headers;
- tree rows;
- toolbar buttons;
- preview navigation shell;
- modal frame.

Examples of disallowed manual value controls:

- ad hoc text input for a dictionary string;
- ad hoc dropdown for an enum;
- ad hoc color picker outside the color control;
- ad hoc X/Y layout outside the pair control.

## 5A. Shared editor organization vocabulary

Editor cards organize fields and child cards through layout metadata. Their
organization must not be selected by record class, hierarchy depth, field
count, label text or a one-off Avalonia exception.

The shared vocabulary is:

- `stacked`: ordinary sequential groups using the standard shared group
  surface;
- `flatStack`: repeated sibling objects inherit the parent card surface and
  are separated by full-width rules, without additional elevation or a lower
  color tier;
- `verticalCards`: vertical internal navigation on the left and the selected
  child content on the right; this is the implementation name for the vertical
  tab treatment;
- `separatedSections`: continuous content where semantic groups are divided by
  a label followed by a horizontal rule, without nested subcard chrome;
- group-level `presentation`: an explicit override that permits one card to
  combine organizations without teaching the renderer about that editor.

When fields belong directly to an owner that also has child cards, the direct
fields use a generic `General` child card. Its own semantic groups use
`separatedSections`. A selected section must not repeat a heading that is
already supplied by the selected navigation item.

Internal navigation is a shared control. It owns keyboard navigation,
selection, responsive content placement, dividers and session state. Editors
only provide sections and metadata.

Simplified editing is also a shared metadata projection. Component editor
layouts may declare promoted direct, embedded and structured-collection fields,
but the Simplified surface must resolve them through their existing
`FieldDefinition`, ValueKind, dictionary control and commit route. The selected
Simplified/Complete mode is session-only. Embedded Simplified defaults are
materialized as a parent-owned snapshot exactly once; they are never live UI
inheritance. A provenance lock may identify a captured entry, but it must not
make the underlying field read-only or couple later child changes back into the
parent projection.

While space permits, `verticalCards` is a two-panel surface with an always
visible draggable vertical splitter. Its responsive threshold is derived from
the selected navigation width plus splitter width plus the minimum content
width; it must not use an editor-specific or fixed-window breakpoint. Below
that threshold the same sections become horizontal tabs. The last vertical
width and selected section are session-only. The control uses natural height,
not fill-height, unless its containing layout explicitly requests fill. In the
vertical presentation that natural height is the maximum of the navigation and
selected-content heights. Splitters and decorative dividers stretch only when
arranged and must not contribute the available viewport height to measurement.

Shared dictionary selectors must accept the width assigned by their host. They
must not impose a field-level minimum width: selected text stays on one line,
contracts with character ellipsis and remains clipped inside the selector while
the containing grid reserves its trailing action columns. Independent surfaces
such as dialogs or preview toolbars may declare their own explicit minimum.
Inline dictionary labels follow the same rule: their shared column contracts
with the field, the label remains on one line and uses character ellipsis. A
compound dictionary value with multiple actions owns its responsive layout and
moves those actions to a second row when its declared content and action minima
no longer fit. The host and individual editors must not clip those actions or
introduce editor-specific breakpoints.

Dictionary editability also owns presentation. When a `FieldDefinition` is not
editable, the shared dictionary host disables and visibly attenuates the value
control for every registered value kind. A custom control must not remain
visually active while merely ignoring input. Labels and external contract
actions such as a Forward indicator remain outside that disabled value surface.

Cross-input normalization is declared by component-input transition metadata.
The generic embedded-input control may update a related Forward value as part of
the same edit, including its session test value. Component names and field ids
must not be hard-coded into the shared control, and resolvers must not use this
UI behavior as a compatibility fallback.

Complex dictionary controls such as `ComponentInputBindings` and
`StructuredCollection` use full-width block layout. A separator precedes the
block, the optional field label sits above the control and no closing separator
is emitted. This layout belongs to the shared dictionary host, not to the
calling editor.

A `StructuredCollection` field may receive its schema directly from its
`FieldDefinition` when the collection is Variant-owned, or derive that schema
from a selected component Variant when editing a parent boundary. Both routes
must use the same generic collection surface, item controls and commit path.

All editor cards start closed in a fresh application session. Expansion,
internal selection and scroll position may be restored when returning to the
same editor during that session, but must never be persisted between
application sessions.

Opening an embedded Override must capture the complete return context. Owner
breadcrumbs restore the previous internal selection, expanded cards, scroll
position and session-only navigation widths for both ordinary embedded slots
and runtime-root overrides.

Compound dictionary controls keep ownership of their internal presentation.
The shared row host assigns every value control the real remaining width after
its field label and outer action. A compound control must then reserve its own
trailing actions before measuring flexible content: selected Component/Variant
text and Typography summaries truncate with ellipsis while Open, Override and
chevron actions remain visible. Do not recover from narrow panels with a local
editor width, clipping exception or horizontal viewport.

Generic two-value controls are responsive units. `IntegerPair` and
`ThemeTokenPair` remain horizontal while their two labelled values fit, then
switch to two internal rows below their content minimum. Their logical value,
commit path and field id do not change when presentation reflows.

For `PaletteColorPair`, a group may opt into `pairLayout: sharedHeader`:

- `Light` and `Dark` appear once as shared column headings;
- every pair remains in two columns, including at narrow widths;
- selector text clips with ellipsis instead of widening or stacking the pair;
- repeated per-row Light/Dark labels and pair borders are removed by the
  dictionary control itself, not by Theme or another editor.

Semantic editor icons come from reusable files under
`assets/system/system_icons/components`. Add a dedicated asset when an existing
semantic icon is not appropriate; do not draw a local icon inside an editor.

## 5B. Embedded components are recursive component slots

Embedded components must follow the component composition contract in
`docs/architecture/23_embedded_component_composition_contract.md`.

The short version:

- a parent component owns a slot and slot-local overrides;
- the child component keeps its own field catalog, controls, resolver and bridge;
- the parent must not copy the child's scalar fields into its own field catalog;
- amber override state means "an override entry exists", not "effective value
  differs from the base";
- inherited/restored fields remove the override entry;
- resolvers merge child base config plus slot overrides before the bridge;
- the bridge resolves tokens, palette colors and device pixels;
- the web renderer paints final visuals and must not understand inheritance.

This is intentionally recursive. A child component can later embed another
component through the same slot/override mechanism.

## 5C. Preview boundaries must not leak component knowledge

Preview work has three separate responsibilities:

```text
component resolver
  -> standard resolved atoms
  -> bridge
  -> final web renderable nodes
  -> web renderer
```

The component resolver owns all component-specific decisions:

- which children exist;
- component-local layout;
- component-specific defaults after inheritance/overrides are merged;
- semantic meaning of fields such as audio waveform bars, label subtext, avatar badge, navigation button mode, or bubble tail shape;
- composition of embedded components.

The bridge owns only generic translation:

- token/palette/alpha/neutral tint resolution;
- design units to final pixels;
- generic placement math;
- generic atoms such as boxes, text, SVG/image/video, surfaces, shadows, relief and marks;
- diagnostic errors for unresolved required values.

The bridge must not contain `componentType` branches, hardcoded field names, or layout/business rules for a concrete component class. If a component cannot be represented with current atoms, add a generic atom or extend the component resolver output first.

There must not be a central web preview bridge acting as a component catalog. Do not add functions such as `labelComponentToRenderable`, `avatarComponentToRenderable`, `audioComponentToRenderable`, `statusBarToRenderable`, `navigationBarToRenderable`, or any equivalent per-component bridge entry point to a shared bridge file. All component classes, including category `system` classes such as status/navigation/text input/keyboard, use their own resolver/renderable modules and are selected only through an explicit registry. There must not be a shared `systemBar*` contract, resolver or renderable layer.

A registry may name components only to route to their owning module. It must not contain component layout, style, defaults, token resolution, or renderable construction logic.

Each migrated component must keep this shape:

```text
component contract/resolver
  -> component renderable module
  -> common preview helpers
  -> generic web renderer
```

Common preview helpers must not import concrete component resolvers/renderables or contain concrete component names. Embedded component imports are allowed only when the parent component explicitly owns that child slot, for example avatar -> label or audio -> avatar/button icon.

## 5D. Component inputs are not preview-only

Inputs exposed by a component are part of the component runtime contract.

They represent values supplied from outside the component while composing a
frame, for example actor, playback state, duration, text content, record state
or future module-provided data. The design preview panel is only one temporary
producer of those values so the component can be inspected in isolation.

The route must stay generic:

```text
component input declarations
  -> preview/sample input values or screen/module input values
  -> component resolver
  -> frame-specific component contract
  -> bridge/renderable pipeline
```

The preview shell may render controls from the declarations and may maintain
generic animation clock state declared by those inputs. It must not contain
component-specific input catalogs, playback rules, actor rules, waveform rules,
label rules, badge rules, or any equivalent branch for a concrete component.

When a screen is composed, it must use the same component input declarations
and provide real screen/module values instead of preview sample values.

Run `npm run check:architecture` before closing any preview/component migration phase. The check must fail if component-specific names or imports leak into central preview files, common helpers, or undeclared component dependencies.

When touching a migrated component, the preferred direction is:

```text
component resolver
  -> standard atoms already containing component layout/style decisions
  -> generic bridge helpers
  -> generic web renderable nodes
```

Do not add new bridge branches for component fields like `waveformBarCount`, `labelSlot`, `avatarSlot`, `playCircleSize`, or component-specific defaults. Move those decisions to the resolver or to a common parameterized helper used by the resolver.

The web renderer is the narrowest layer. It must not know:

- database records;
- component class config;
- inheritance or overrides;
- theme token names or palette tokens;
- component-specific layout rules;
- plausible fallbacks for missing values.

The web renderer may add support for a new generic primitive, but that primitive must receive final resolved style/data. It must not infer behavior from component names such as `label`, `avatar`, `buttonIcon`, `audio`, `video`, `bubble`, `statusBar`, or `navigationBar`.

Animation follows the same boundary. Resolvers own component state for the requested frame, and the bridge may translate that resolved frame into final pixels. The web preview/render layer must not run timers, CSS animations, countdowns, or component-specific interpolation. For web preview, an animated component is only a succession of resolved frames.

## 5E. `MainWindow` is shell-only

`MainWindow` may orchestrate the desktop shell, but it must not implement individual editors.

Allowed responsibilities:

- window initialization;
- high-level three-panel composition;
- selected tree node state;
- navigation tree refresh and selection wiring;
- editor card composition from generic layout metadata;
- preview panel wiring;
- generic modal hosting/delegation;
- persisted window/panel visual state.

Disallowed responsibilities:

- editor-specific field construction;
- editor-specific collection rows;
- table-specific business rules;
- domain-specific pickers or dialogs;
- SVG/icon/media/font/palette logic specific to one editor;
- one-off layout fixes for a specific editor.

If a behavior is needed by one editor only, it belongs in that editor's class. If it can be reused, it belongs in a shared editor-shell class. `MainWindow` should instantiate/delegate, not implement.

This applies especially to the Avalonia/Suki spike at:

- `spikes/desktop-editor-shell/MainWindow.axaml.cs`
- `spikes/desktop-editor-shell/EditorShell/`

The target shape is:

```text
MainWindow
  shell state
  tree/editor/preview wiring
  generic card rendering
  generic dialog host

EditorShell/*
  editor-specific classes
  reusable collection editors
  dictionary controls
  picker/dialog classes
  preview helper controls
```

## 5F. Field commits are a shared editor-shell behavior

Dictionary controls may emit local editing events while the user types, selects, or while Avalonia initializes a control. Those local events must not be treated as final record commits by each editor independently.

The desired general behavior is:

- the control owns temporary edit state;
- the record is updated when the field edit is committed, normally on field exit, picker accept, checkbox toggle, or an explicit commit gesture;
- editors do not implement their own per-character persistence rules;
- rebuilding an editor in response to a field commit must never create a feedback loop where control initialization commits the same value again;
- if an emitted value is equal to the stored value, the shell must ignore it before updating the record or rebuilding the editor.

Current spike note: the Actor editor freeze exposed this problem. Avatar controls emitted `ValueChanged` during initialization, and the Actor editor rebuilt itself on every avatar change. A same-value guard stops the immediate loop, but the real fix is to generalize commit-on-field-exit / commit-on-accepted-change for all dictionary controls and all editors.

## 6. Visual style is token-based

The editor shell may have its own UI tokens, but they must be centralized.

Controls should not hardcode colors, borders, shadows, or radii except as temporary debug markers during a clearly named migration phase.

The runtime visuals must resolve through:

- palette;
- theme tokens;
- component defaults;
- component overrides;
- module/app/screen inheritance where applicable.

No primitive color literals should leak beyond palette/theme definition layers.

## 6A. Generic routines live in common libraries

Reusable behavior must not be implemented inside the first module that needs it.

This applies to routines such as:

- SVG normalization, tinting conventions, and generated SVG primitives;
- theme token path catalogs and color variant resolution;
- palette color/alpha serialization;
- JSON path read/write helpers;
- numeric parsing and slider stepping rules;
- device metric normalization;
- import mapping that is provider-independent.

Per-editor, per-module, per-bridge, and per-renderer classes may orchestrate these routines, but they must not own the generic algorithm.

If a feature needs reusable behavior and the common routine does not exist yet, create or extend the common routine first. Do not add a local helper and plan to clean it later unless it is marked with `TODO(editor-architecture)` and removed in the same phase.

## 7. Inheritance is recursive and uniform

An inheritable field has either:

- an explicit value;
- an inherited value.

Resolution walks the parent chain until it finds a concrete value.

This rule should be generic. It should not be reimplemented per editor, per module, or per component.

Resource systems are related but not ordinary parents:

- palette;
- fonts;
- icon themes;
- assets.

They resolve resources, but they are not normal inheritance parents.

## 8. Components are reusable classes, not copied property bags

Component classes define reusable visual/behavior defaults.

Examples:

- avatar;
- text input bar;
- keyboard;
- button icon;
- label;
- audio;
- video;
- surface style.

Modules use component classes by reference and may expose controlled overrides.

If many modules need the same cluster of fields, that cluster should become:

- a component class;
- a value kind such as `surfaceStyle`;
- or a `componentOverride` control.

It should not be copied into each module editor.

## 9. Component overrides are field-level and explicit

A component override editor receives:

- the component/class being overridden;
- the allowed fields;
- each field's dictionary definition;
- inherited/default values from the component.

It returns only changed fields.

The resolved model combines:

```text
component defaults
  + module/app overrides
  + instance overrides where allowed
```

The UI must show clearly whether a field is default/inherited or overridden.

## 10. Editors organize; controls edit

An editor is responsible for:

## 18. Deferred implementation notes

These are agreed improvements to revisit after the current migration pass. They should not be solved with editor-specific shortcuts.

- Extract the current `DictionaryFieldControl` switch into a dedicated dictionary control registry/factory. `DictionaryFieldControl` should become mostly a shell/row host; each `ValueKind` should resolve to a reusable control class.
- Promote numeric controls beyond plain text boxes. `number.integer` and `number.decimal` should have numeric validation, formatting, and step behavior owned by the dictionary layer.
- Add compound value kinds for paired values instead of solving pairs visually per editor:
  - `pair.xy`;
  - `pair.widthHeight`;
  - `pair.lightDarkColor`;
  - future logical pairs/triples where the value is conceptually edited as a unit.
- Layout JSON should be able to reference either individual fields or compound field groups. If a pair is conceptually one value, the JSON should declare it as one dictionary field/control, not as two unrelated controls forced onto one row by an editor.
- Centralize editor-shell UI tokens for common chrome still hardcoded in the spike:
  - selected row background;
  - amber/changed marker;
  - swatch border;
  - preview demo colors.
- Keep navigation rows, dialogs, and preview shell as common editor chrome. They may be custom, but must remain shared infrastructure, not per-editor styling.
- The palette “used” marker is already modeled in navigation. When theme/component tables land, implement reference scanning there instead of inventing table-specific markers.

- choosing the record/entity;
- grouping fields into cards/subcards;
- ordering groups;
- deciding what is shown or hidden;
- calling commands.

A dictionary control is responsible for:

- editing the value;
- validation feedback;
- restore affordance;
- picker/modal UI;
- local layout of compound values.

If these responsibilities blur, stop and refactor before continuing.

## 11. Cards and groups are structural, not data logic

Cards, subcards, accordions, and tabs are presentation structure.

They may be reusable shell controls, but they must not contain field-specific logic.

General ordering rule unless explicitly overridden:

- groups/cards sorted alphabetically;
- later, an explicit order field may override alphabetical sorting.

Cards supporting animation/overrides/status may show indicators, but those indicators should be derived from data, not manually toggled.

## 12. The preview consumes resolved/frame models

The preview should not know about editor forms.

The editor sends or persists data. A resolver builds:

- resolved model;
- frame model for a specific frame.

Preview/render consumes that output.

Separate clearly:

- editable data;
- resolved data;
- frame-specific data.

## 13. No compatibility fallbacks unless explicitly approved

We are not supporting arbitrary old databases during this spike.

If a field is missing because the current schema needs migration, migrate it.

Do not add silent fallback paths that hide schema problems.

This rule also applies to token vocabulary and persisted identifiers. Rename or
replace them through one explicit data migration: map every old value, update
all references, seeds and committed parity data, then remove the retired value.
Do not preserve aliases or compatibility coercions unless explicitly approved.

Do not add development-only fallbacks with plausible production values. Values
such as `12`, `#FFFFFF`, `#111827`, `0`, `"normal"` or a hardcoded token are not
acceptable inside runtime resolvers just to keep an incomplete payload rendering.
They make broken data look valid. Required current-model data must be created by
seed/migration/default normalization before runtime code consumes it.

When a defensive visual fallback is truly needed after resolved data is already
being rendered, it must be intentionally obvious, normally the protected
`debug_red` sentinel or an explicit unsupported placeholder.

Any fallback must be:

- explicit;
- temporary;
- documented;
- approved before implementation.

## 14. Destructive cleanup requires a commit first

Before any destructive cleanup or migration:

1. commit the current known-good state;
2. run the relevant validation;
3. perform the destructive step;
4. validate again.

This applies especially to:

- schema cleanup;
- deleting legacy fields;
- removing fallback code;
- renaming tokens;
- consolidating palette/theme/font/icon records.

## 15. Stop conditions

Stop and ask before continuing if:

- a dictionary field seems to need editor-specific styling;
- a value needs a new kind but the kind is unclear;
- a component override would require manual per-field UI;
- runtime rendering logic is about to be duplicated in the editor;
- a compatibility fallback feels tempting;
- a new class/control is being created for something that already exists conceptually.

The default answer should be: create or improve the shared class, not patch the local case.

## 16. Spike success criteria

The desktop shell spike is only useful if it proves:

- three-panel layout is simple and robust;
- dark/light palette is centralized;
- property editors can be generated from field definitions;
- dictionary controls can own their visuals;
- component override modal is simpler than the React/CSS version;
- preview can remain web-based;
- adding a new field kind does not require editing every editor.

If the spike fails these criteria, we should not migrate further.
