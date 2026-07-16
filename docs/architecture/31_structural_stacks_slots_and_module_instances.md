# Structural Stacks, Slots, States and Module Instances

Status: normative cross-component contract for reusable structural composition.

This document connects the component composition, module Variant, runtime-input
and animation contracts. The detailed visual behavior remains in the Component
Stack, Collection Stack, Notifications and Lock Screen behavior sheets.

## 1. Vocabulary and identity boundaries

Several different structures use the word `slot`. They are not interchangeable.

| Term | Stable owner | Cardinality | Purpose |
| --- | --- | --- | --- |
| embedded component slot | parent component or module Variant | one selected child Variant | recursively compose a known child role such as Label, Surface or Status Bar |
| Component Stack slot | Component Stack `items` runtime collection | one ordered State collection | reserve one stable position whose visible component may change |
| Component Stack State | one Component Stack slot | one allowed component Variant or explicit None | define a selectable/animatable visual state for that slot |
| Collection Stack item | Collection Stack runtime collection | variable ordered items | lay out a changing list of independent child instances |
| Shot module slot | Shot | one Module Instance | place a module runtime unit on the Shot timeline |

The persisted spelling `presetId` remains the internal field name for a concrete
component Variant reference. UI and architecture language use **Variant**.

## 2. Choosing the structural primitive

### Component Stack

Use Component Stack when the parent knows a fixed set of semantic positions but
each position may display different allowed components over time. Examples are
a Lock Screen content position that begins as Clock and later becomes Password,
or a position that may add a notification overlay without replacing its base.

```text
Component Stack
  -> ordered slots
       -> ordered allowed States
            -> concrete component Variant or explicit None
```

The slot, not the selected child, owns its position in the Stack flow. Changing
State therefore changes the content of one stable layout participant.

### Collection Stack

Use Collection Stack when the number and order of independent items is runtime
data. Examples are notifications, cards or overlays. It has no per-position
State collection:

```text
Collection Stack
  -> ordered runtime items
       -> one concrete component Variant and item Runtime Inputs
```

An item may expose its own runtime state, such as Notification Summary/Detail,
but that remains the selected child component's contract. It does not turn the
Collection Stack item into a Component Stack slot.

### Domain wrapper

A domain component may own either structural atom. Notifications, for example,
owns a Collection Stack but exposes Notification items rather than exposing the
generic child selector. The wrapper supplies one common Notification Variant,
maps item runtime data into that Variant and retains generic collection layout.
The structural atom stays domain-agnostic.

## 3. Component Stack contract

Component Stack has one protected `Default` Variant. Its public composition is
the Stack Runtime Inputs contract: sizing, boundaries and the ordered `items`
slot collection. A containing component or module normally binds that contract
as its own Variant data, so a new Stack Variant is not required for every
composition.

### Slot fields

Every slot has a stable id and owns:

- its ordered `alternatives` State collection;
- the Fixed/Reflow gap before this slot;
- a `theme.spacing.*` token for Fixed gap;
- a positive weight for Reflow gap.

Start and End gaps belong to the Stack container. The first slot has no
predecessor, so its gap-before fields do not participate. Slot order is layout
order and can be changed with the shared collection controls.

### State fields

Every State has a stable id and owns:

- a full `componentClassId::preset::presetId` reference or explicit None;
- local child Overrides;
- bound Runtime Inputs for that selected child Variant;
- one Placement relative to the frame assigned to the slot;
- Enter Motion and Exit Motion;
- for States after State 1, `active` and Replace/Overlay behavior.

State 1 is structurally special only because collection order declares it the
default. It is always active and Replace; it has no editable Active or Behavior
field. Reordering the State collection changes which State is the default.

State Placement belongs to the State, not the slot. Clock and Password may
therefore use different placements while occupying the same semantic slot.
Placement is resolved after the common Stack flow assigns the slot frame.

### Replace, Overlay and None

Active States are evaluated in collection order:

