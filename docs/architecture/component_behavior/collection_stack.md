# Collection Stack

Status: active structural atom. Collection Stack is a generic ordered runtime
collection whose children may flow vertically or share one layered region.

Source of truth: `src/desktop-preview/collectionStackComponentContract.ts`,
`collectionStackComponentResolver.ts`, `collectionStackComponentRenderable.ts`
and the shared component-collection helpers used by Component Stack.

## Purpose and boundary

Collection Stack is intended for variable groups such as notifications, cards
or overlays. It owns collection layout only. It does not know Notification,
Lock Screen, message semantics or the internal state of any child.

The class has one protected `Default` Variant. Its Variant config contains only
an empty `collectionStack` root. Distribution, sizing and every child are
Runtime Inputs so a containing component or module can supply the collection
without creating a Variant for each combination.

Collection Stack and Component Stack share the generic child contract:

- stable item id;
- full `componentClassId::preset::presetId` reference;
- item-local overrides;
- the selected child Variant's runtime inputs;
- Left, Center or Right alignment;
- fixed-token or weighted-reflow gap before the item.

The selector excludes Collection Stack itself to prevent recursive collection
trees. It may contain Component Stack; Component Stack may contain Collection
Stack. Those explicit manifest dependencies allow structural composition
without allowing either atom to recurse into itself.

## Runtime inputs

The scalar Runtime Inputs are:

- `distributionMode`: `flow` or `stacked`;
- `sizingMode`: `fill` or `content` while Distribution is Flow;
- `startGapToken` and `endGapToken`: tokenized container boundaries;
- `stackDirection`: `down` or `up` in Stacked mode;
- `stackOffsetToken`: tokenized displacement between successive layers.

The `items` collection uses the same generic collection editor, two-level
Component/Variant selector, standard View Variant and Overrides actions,
embedded child Runtime Inputs, session Test Values and forwarding behavior as
Component Stack.

## Flow distribution

Flow is identical to Component Stack layout. Children form one vertical flow,
collection order is visual order, the first item uses Start gap, the final
boundary uses End gap and every later item owns its relationship with its
predecessor. Weighted reflow consumes only positive remaining height in `fill`
mode; it collapses in `content` mode.

## Stacked distribution

Stacked children share one region. Collection order is also paint order: later
items are above earlier items. Each item keeps its own horizontal alignment.

With direction `down`, item zero starts at the Start boundary and every later
item is displaced downward by `stackOffsetToken`. With direction `up`, item
zero starts at the End boundary and later items are displaced upward. In
`content` mode the natural height is the largest child extent plus accumulated
offsets and boundaries. Stacked always resolves as `content`; the Sizing control
is disabled while Stacked is selected. A stale persisted `fill` value is not a
fallback and cannot alter the resolved Stacked frame—it becomes relevant again
only after Distribution returns to Flow.

Flow-only per-item gap values remain part of the runtime item schema so
switching distribution does not destroy authored intent; Stacked layout ignores
them explicitly and uses only the shared stack offset.

## Temporal boundary

Collection Stack owns no clock, timer or implicit presence state. This first
structural phase keeps stable item identities so a later generic presence
contract can give each item deterministic enter/exit phases and a local frame
origin. Until that contract is connected, adding, removing or reordering the
runtime collection changes the resolved frame directly; the renderer still
receives only final translated primitives.

Exit motion must not be inferred from a current boolean because the outgoing
item must remain renderable during its exit interval. The future presence
contract therefore has to resolve that interval before this component's
renderable boundary rather than add a timer or fallback here.

## Preview boundary

The owning resolver validates every runtime value and concrete Variant
reference. Shared component-collection helpers resolve child presets and
measure/translate generic renderables. The registry only routes the new atom;
the common bridge, web renderer and `MainWindow` contain no Collection Stack
layout or component-specific rules.
