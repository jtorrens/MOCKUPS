# Notification and Notifications

Status: structural components built on the generic component composition route.

Their relationship with Collection Stack, Component Stack States and Module
Instances is summarized in
[Structural Stacks, Slots, States and Module Instances](../31_structural_stacks_slots_and_module_instances.md).

## Notification

Notification is one reusable item. Its Variant owns only composition:

- concrete Surface Variant plus local overrides for the complete frame;
- concrete Avatar Variant plus local overrides;
- concrete Summary Label and Detail Label Variants plus local overrides;
- `Fixed size` or `Content + padding` dimension mode;
- explicit fixed width/height and tokenized X/Y padding;
- tokenized gap between Avatar and Label;
- independent generic placement for Avatar and Label relative to the padded Surface frame.
- explicit Avatar Badge values: visibility, icon/text mode, content, diameter and palette colors.

Runtime Inputs own the notification data: `Max width %`, Actor, `displayMode`,
Summary text/subtext and Detail text/subtext. `displayMode` is the orthogonal `summary`/`detail`
state; it does not encode item presence. The Actor is
resolved through the ordinary record-reference path and passed explicitly to
Avatar. The selected concrete Label receives final literal text/subtext values. Notification owns the
horizontal relationship. In Content mode `Max width %` is the explicit maximum
frame width relative to the resolved screen width. Its seeded value is `90` and
it shares the common percentage-to-design-width conversion used by Bubble.
Notification subtracts its padding,
Avatar and Gap, then supplies the remaining width as a constraint to the embedded
Label. Label resolves deterministic wrapped lines with the shared production-font
measurement helpers before the renderable frame is emitted; the resulting line
count determines Notification height. The final Surface frame is measured as
Avatar + Gap + constrained Label plus padding and may remain narrower than the
available maximum. In Fixed mode the authored width/height owns
the frame. Each child placement is resolved against the padded Surface frame;
the gap is enforced as their minimum horizontal separation. An `insideEdge`
child remains constrained to the padded inner frame while the other child uses
its available movement to satisfy that gap; gap enforcement never pushes it
through the padding. Their placement therefore determines their order. Surface is painted behind the children and
Notification emits only generic child renderables.

The editor keeps the standard General card. Layout owns dimension, padding, gap
and Surface Variant. Avatar, Summary Label and Detail Label each have their own top-level card with
their concrete Variant and placement. The Avatar card also embeds the generic
child-input editor for Badge settings. Every value remains Variant at this
boundary by default and exposes the standard Forward triangle, so a parent can
promote only the values it needs to Runtime. All embedded Variants use the standard
view and Overrides actions. When editing a parent Variant, the override route
retains that Variant as the data owner while loading the concrete embedded
component layout.

## Notifications

Notifications owns one internal Collection Stack, one concrete base Notification
Variant and one Badge Variant. The Collection Stack is an implementation detail:
it is not exposed as a selectable child in the Notifications editor. The
Notifications Variant owns its layout contract: tokenized boundaries and offsets,
uniform/intrinsic item sizing, depth scale and opacity ratios, initial distribution,
`Closed item limit`, Badge visibility and one generic Distribution Motion.

The editor retains the mandatory first-level `General` identity card and exposes
the Variant values in a separate first-level `Layout` card with vertical `Stack`,
`Notification`, `Badge` and `Motion` tabs. `Notification` selects
the common base Variant and its overrides, which every item shares. `Badge` owns
its Variant and visibility. Only the ordered item collection and Distribution
action remain public Runtime state.

Stacked shows `min(present items, limit)` while Flow shows
every present item. The limit changes only the closed visual stack: it never
truncates the runtime collection and the Badge count remains the complete
logical present count. `distributionMode` is the runtime state/action itself.
When it changes, the resolver composes the previous and next complete layouts
for the finite Distribution Motion; closing uses that same Motion reversed.

`showBadge` controls the Badge overlay. When enabled, Notifications derives its
text deterministically from the resolved collection length; its Variant keeps
an explicit diameter and palette colors. Badge placement is relative to the
Collection Stack frame and does not change its layout bounds. The Badge exists
only in Stacked state and follows the stacked side during a finite distribution
transition; Flow never retains it.

Each item stores a stable id, explicit Actor, Summary/Detail state and four text
values, plus a runtime/animatable `Present` boolean. Presence Motion, item
alignment and gap-before policy are common Notifications Variant values. An item
does not store a Notification Variant or local overrides: values not declared by
the item come from the common base Notification Variant. The Notifications
resolver validates the item vocabulary strictly, resolves each item against that
base and then supplies concrete children to the internal Collection Stack.

`Present=false` does not remove the child immediately. The resolver retains the
outgoing renderable for the reversed Presence Motion. Only after that finite
exit completes does the item leave layout; the surviving boxes then interpolate
through generic Reflow. A Summary/Detail change skips presence motion and starts
Reflow directly from the previous resolved Notification geometry. Surface size,
Avatar/Label geometry and the remaining item positions are interpolated frame by
frame with stable renderable ids; label content selects the destination state at
the keyframe while geometry completes over the Theme Reflow duration.

The default contract uses Stacked + Fit content + Largest item. All Notification Surfaces therefore use the maximum
resolved item width and height while their internal content keeps its own
padding and placement. Item zero is the foreground notification; successive
items are painted behind it and receive exponential scale/opacity reduction.
Neither component owns a timer. Every phase is derived from the requested frame
and the owner-local v2 tracks before preview.