- `Replace` clears the current visible set and becomes its new base;
- `Overlay` appends above the current visible set without removing it;
- explicit None has no child renderable, but with Replace it clears the slot.

Overlay is not an exclusive radio choice. Several overlay States may be active
and collection order remains paint order. This supports a persistent base plus
independent visual additions without adding switches or renderer exceptions.

### Slots are independent

State selection is local to each slot. There is no global State number shared
by the Stack. Slot A may have one State while Slot B has three. Selecting State
3 in Slot B does not select or require State 3 in Slot A. Slot A's current State
continues to occupy flow space and therefore still pushes Slot B.

```text
resolve visible States in each slot
  -> measure each occupied slot
  -> assign all slot frames in order
  -> place every visible State inside its assigned slot frame
```

Outgoing States remain measurable until their finite Exit Motion completes.
This prevents later slots from jumping before the requested transition frame.

## 4. Collection Stack contract

Collection Stack also has one protected `Default` Variant and receives its
complete structure through Runtime Inputs. It provides two distributions:

- `Flow`: ordered vertical layout, Fixed/Reflow gaps and Fill/Content sizing;
- `Stacked`: one layered region with direction, offset, uniform/intrinsic item
  sizing and exponential scale/opacity depth ratios.

Stacked always resolves to content size. Fill has no useful meaning when all
children share one layered region. Flow retains the authored Fill/Content value,
and switching distributions never destroys the other mode's fields.

Items own stable ids, child Variant, Overrides, child Runtime Inputs, alignment,
gap-before, `Present` and Presence Motion. `Present=false` first resolves the
reversed Presence Motion; removal from layout happens only at its finite end.
Surviving stable-id renderables then move through Theme Reflow.

Collection Stack does not own Summary/Detail, notification counts or read state.
Those belong to the child or a domain wrapper such as Notifications.

## 5. Parent boundary: Variant by default, Forward by decision

A child Runtime Input does not automatically remain Runtime when the child is
embedded in a new parent. At every new component or module boundary it becomes
parent Variant data by default. The parent designer may explicitly activate
Forward only for child inputs that the child already declares Runtime.

```text
child Runtime Input
  -> parent Variant value by default
  -> optional Forward
  -> parent Runtime Input with a stable forwarded id
```

The forwarded field keeps its declared type, runtime label and nested stable-id
path. Structured collections remain arrays and Actor references remain record
references. No name-based or type-based ambient binding occurs. A Lock Screen
Actor supplies wallpaper context; it does not silently replace an Actor stored
inside Audio, Avatar or Notification items.

When Forward is enabled, the Variant-owned source field becomes read-only and
the effective value comes from the parent runtime contract. At a later parent
boundary that forwarded input is again Variant by default and may be forwarded
again. Removing a used forwarding link requires confirmation/usage handling;
it may never silently delete downstream instance values or animation tracks.

## 6. Module class and Module Variant

A module class owns identity, schema, shared Runtime Input declarations,
resolver identity and renderable behavior. It is not the reusable visual
configuration selected by a Shot.

Every module has a protected `Default` Variant and may have user Variants. A
Module Variant is a complete config snapshot. For Lock Screen it decides, among
other values:

- concrete Status Bar and Navigation Bar Variants;
- the concrete Component Stack Variant;
- Stack sizing and boundary values bound at the module boundary;
- the complete ordered slot/State composition;
- child Variants, Overrides, Placements and Motions;
- which eligible nested Runtime Inputs are forwarded to the module runtime.

The tree follows the same class/Variant navigation model as components. Opening
a module class selects `Default` on first visit and the last selected Variant
later in the same session. Only `Default` is protected by the system. A used
Variant cannot be deleted; ordinary Variants can be renamed, locked, duplicated
or deleted subject to usage.

Module Variant references use the explicit form:

```text
moduleId::variant::variantId
```

