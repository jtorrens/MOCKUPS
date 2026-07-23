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
- canonical `ValueKind`;
- numeric bounds and increment;
- explicit options or typed option source;
- record or Component selector contract;
- pair labels;
- Runtime Input source and animation metadata.

Pair labels travel unchanged through embedded input bindings into the final
field. They are never generated from an id, label, type, hierarchy or position.

Every Runtime Input declares a canonical `valueKind`. The registry is
exhaustive: an unknown or unregistered kind is an error.

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
