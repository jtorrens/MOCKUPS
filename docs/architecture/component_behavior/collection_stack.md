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
- runtime/animatable `Present` plus one generic Presence Motion.

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
- `stackOffsetToken`: tokenized displacement between successive layers;
- `itemSizingMode`: intrinsic child frames or one uniform frame measured from
  the largest child;
- `scaleRatio` and `opacityRatio`: Stacked-only depth multipliers in the `0..1`
  range (`scaleRatio` has a positive minimum).

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

Stacked children share one region. Item zero is the foreground item. Later
items accumulate depth and are painted behind it. Each item keeps its own
horizontal alignment.

With direction `down`, item zero starts at the Start boundary and every later
item is displaced downward by `stackOffsetToken`. With direction `up`, item
zero starts at the End boundary and later items are displaced upward. In
`content` mode the natural height is the largest child extent plus accumulated
offsets and boundaries. Stacked always resolves as `content`; the Sizing control
is disabled while Stacked is selected. A stale persisted `fill` value is not a
fallback and cannot alter the resolved Stacked frame—it becomes relevant again
only after Distribution returns to Flow.

`Largest item` first resolves every intrinsic child, chooses the maximum width
and height and assigns that frame to every item. A child that accepts an
assigned frame may fill it directly; otherwise the common collection layout
centers its intrinsic content inside that explicit frame. This is a generic
layout constraint and contains no Notification knowledge.

Depth ratios are exponential and use collection position: item zero resolves
to scale/opacity `1`, item one to `ratio`, item two to `ratio²`, and so on.
Scale is centered and visual only: it does not rewrite the uniform frame,
offset or natural Stack bounds. Opacity applies to the complete child group.

Flow-only per-item gap values remain part of the runtime item schema so
switching distribution does not destroy authored intent; Stacked layout ignores
them explicitly and uses only the shared stack offset.

## Temporal boundary

Collection Stack owns no wall clock or renderer state. Its resolver evaluates
`Present` against the stable item id and requested owner-local frame. A true to
false transition retains the outgoing child through the reversed Presence
Motion. The child leaves layout only at the exact finite exit completion.

That completion starts generic Reflow. Reflow measures the previous and next
resolved layouts and recursively interpolates the boxes of every surviving
stable-id renderable. This includes the item frame, its Surface and its owned
children; it is not approximated by uniformly scaling the final item. A change
to an embedded animatable runtime input, such as Notification `displayMode`,
starts the same layout interpolation directly from the field keyframe. The
changed item morphs between its resolved Summary and Detail geometry while its
siblings move to their next positions. The renderer receives only the resulting
per-frame boxes.

Reflow timing is Theme-owned through `theme.motion.reflowDurationMs` and
`theme.motion.reflowEasing`. The seeded duration is 240 ms, equivalent to six
frames at the project's 25 fps reference rate; the stored token remains a
physical reusable UI duration and is converted by the resolver for the active
Shot FPS. Easing uses the existing generic Motion vocabulary. Theme edits the
pair through one `Reflow` Motion Timing dictionary row containing Duration and
Easing only; this presentation does not add delay or intensity semantics.

## Preview boundary

The owning resolver validates every runtime value and concrete Variant
reference. Shared component-collection helpers resolve child presets, assign
optional uniform frames and measure/translate generic renderables. The registry only routes the new atom;
the common bridge, web renderer and `MainWindow` contain no Collection Stack
layout or component-specific rules.
