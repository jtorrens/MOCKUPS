# Component Stack

Status: active structural atom with deterministic slot states.

Source of truth: `src/desktop-preview/componentStackComponentContract.ts`,
`componentStackComponentResolver.ts`, `componentStackComponentRenderable.ts`
and the recursive generic Structured Collection dictionary control.

## Purpose and ownership

Component Stack places ordered slots in one vertical flow. Every slot owns an
ordered collection of allowed states, so one stable position may show a default
clock, replace it with Password, overlay another component, or resolve to an
explicit empty state without changing the Stack schema.

The Stack owns slot order, horizontal alignment, the gap before each slot,
leading/trailing container gaps, fill/content sizing and state selection inside
each slot. It does not copy child schemas. The class therefore has only its
protected `Default` Variant; complete composition is supplied through Runtime
Inputs by a containing component or module.

## Runtime contract

Scalar inputs are `sizingMode`, `startGapToken` and `endGapToken`. The `items`
Runtime collection contains stable slots. Each slot contains:

- `alternatives`: ordered nested state collection;
- `alignment`: Left, Center or Right (`start`, `center`, `end` in storage);
- `gapBeforeMode`: Fixed or Reflow;
- `gapBeforeToken`: fixed `theme.spacing.*` token;
- `gapBeforeWeight`: positive proportional Reflow weight.

Each state has a stable id and contains:

- a full `componentClassId::preset::presetId` reference, or explicit None;
- local Overrides and the selected child Variant's Runtime Inputs;
- `active`: animatable boolean for states after the first;
- `behavior`: Replace or Overlay for states after the first;
- generic `enterMotion` and `exitMotion` values.

State 1 is always the default. It has no editable Active or Behavior fields and
is treated as active/Replace. Later active states are evaluated in collection
order: Replace clears the current visible set; Overlay appends above it. None is
a valid state and can therefore clear a slot through Replace without a switch,
fallback or renderer exception.

## Generic nested collection editing

Both collection levels use the same recursive `StructuredCollection`
dictionary control. Nested collection support is part of the generic field
contract, not a Component Stack editor. It preserves standard Component then
Variant selection, View Variant, Overrides, child Inputs,
add/duplicate/delete/reorder, session expansion and reveal behavior at every
depth.

Changing or deleting a component uses the shared forwarded-input confirmation.
Nested animatable fields use their own stable item id as v2 `targetId`. Test
Values never show animation/Forward affordances that only make sense at an
authoring boundary.

## Layout

Start and End gaps belong to the container. Every slot after the first owns its
gap relative to its predecessor; the first slot's gap controls are ignored.
Fixed gaps resolve a spacing token. Reflow weights divide only positive
remaining height in Fill mode and collapse in Content mode.

Fill uses the complete parent box. Content computes the widest visible slot and
the sum of visible slot heights plus boundaries/fixed gaps, leaving final
placement to the parent. Visible Overlay states share the same slot region and
collection order is paint order.

## Frame and transition contract

Active persists only as v2 hold keyframes addressed by `fieldId` and the stable
state `targetId`. For an arbitrary requested frame the resolver evaluates all
state tracks and applies ordered Replace/Overlay semantics. It compares the
resolved visible sets at track change frames so a displaced or deactivated
state remains explicitly resolved during its finite Exit Motion.

Entry gives the child local frame zero at its activation keyframe. Re-entry
restarts it. During exit the child is frozen at its final internal frame while
the generic Motion helper resolves the outgoing transform. There are no timers,
CSS animations or inferred renderer state. The bridge and web renderer receive
only generic nodes for the requested frame.

## Preview boundary

The component resolver validates the slot/state contract, resolves concrete
Variants, merges local Overrides and resolves the visible set. The renderable
invokes the normal component registry, measures generic child nodes and emits a
generic group. Common collection helpers know only layout records. The bridge,
renderer and `MainWindow` contain no Component Stack or Lock Screen rule.

## Explicit migrations

The retired Variant-owned `order`/`slots` model and `gapAfter` fields were
migrated once into Runtime Inputs; old gap N became gap-before N+1. The later
flat runtime item shape is also migrated once: every old item becomes a slot and
its former Component/Variant/Overrides/Inputs become state 1. Lock Screen stack
bindings and the committed desktop database are migrated in the same change.
No aliases, coercions or compatibility fallbacks remain.
