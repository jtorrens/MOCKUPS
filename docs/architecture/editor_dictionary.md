# Editor dictionary and presentation

Status: normative.

## Editable field route

Every editable scalar value follows one route:

```text
editor layout metadata
→ FieldDefinition
→ ValueKind
→ DictionaryFieldControl or registered dictionary control
→ generic commit path
→ owning document or repository
```

Editors do not construct raw text, numeric, option, boolean, color, font, icon
or file controls for values that belong to the dictionary. When a new value
shape is needed, its `ValueKind`, validation, registered control, serialization
and commit behavior are defined first.

`ValueKindCommitContract` is the single owner of persistence timing. Free-form
single-line text, paths, colors and numeric entry remain local until Enter or
focus loss; multiline text commits on focus loss so Enter remains content.
Continuous controls publish transient values while moving and commit once when
the interaction ends. Discrete selectors commit immediately. Controls and
concrete editors never add a timer, debounce or field-specific trigger.

Structured collections have an owning collection editor. Scalar fields inside
each item still use dictionary definitions and controls. Their Add, Duplicate,
Move and Delete actions call the same typed collection mutation owner used by
Runtime/Test Value collections. A collection item factory creates the exact
declared item document before that mutation; controls do not append ids or
embedded documents independently.

Component and Module dictionary fields may both declare the same
`StructuredCollection` contract. A fixed collection sets
`canEditStructure: false` and an exact `fixedItemCount`; the common editor then
shows its stable items without Add, Duplicate, Move or Delete actions.

## Field identity and metadata

`FieldDefinition` is the canonical UI projection of an editable value. It
preserves:

- stable field id and exact JSON key/path;
- label, unit, editability and visibility;
- optional concise help text and an explicit scalar validation pattern;
- canonical `ValueKind`;
- numeric bounds and increment;
- explicit options or typed option source;
- record or Component selector contract;
- pair labels;
- Runtime Input source and animation metadata.

Pair labels travel unchanged through embedded input bindings into the final
field. They are never generated from an id, label, type, hierarchy or position.

Concise field help travels with the same definition and is presented by the
common dictionary field shell. Scalar Runtime Input and Component Variant
definitions may declare a value pattern and validation message; the common
scalar pattern contract validates defaults, authored Variant values, local
Overrides and Runtime values before persistence. An owning editor never adds
a local validator or explanatory control for that field.

Every Runtime Input declares a canonical `valueKind`. The registry is
exhaustive: an unknown or unregistered kind is an error.

## Field row layout

A standard dictionary field is one generic three-column row: a bounded
responsive label, a flexible value host with zero minimum width and a fixed
Restore action. The value host receives exactly the row width left after the
label, Restore action and column gaps; a registered control cannot enlarge the
row or move Restore outside its viewport.

Compound controls own only their internal presentation. Pair controls use the
width assigned by the value host, keep two peer groups when it fits and stack
those groups through the shared responsive policy when it does not. Block
values keep Label and Restore in their header and use the full following row
for their registered control.

Presenting or refreshing a registered control is silent. In particular, after
Restore switches a field to its inherited value, a control notification that
only mirrors that presented inherited value cannot create a new local
Override.

## Specialized values

### Component Variant

`ComponentVariant` stores one full Variant reference when the boundary has no
local Override document.

### Component Variant Slot

`ComponentVariantSlot` stores the complete current value:

```json
{
  "variantReference": "componentClassId::variant::variantId",
  "overrides": {}
}
```

Variant selection, navigation to the class and local Overrides use the shared
compact actions in one row. Every boundary that exposes Overrides also exposes
Restore immediately to its right while local Overrides exist. Without local
Overrides, the Overrides action is neutral white and Restore is absent. With
local Overrides, the boundary label and both actions are amber. Restore clears
the complete local Overrides document below that boundary, preserves the exact
Variant reference and any unrelated Runtime values, and leaves an empty object
only where the current document contract requires it. Empty objects alone do
not mark a boundary as overridden.

A fixed boundary exposes Variant, class navigation and Overrides, never a
Component selector. A polymorphic boundary exposes Component selection only
when its declared selector explicitly contains `*`.

