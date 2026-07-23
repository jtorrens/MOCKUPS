# Embedded Component Document Boundary Contract

Status: normative.

This document governs desktop reads and writes for embedded Component Variant
fields and Runtime local Overrides. It extends contracts 23, 34, 35, 46, 58,
60 and 63 without changing slot metadata, Variant identity, Override shape,
dictionary controls or Preview resolution.

## 1. Objective

Structural editor context and data mutation have separate owners:

```text
owner node + explicit slot path + optional Runtime Override source
→ EditorEmbeddedContext
→ EmbeddedComponentDocumentStore
→ current embedded field domain operation
→ complete owning Component Variant or explicit Runtime Override update
```

`EditorEmbeddedContext` identifies where the user is editing. It is a pure
structural value and must not receive or call persistence. The store is the one
desktop data boundary used by embedded breadcrumbs and field-value services.

## 2. Structural context ownership

`EditorEmbeddedContext` owns only:

- the explicit owning tree node;
- the ordered explicit `EmbeddedComponentSlotDefinition` path;
- the optional `RuntimeComponentOverrideSource` supplied by a concrete full
  Component Variant reference;
- current record-class and component-type projection from that explicit path;
- structural `Nested` and `Ancestor` operations.

The context must not query a database; resolve a Variant; read or write a field;
merge config; choose Default; persist an Override; create a control; or infer a
slot from a label, component name, hierarchy depth or array position.

## 3. Document-store ownership

`EmbeddedComponentDocumentStore` may:

- resolve the active Variant display name through the existing exact embedded
  Component domain operation;
- create one dictionary `FieldValue` through the existing explicit owner and
  slot path;
- commit one prepared dictionary value through that same explicit path;
- for Runtime context, update only the supplied local Overrides object and
  invoke its explicit changed callback;
- for Design Variant context, delegate the prepared value to the owning
  complete Component Variant document write.

It composes current facade/domain operations during the repository transition.
It is not a repository and must not execute SQL, parse or repair JSON, construct
slot paths, discover fields, create ids, select a Variant, apply forwarding,
resolve Preview state or create UI.

## 4. Consumers

`EditorHeaderController` asks the store for breadcrumb Variant names. A Runtime
root breadcrumb uses the explicit zero-slot ancestor of the same context; it
does not reconstruct the referenced Variant from a name or type.

`ComponentClassFieldValueService` delegates embedded-context field reads and
commits to the same store. Ordinary Component Class/Variant fields retain their
existing separate field service route.

Both consumers may receive `SpikeDatabase` only as a construction parameter
while composing the typed store. `EditorHeaderController` must not retain or
call the general database facade.

## 5. Preserved contracts

- Every embedded scalar still follows
  `FieldDefinition → ValueKind → dictionary control → generic commit`.
- Every embedded slot stores a full
  `componentClassId::variant::variantId` reference.
- Crossing a new boundary still chooses explicit Default; current data never
  falls back to Default.
- Inheritance reads the selected concrete Variant, not mutable class config.
- Local Overrides remain explicit leaves and survive coincidental equality.
- Restore removes the local Override rather than copying an inferred value.
- A missing slot/Overrides object may be created only by the explicit authoring
  action that crosses or edits that boundary; a present wrong-root slot or
  Overrides document fails without repair.
- A structured collection with explicit `componentItems` metadata stores three
  distinct members per stable item: a full Component Variant reference (or the
  explicit empty sentinel used by a visual-empty State), an Overrides object
  and an Inputs object. Existing items are validated by the shared document
  owner; presentation, Usage and Preview do not filter or manufacture them.
- Preview uses the same exact array-of-object boundary for Collection Stack
  items, Component Stack slots and their nested States. Every non-empty visual
  State retains object Inputs and Overrides; a wrong array entry or Overrides
  root never becomes an empty local override at the resolver.
- Runtime Test Values and Production payload ownership do not change.
- Forwarding remains explicit. Embedded Preview animation addresses only
  complete declared Runtime Input ids or an explicit forwarded id mapped to an
  existing local value; forwarding may promote a value without duplicating the
  child definition. A payload key with neither owner is not an animation
  identity. Preview resolves fully before the generic bridge and renderer.

## 6. Enforcement and tests

Architecture enforcement must verify:

- this contract is linked from `AGENTS.md` and the architecture index;
- `EditorEmbeddedContext` has no Data-layer or `SpikeDatabase` dependency and
  exposes no field read/write method;
- `EditorHeaderController` composes the typed store and retains no database
  field or direct database call;
- embedded-context operations in `ComponentClassFieldValueService` delegate to
  the store;
- the store contains no SQL, Avalonia, dialog, forwarding, Preview, resolver or
  renderer dependency.

A disposable-database test must compare active Variant name and inherited
embedded field reads with the exact existing domain operations, prove those
reads byte-for-byte immutable, exercise one explicit Design embedded commit,
and exercise Runtime local Override add/restore callbacks without writing that
transient Override to SQLite.

## 7. Out of scope

This phase does not redesign embedded navigation, breadcrumbs, selectors,
Overrides, forwarding, dictionary fields, Component Variants, structured
collections or Preview. It changes no tables, JSON, parity data, assets,
animation, Render Mode or export behavior.

## 8. Forbidden shortcuts

- passing `SpikeDatabase` into `EditorEmbeddedContext`;
- resolving a child Variant from a short id, label, name or position;
- manufacturing a slot path from hierarchy depth;
- writing Runtime Overrides into the Component Variant or Module Instance
  unless their explicit owning callback does so;
- treating an equal effective value as proof that no Override exists;
- moving embedded field semantics into persistence, bridge or renderer code.