Actor, Device, Theme or appearance mode never infer a Module Variant. Different
platform compositions are explicit Variants selected by the Module Instance.

## 7. Module Instance inside a Shot

The production hierarchy is:

```text
Project -> Episode -> Shot -> ordered Module Instance slots
```

Each Module Instance stores:

- its module id;
- one full Module Variant reference;
- `content_json` containing only public Runtime Inputs in the selected effective
  contract;
- v2 parameter-animation tracks;
- duration/transition data owned by the Shot timeline.

The Module editor's Runtime Test Values and a Shot Module Instance consume the
same effective runtime contract. Their ownership differs: Test Values are
session-only samples and Reset discards them, while `content_json` is the real,
persistent payload for that concrete instance. A field visible in module Test
Values must therefore be visible at the same nested level in the instance; the
instance must not receive a second, module-specific editor schema.

The effective instance contract is formed before editing or preview:

```text
module shared Runtime declarations
  + selected Module Variant config
  + recursively resolved Forward declarations
  = effective Module Instance Runtime contract
```

The Module Instance never receives the complete Stack design again. It sees
the module's shared Runtime Inputs plus fields promoted by the selected Module
Variant. Consequently an iPhone Lock Screen and Android Lock Screen may have
different slot structures while their instances expose only the runtime values
appropriate to that explicit Variant.

For a projected Component Stack contract, `content_json` contains two related
minimal runtime collections:

- one entry per Variant slot with the stable slot id and State-selection action
  transport (`runtimeStateId`, source State, transition flag and elapsed time);
- one entry per stable State id with its parent slot id and only that State's
  explicitly forwarded child runtime contract/values and actions.

The second collection is not a copy of the State design. The complete
`alternatives` collection, component Variant references as editable choices,
Overrides, Placement and Motions remain exclusively in the Module Variant.
Generic parent metadata makes the editor render both arrays as
`Slots -> Slot -> State`, and the resolver joins them by stable ids only while
preparing the requested frame. State runtime values never appear in General.
Unknown or retired slot/State ids are migration errors; they are not resolved
through positional matching or fallback defaults.

Changing the selected Module Variant is a contract change. The instance retains
only Runtime values still declared by the new effective contract and removes
animation tracks whose field ids or stable target ids no longer exist. It never
falls back to module class config or guesses an equivalent State.

### 7.1 Production tree lifecycle

Adding a child to a Shot is an explicit three-part decision: Module, one of that
Module's Variants, and the instance name shown in the tree. The proposed name is
`Module · Variant` but remains editable before creation. The tree and the Shot
collection card delegate to one shared modal and one database operation, so
neither surface may invent a Default Variant or module-specific payload.

Rename, duplicate and delete operate on the Module Instance boundary. Duplicate
preserves the selected Variant, runtime values, actions/tracks, behaviour and
transition while assigning the next Shot order and a unique name. Delete is
confirmed and removes only that ordered instance. Both operations keep the Shot
duration synchronized with the remaining instance durations.

## 8. Stack State actions in Test Values and instances

Every Component Stack slot whose State collection reaches the effective parent
runtime contract exposes its own option action at that exact nested level. The
options are derived generically from the State collection's stable ids and
component Variant names. There is no Lock Screen-specific state control.

In module Test Values the shared action control shows:

- a compact State combo;
- the current State option visible but disabled as a destination;
- Play, which resolves one finite transition and holds its final frame;
- Restore, which returns to the captured starting State.

An action inside a collection item remains inside that item. It is never lifted
to the top Runtime Inputs card merely because it is animatable.

A persistent Module Instance does not show Play/Restore. At that boundary the
same declared State appears as a dictionary option field with the standard
animation activation control. Its options still come from the slot's State
collection. Action-only fields with animation metadata are visible as timeline
properties; action-only clocks without animation metadata are internal and are
never rendered as editable instance values.

