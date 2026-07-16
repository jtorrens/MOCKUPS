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

State identity and selection are local to their slot. There is no global State
index and no implicit substitution when another slot does not contain the same
number of States. Changing one slot leaves every other slot's selected State
unchanged; those unchanged slots remain measured participants in the same flow.

The Stack owns slot order, the gap before each slot,
leading/trailing container gaps, fill/content sizing and state selection inside
each slot. It does not copy child schemas. The class therefore has only its
protected `Default` Variant; complete composition is supplied through Runtime
Inputs by a containing component or module.

## Runtime contract

Scalar inputs are `sizingMode`, `startGapToken` and `endGapToken`. The `items`
Runtime collection contains stable slots. Each slot contains:

- `alternatives`: ordered nested state collection;
- `gapBeforeMode`: Fixed or Reflow;
- `gapBeforeToken`: fixed `theme.spacing.*` token;
- `gapBeforeWeight`: positive proportional Reflow weight.

Each state has a stable id and contains:

- a full `componentClassId::preset::presetId` reference, or explicit None;
- local Overrides and the selected child Variant's Runtime Inputs;
- `active`: animatable boolean for states after the first;
- `behavior`: Replace or Overlay for states after the first;
- generic `placement` relative to the frame assigned to that slot;
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
depth. The shared collection footer always exposes Add, including when items
already exist, and reveals the newly created item. A new slot starts with one
explicit empty default state rather than an invalid empty state collection.

Changing or deleting a component uses the shared forwarded-input confirmation.
Nested animatable fields use their own stable item id as v2 `targetId`. Test
Values never show animation/Forward affordances that only make sense at an
authoring boundary. When a State is removed, the generic Test Values session
invalidates any option action that targeted its retired id and selects the first
remaining State before producing the next payload; the resolver keeps rejecting
unknown state ids.

## Layout

Start and End gaps belong to the container. Every slot after the first owns its
gap relative to its predecessor; the first slot's gap controls are ignored.
Fixed gaps resolve a spacing token. Reflow weights divide only positive
remaining height in Fill mode and collapse in Content mode.

Fill uses the complete parent box. Content computes the widest visible slot and
the sum of visible slot heights plus boundaries/fixed gaps, leaving final
placement to the parent. Every visible state is placed independently inside the
frame assigned to its slot using the generic Center/Inside edge/Outside edge
contract. Visible Overlay states share that slot region and collection order is
paint order.

Layout is resolved in two passes. First, each slot measures the union of all
States that are still visible for the requested frame, including an outgoing
State during its finite Exit Motion. The common vertical flow then assigns one
non-overlapping frame to every slot in order. A child that consumes its parent
frame, such as Password, receives that exact assigned frame on the second pass;
intrinsic children are placed inside it by the State Placement. In Fill mode an
assigned frame is limited to the remaining container height, so a preceding
slot always pushes later slots instead of allowing a full-frame child to paint
back over it.

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

The Test Values State action follows the same frame contract without depending
on an advancing module frame: every precomputed preview payload carries its
explicit elapsed action time into the incoming and outgoing State contracts.
Exit, entry and container Reflow start together. The shared action duration is
the maximum of the selected outgoing Exit Motion, incoming Enter Motion and
`theme.motion.reflowDurationMs`; their durations are never added serially.

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
The retired slot-level Left/Center/Right alignment is migrated explicitly to an
equivalent placement on every state and then removed from the active contract.
No aliases, coercions or compatibility fallbacks remain.
