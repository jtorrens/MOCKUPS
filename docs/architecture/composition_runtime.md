# Composition and Runtime Inputs

Status: normative.

## Composition identity

Reusable composition references concrete Component Variants. Every boundary
uses:

```text
componentClassId::variant::variantId
```

The reference is complete and stable. A class id, short Variant id, label,
component type, sibling order and collection position are insufficient.

A boundary also owns its explicit local Overrides. Overrides customize that
one embedded use and do not mutate the referenced Variant.

## Runtime Inputs

Runtime Inputs are declared product inputs shared by isolated Design Preview
and Production instances. Their definition includes stable identity,
`ValueKind`, serialization, option source, forwarding rules and animation
metadata.

- Design Test Values are temporary samples for the selected definition.
- A Production Screen payload is persisted instance data.
- Saving Design Test Values as defaults explicitly updates the active
  definition.
- Editing a Production payload updates only the selected Screen.

Preview controls do not create fields or reinterpret their ownership.

## Explicit forwarding

Forwarding crosses a named boundary only through an explicit declaration. It
maps a source field to a target field or collection using stable ids and
preserves the target's canonical value shape.

When a new Component boundary is created:

1. the target class is explicit;
2. the protected Default Variant is selected;
3. forwarding is empty until authored;
4. local Overrides are an explicit object.

Forwarding is prepared before Preview registry dispatch. A resolver, registry,
bridge or renderer never infers forwarding from matching names or shapes.

## Embedded documents

At a recursive embedded boundary:

- the child `designPreviewJson` may become the local fixture;
- the complete `runtimeContractJson` temporal-owner envelope remains intact;
- the selected Variant reference and Overrides remain explicit;
- owner-relative animation identity is preserved.

Required documents are current objects and fail when absent, blank, malformed
or of the wrong root kind.

## Text Box and Icon Rows

Text Box Variants own exactly:

- the complete Left Icon Row slot;
- the complete Right Icon Row slot;
- the gap between icon rows and text;
- Text Box visual and runtime text values.

Icon Row Variants own:

- ordered items with stable ids;
- row gap;
- orientation;
- item sizing.

Each Icon Row item is a fixed Button boundary with a full Button Variant
reference and explicit local `buttonOverrides`. The editor shows Variant,
navigation to Button and the shared Overrides action. It does not show a
Component selector.

Icon Row structure is authored in the Variant. It is not a Runtime/Test Value.
An owning parent may explicitly project Runtime content into those stable item
ids, including icon, label, Button state, colors and Badge values. This never
adds, removes or reorders the Icon Row items.
Text Input Bar forwards only its explicit runtime text. Bubble and Text Input
Bar customize their selected Text Box slot through local Overrides.

## List Item

A List Item Variant is one complete item model. It owns:

- the item size;
- an ordered element model containing at most one Avatar, Label and Icon Row;
- each element's exact Component Variant slot, local Overrides, size and
  placement;
- Normal, Pressed and Inactive appearances.

Elements may be reordered or omitted by a Variant. Runtime never changes that
structure. Runtime owns a collection of stable content sets, the selected set
id and each set's state. Every content set supplies the Actor/avatar, primary
and secondary text, text color tokens and values for the stable Icon Row item
ids required by the active Variant.

The selected set and the selected set's state are separate animatable fields.
The state appearance contains exactly one Surface Variant slot and one
`elementsOpacity` multiplier between zero and one. The multiplier applies to
Avatar, Label and Icon Row as a group; it never changes the state Surface.

## Structural stacks

### Component Stack

A Component Stack owns ordered stable slots. Each slot references a concrete
Component Variant and local Overrides. Placement and sizing belong to the slot
or its declared component boundary, not to its index.

### Collection Stack

A Collection Stack repeats an explicitly declared item contract over a
structured Runtime Input collection. Collection items retain stable ids.
Reordering changes order, not identity.

### States

A stack State owns the exact active alternatives and their explicit visual
transition:

- **Replace** selects one complete alternative;
- **Overlay** composes complete alternatives in declared order;
- **Reflow** resolves layout movement from the owner state change.

State selection, entry and exit are contract data. Names and positions do not
select alternatives.

## System bars

Status Bar and Navigation Bar own their item composition in their component
contracts. Fixed Button children use the standard fixed-boundary presentation:
Variant, navigation and local Overrides without a Component selector.

## Keyboard pressed popup

Keyboard owns the complete pressed-key composition as one continuous
head/connector/key path with:

- one exterior shadow;
- one glyph;
- horizontal containment within the resolved keyboard frame;
- a connector whose top width matches the popup head;
- a connector base sharing the pressed key's top edge.

Edge handling and shape construction remain in the Keyboard owner. Common
Preview helpers and the renderer receive only resolved generic geometry and
style.