A `RecordReference` may declaratively name the referenced record class, sparse
owner-local document and exact editable field set that support Overrides. The
registered reference control adds the same compact Overrides action and
Restore action, and navigates through `EditorWorkspaceCoordinator` into the central contextual
editor. The normal layout/card projection renders that referenced class with
its breadcrumb and session view memory. Overrides never opens a modal or a
parallel editor surface. Editors, shell and shared services do not add buttons,
construct controls or select behavior by concrete owner/reference field id.
Restore and commit continue through the normal inherited-field contract; one
focused generic reference-Override port fronts the persistence adapter that
owns the concrete sparse document.

A new fixed boundary resolves one exact class and its protected Default
Variant. Zero or multiple matches fail. A new polymorphic boundary remains
unselected until the user chooses a class, then crosses into that class's
protected Default Variant.

### Behavior timing

`BehaviorTiming` owns fixed and natural duration authoring. Fixed mode stores
frames. Natural mode stores a semantic pace token while the owner contract
supplies the unit source and base rate.

### Spacing and compound values

Padding and gaps use `theme.spacing.*` tokens. X/Y spacing uses a spacing-token
pair. `PaletteColorPair` owns its compact Light/Dark layout, header, ellipsis
and border treatment.

`VideoFilePath` is the registered Shot-reference path control. Its Browse
workflow accepts a supported video inside the Project root and persists only
the normalized Project-relative path.

## Shared editor organization

Reusable layout is declared in metadata with stable ids:

- `flatStack` presents repeated siblings with separators;
- `verticalCards` provides internal navigation and one child surface;
- `separatedSections` divides continuous content with labelled rules;
- per-group `presentation` allows an intentional mix;
- `pairLayout: sharedHeader` gives compound values one shared header.

The shell composes these presentations generically. Hierarchy depth, record
class, labels and position do not select a layout.

A layout field may declare `visibleWhenFieldId` together with one or more
`visibleWhenValues`. The generic editor presents that field only while the
prepared value of the referenced dictionary field matches one of those exact
values. Both properties are required together, the referenced field must be
part of the same prepared editor context, and hiding a field never changes or
removes its authored value. Concrete Components and Modules declare these
conditions only in layout metadata; visual editors contain no owner-specific
visibility rules.

## Flat Variant Overrides view

Component and Module Variant editors expose `Editor` and `Overrides` as peer
views. The flat Overrides view is a projection of the current dictionary
semantics, not a comparison between Variant snapshots:

- it includes only fields whose prepared `FieldValue.HasLocalOverride` is true;
- it follows owner-declared embedded slots recursively and preserves the
  owning boundary path;
- it reuses the registered dictionary control and the same Restore commit;
- restoring a field removes it from the projection;
- every explicit control edit remains authored, including a value equal to the
  currently inherited value; equality never changes ownership;
- only Restore removes a local Override: field Restore removes that leaf and
  prunes empty parent objects, while boundary Restore clears every descendant
  Override recursively;
- reads never perform that comparison: if a parent Variant later changes and
  its value happens to match an already stored Override, the Override remains
  intact until Restore removes it;
- direct fields stored by the current Variant never appear, even when they
  differ from the protected Default Variant, class scaffold or seed;
- referenced child Variant data never appears as an Override of its parent.

An Icon Row item contributes only its declared fixed Component boundary and
typed local `buttonOverrides` document. It uses the generic structured
collection control and the same lifecycle mutation as every other collection;
there is no Icon Row value kind, control or persistence path. The boundary
metadata names the Button Variant field, Overrides key and exact Button class.
The item's selected icon, label, state and Button Variant reference remain
direct Icon Row Variant data and are not projected.

## Session view state

Card expansion, internal selection and editor scroll are session-only. State is
keyed by the exact editor layout `recordClassId` and explicit stable card or
section ids.

Moving between records of the same class preserves the open card and scroll
position. Returning to an editor class restores its previous point in the
current session. A new application session starts with cards closed. Preview
history and Variant selection never overwrite this state, and it is not stored
in `data/window-state.json`.

## Shared input interaction

Desktop text inputs preserve native mouse, touch and keyboard behavior. The
shared behavior adapts primary Pen drag so Wacom selection follows the same
standard. A double click selects the complete value in numeric fields.

Editor-specific selection handlers and per-field input interaction variants
are not allowed. The same rule applies to commit timing: typing never opens a
persistence operation, slider movement never writes intermediate values, and
the registered type policy determines the confirmation boundary.

## Forward presentation

Forward uses the shared compact, right-pointing indicator and its standard
active/inactive semantics. Editors do not create local Forward glyphs, sizes,
tooltips or highlighted states.
