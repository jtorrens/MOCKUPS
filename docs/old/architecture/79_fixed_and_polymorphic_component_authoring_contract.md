# Fixed and Polymorphic Component Authoring Contract

Status: normative.

This contract governs every dictionary field or structured item that creates or
edits a Component Variant boundary.

## 1. Two distinct authoring boundaries

A fixed boundary permits one exact Component Class. Its UI contains:

- one Variant selector limited to that class;
- the shared compact navigation action for the selected full Component Variant
  reference;
- the shared compact Overrides action when the boundary owns local Overrides.

It never shows a Component selector.

A polymorphic boundary explicitly declares `*` in its component type selector.
Its UI contains:

- an explicit Component selector;
- the Variant selector for the chosen class;
- the same navigation and local Override actions when applicable.

The editor must not determine whether a boundary is fixed or polymorphic from a
field name, label, hierarchy depth, card, collection position or the available
option count. Only the declared selector determines the presentation.

## 2. Exact references and Default creation

Every non-empty selection is one complete reference:

```text
componentClassId::variant::variantId
```

Creating a fixed boundary requires its option source to identify one exact
Component Class id and that class's protected stable `default` Variant. Zero
classes, multiple classes, missing group ids or a missing Default Variant fail
explicitly. No class or Variant is selected by label, name, order or position.

If future scaffolding introduces multiple Component Classes within a role that
is currently fixed, that scaffolding must first establish an exact class
binding for the owner. The editor must not preserve operation by silently
choosing one.

Creating a polymorphic boundary starts without a selected Component. The user
chooses the Component explicitly; that action crosses the new boundary and
selects the chosen class's exact Default Variant. The editor does not select
the first available Component.

Changing a Component or Variant clears that boundary's local Overrides. It does
not rewrite another boundary or infer replacement Runtime values.

## 3. Shared presentation

Variant selection, Variant navigation and local Overrides use the shared
dictionary Component Variant surfaces. A component-specific collection may own
stable item ids, reordering, duplication and deletion, but it must not recreate
the Component/Variant/Overrides interaction locally. A Component Variant Slot
must pass its local Override callback and highlight state into that shared row;
it must not append a textual `Overrides…` button or invent another action
layout.

`IconSlots` is the current fixed-collection example:

- every item is a Button boundary;
- `+` creates a real item immediately with a new stable item id;
- the item starts on the exact fixed Button class's Default Variant;
- the item stores its full `buttonVariantReference` and explicit
  `buttonOverrides`;
- its editor uses the shared Component Variant Slot surface;
- no provisional Component/Variant row exists before the item is created.

## 4. Data and Preview boundaries

This is an authoring rule. It does not weaken current persisted documents:

- stable ids remain stable;
- complete Variant references remain required;
- Overrides remain explicit local objects;
- Runtime forwarding remains explicit;
- resolvers receive complete resolved Component boundaries;
- the bridge and renderer remain generic.

The Preview panel may materialize the Default Variant for a new fixed boundary.
It must leave an unselected polymorphic boundary empty until the user chooses a
Component. It may not repair existing missing references.

## 5. Enforcement

Automated checks and desktop tests require:

- one shared rule for detecting an explicitly polymorphic selector;
- one shared rule for validating a fixed option boundary and locating its exact
  Default Variant;
- no `SelectComponentClass: true` special case inside `IconSlots`;
- no provisional first-Button selector inside `IconSlots`;
- shared Variant navigation and Overrides for each Icon Row Button;
- no first-option selection when creating a structured polymorphic Component
  item;
- failure when a fixed boundary has zero or multiple Component Classes or lacks
  its Default Variant.

Opening or validating the database remains byte-for-byte read-only.
