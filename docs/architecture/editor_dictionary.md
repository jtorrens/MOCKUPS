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

Structured collections have an owning collection editor. Scalar fields inside
each item still use dictionary definitions and controls.

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
compact actions in one row.

A fixed boundary exposes Variant, class navigation and Overrides, never a
Component selector. A polymorphic boundary exposes Component selection only
when its declared selector explicitly contains `*`.

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

## Shared editor organization

Reusable layout is declared in metadata with stable ids:

- `flatStack` presents repeated siblings with separators;
- `verticalCards` provides internal navigation and one child surface;
- `separatedSections` divides continuous content with labelled rules;
- per-group `presentation` allows an intentional mix;
- `pairLayout: sharedHeader` gives compound values one shared header.

The shell composes these presentations generically. Hierarchy depth, record
class, labels and position do not select a layout.

## Flat Variant Overrides view

Component and Module Variant editors expose `Editor` and `Overrides` as peer
views. The flat Overrides view is a projection of the current dictionary
semantics, not a comparison between Variant snapshots:

- it includes only fields whose prepared `FieldValue.HasLocalOverride` is true;
- it follows owner-declared embedded slots recursively and preserves the
  owning boundary path;
- it reuses the registered dictionary control and the same Restore commit;
- restoring a field removes it from the projection;
- an explicit control edit that returns the canonical value to the exact
  currently inherited value commits the inherited storage value, removes the
  local Override leaf and prunes empty objects inside that Override boundary;
- reads never perform that comparison: if a parent Variant later changes and
  its value happens to match an already stored Override, the Override remains
  intact until an explicit edit or Restore removes it;
- direct fields stored by the current Variant never appear, even when they
  differ from the protected Default Variant, class scaffold or seed;
- referenced child Variant data never appears as an Override of its parent.

An Icon Row item contributes only its typed local `buttonOverrides` boundary.
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
are not allowed.

## Forward presentation

Forward uses the shared compact, right-pointing indicator and its standard
active/inactive semantics. Editors do not create local Forward glyphs, sizes,
tooltips or highlighted states.