The same ownership rule applies to forwarded child fields: Clock text belongs
to its Label State; Password text, attempt, timing and Enter Password action
belong to its Password State. General contains only runtime declarations owned
by the module root, such as the Lock Screen Actor and system-bar visibility.
The same nested layout is used for module Test Values and the persistent Screen
instance payload.

Design Test Values use transient `runtimeStateId`, source id, transition flag
and elapsed action time in prepared payloads. These are preview transport
values, not a second persisted Stack schema. Production animates the slot's
State property with ordinary v2 tracks. The track target is the stable slot id
and each keyframe value is a stable State id:

```json
{
  "fieldId": "runtimeStateId",
  "targetId": "stable-slot-id",
  "keyframes": [
    { "frame": 0, "value": "clock-state-id", "interpolation": "hold" },
    { "frame": 25, "value": "password-state-id", "interpolation": "hold" }
  ]
}
```

The stable slot id owns selection and the stable State ids are its values.
Collection position is never an animation identity, so reordering States or
slots does not rewrite keyframes.

Root and embedded component actions follow the same rule. Test Values renders
the generic Play/Restore control from the action contract. A Module Instance
does not store a simulated button press: it exposes the promoted play field as
an animatable property at its declared nested owner. The animation editor
authors a v2 track for that field, and the common owner timeline derives the
action's frame clock before the component resolver runs. The clock field itself
is hidden.

## 9. Requested-frame transition resolution

All Stack and child animation is resolved before preview. For a transition at
frame `F`:

1. the previous visible set identifies outgoing States;
2. the current visible set identifies entering States;
3. outgoing Exit Motion and incoming Enter Motion start at `F`;
4. container Reflow, when required, starts on the same action clock;
5. the action duration is the maximum finite duration, never their sum;
6. each requested frame receives final child state, placement, transform and
   opacity;
7. the bridge and renderer paint only those resolved generic nodes.

An entering child's owner-local frame is zero at activation, so its internal
animation starts relative to appearance. Re-entry restarts that local origin.
An outgoing child is held at its final internal state while Exit Motion is
resolved. Motion `Screen` bounds use the immutable device screen; `Parent`
bounds use the State's assigned slot frame, even through nested parent boxes.

There are no timers, CSS transitions or renderer-owned interpolation. Design
Play and Shot playback are two sources for the same deterministic requested-
frame contract.

## 10. Lock Screen example

One Lock Screen Module Variant may author:

```text
Content Stack
  Slot clock-authentication
    State 1: Label / Clock                 Replace, default
    State 2: Password / PIN                Replace
    State 3: Notifications / Closed stack  Overlay

  Slot lower-widget
    State 1: None                          default
    State 2: component Variant / Widget    Replace
```

The Module Variant decides this structure, all placements/motions and whether
Clock text, Password attempt, notification items or State actions are forwarded.
A Module Instance selects that Lock Screen Variant and stores the Lock Screen
shared Runtime Inputs plus those forwarded values. Activating Password affects
only the first slot. The second slot remains in its current State and its
occupied frame continues to participate in Stack flow.

The Lock Screen resolver supplies Actor wallpaper and the content frame between
visible system bars. Component Stack resolves its slots; Password and
Notifications resolve their own children. No rule for these decisions exists in
`MainWindow`, the preview bridge or the web renderer.

## 11. Validation and migration rules

- Every component and module reference is full and explicit.
- Every slot, State and collection item has a stable id.
- Unknown runtime State ids are errors.
- Missing Placement, Motion or required current-schema fields are errors.
- State deletion invalidates transient Test Values selection immediately.
- Variant changes prune invalid instance values/tracks explicitly.
- Retired shapes are migrated once in seed data and the committed SQLite file.
- Resolvers do not retain aliases, inferred defaults or compatibility fallbacks.

The generic structured collection control, Runtime Input forwarding pass, owner
timeline and renderable primitives implement these contracts. A structural
component or module may specialize its own composition, but it must not add
component-specific behavior to the editor shell, bridge or renderer.
