# Component Stack

Status: active structural atom. Component Stack is a generic vertical container
whose complete composition is supplied through Runtime Inputs.

Source of truth: `src/desktop-preview/componentStackComponentContract.ts`,
`componentStackComponentResolver.ts`, `componentStackComponentRenderable.ts`
and the generic Runtime Inputs collection editor in the desktop shell.

## Purpose and ownership

Component Stack places an ordered collection of concrete component Variants in
one vertical flow. It owns only the relationships between those children:

- collection order;
- horizontal alignment inside the Stack;
- the gap before each child;
- the leading and trailing container gaps;
- fill-container or fit-content sizing.

It does not copy the child component schema and it does not require a different
Stack Variant for each combination. The containing module/component supplies
the collection and may govern every child Variant, override and runtime input
through the same public runtime contract.

The Stack class therefore needs only its protected `Default` Variant. That
Variant has no compositional properties; the class configuration contains only
the empty `componentStack` contract root.

## Runtime input contract

The scalar Runtime Inputs are:

- `sizingMode`: `fill` or `content`;
- `startGapToken`: `theme.spacing.*` token between the container start and the
  first child;
- `endGapToken`: `theme.spacing.*` token between the final child and the
  container end.

The ordered `items` collection is also a Runtime Input. Every item has a stable
id and contains:

- `presetId`: full concrete Variant reference in
  `componentClassId::preset::presetId` form;
- `overrides`: explicit field-level overrides local to this Stack item;
- `inputs`: the selected child Variant's runtime values;
- `alignment`: stored as `start`, `center` or `end` and presented in the UI as
  Left, Center or Right;
- `gapBeforeMode`: `fixed` or `reflow`;
- `gapBeforeToken`: fixed `theme.spacing.*` gap;
- `gapBeforeWeight`: positive relative weight used by reflow.

Component selection uses the shared two-level `ComponentPreset` dictionary
control: first Component, then one of that Component's Variants. Changing the
Component refreshes the available Variants and child runtime contract. The
Stack itself is excluded as a child choice so the current contract cannot form
recursive Stack trees.

Child overrides open the normal embedded-Variant editor. There is no Stack-
specific override editor or breadcrumb model. Override state is explicit stored
state: restoring a field removes that override entry.

## Ordering and item editing

Collection order is visual order. Add, duplicate, delete and reorder use the
shared Runtime Inputs collection machinery. A newly added item becomes the only
expanded item and is revealed immediately.

Runtime Test Values are session-only inspection data. They are initialized from
the declared defaults only the first time that contract is opened in an
application session. Switching to another editor and returning must preserve
the current session values and collection. A module or parent component later
provides production values through this exact contract; preview-only Stack
inputs are forbidden.

## Gap semantics

Container boundaries and item relationships are deliberately separate:

- `startGapToken` owns the space before the first item;
- `endGapToken` owns the space after the final item;
- every item from the second onward owns the gap before itself, relative to its
  predecessor;
- the first item's gap-before controls are disabled and ignored.

For a fixed gap, the renderable resolves `gapBeforeToken` to final pixels. For a
reflow gap, `gapBeforeWeight` participates in deterministic proportional
distribution of the remaining vertical space. Fixed child heights, Start/End
gaps and fixed inter-item gaps are removed first; only positive remaining space
is distributed.

Reflow has an effect only in `fill` mode. In `content` mode there is no surplus
container height, so reflow gaps collapse to zero. The Fixed gap field is
disabled when mode is Reflow, and Reflow weight is disabled when mode is Fixed.

## Sizing and placement

`fill` uses the complete box supplied by the parent. It is the mode for layouts
where weighted reflow should consume available height.

`content` computes the natural width as the widest child and the natural height
as child heights plus Start/End and fixed gaps. The resulting frame hugs its
contents; its parent remains responsible for final placement. The isolated
Design Preview centers that natural frame only as preview presentation.

Each child is aligned independently across the Stack width. Left aligns its
left edge, Center centers it, and Right aligns its right edge. Alignment does
not change child size.

## Resolver and preview boundary

The Stack resolver validates all runtime values, resolves each full Variant
reference to its owning component type and merges that Variant config with the
item-local overrides. Missing references, item ids, child inputs or unsupported
enum values are errors; there are no compatibility fallbacks.

The Stack renderable invokes the normal component registry for each child,
requires the child to return a resolved box, calculates deterministic placement
and emits one generic `group` containing translated generic child primitives.
The common bridge and web renderer receive only final boxes and paint data.
They contain no Component Stack branch, child-component rule, inheritance rule
or runtime collection logic.

Component Stack owns no clock or animation timeline. If a child is animated,
its resolver receives the already requested frame state through its normal
runtime input contract; Stack only places the resulting resolved frame.

## Explicit migration rule

The retired Variant-owned `order`/`slots` model and item-owned `gapAfter` fields
are migrated once into Runtime Inputs. The old gap after item N becomes the gap
before item N+1. Retired fields are removed from every Stack Variant and from
the committed desktop database; runtime compatibility aliases are not kept.
