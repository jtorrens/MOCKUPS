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

Icon Row structure is authored in the Variant. Runtime supplies one exact
Button Runtime value for every stable Variant item id, including content mode,
icon or label, Button state, colors, push values and Badge values. Runtime
never adds, removes or reorders Icon Row items. An owning parent receives and
forwards that same Icon Row Runtime contract; it does not declare a reduced or
renamed copy of Button fields.
Text Input Bar forwards only its explicit runtime text. Bubble and Text Input
Bar customize their selected Text Box slot through local Overrides.

## List Item

A List Item Variant is one complete item model. It owns:

- horizontal and vertical padding plus the gap between visible children;
- exactly one fixed Avatar, Label and Icon Row slot, each with visibility,
  order, exact Component Variant, local Overrides, sizing mode and vertical
  alignment;
- automatic or fixed Avatar sizing, fill or fixed Label sizing, and intrinsic
  or fixed Icon Row sizing;
- the number of Content Sets;
- Normal, Pressed and Inactive appearances.

List Item `width` and `height` are Runtime Inputs. The content box subtracts
the Variant padding. In automatic mode Avatar is a square whose side equals
the content-box height. Icon Row consumes its intrinsic width unless fixed,
and Label receives the remaining width unless fixed. Visible children follow
Variant order with one gap between adjacent children. A child that cannot fit
fails explicitly; clipping is not a layout fallback.

Runtime owns numbered Content Set rows, a positive numeric `activeSet` and the
current item state. Each row contains the exact Runtime contracts of Avatar,
Label and Icon Row. List Item never copies Actor, text, color, Button or Badge
fields into a parent-specific schema. The active set and current state are
separate animatable fields. Each embedded child Runtime keeps a stable target
id under its Content Set.

The Runtime collection declares `uiPresentation: itemSections`. This generic
editor metadata promotes each fixed Content Set to a section beside General;
it does not change the persisted collection envelope. Child collections appear
as compact rows in Variant order, and their shared `…` action reveals the
child's exact Runtime values directly. The editor does not add an intermediate
Content Sets section or nested child navigation.

The state appearance contains exactly one Surface Variant slot and one
`elementsOpacity` multiplier between zero and one. The multiplier applies to
Avatar, Label and Icon Row as a group; it never changes the state Surface.

## List

A List is a vertical Collection Stack whose repeated child boundary is one
exact List Item Variant. Its Variant owns the Collection Stack slot, List Item
slot, sizing, edge gaps, item alignment, inter-item gap policy and item
presence motion.

Runtime owns `itemWidth`, `itemHeight` and the ordered item collection. List
forwards the same two dimensions to every child. Each item has a stable id,
presence and one complete exact List Item Runtime contract. A parent of List
can therefore provide the shared width and height without bypassing List Item
ownership. List owns vertical flow, entry, exit and reflow. It does not
reinterpret Avatar, Label, Icon Row, List Item state or Content Set behavior.

The List Runtime editor identifies collection entries only by their ordinal
position: `Item 1`, `Item 2`, and so on. Runtime content is never reused as an
editor title. Selecting an item repeats the exact List Item Runtime sections:
General plus its numbered Content Sets and their Avatar, Label and Icon Row
boundaries. List-owned presence remains in General; the shared child `width`
and `height` are hidden there because `itemWidth` and `itemHeight` already own
those values in List General.

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
