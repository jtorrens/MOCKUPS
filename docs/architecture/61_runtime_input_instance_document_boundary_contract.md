# Runtime Input Instance Document Boundary Contract

Status: normative.

This document governs persisted Screen Runtime Value, structured collection and
animation document mutations initiated by the Runtime Inputs editor. It extends
contracts 23, 29, 31, 36, 47, 52, 59 and 60 without changing payload or JSON
shape.

## 1. Objective

Editor intent, document delegation and persistence have explicit owners:

```text
dictionary/collection/animation editor intent
→ RuntimeInputInstanceDocumentStore
→ existing strict facade domain operation
→ ModuleInstanceRepository complete current document write
→ duration synchronization through the existing common timeline owner
```

The editor supplies stable ids and already prepared values/documents. The store
is a narrow delegation boundary. Existing facade coordinators preserve atomic
content/animation updates and the repository remains the SQL owner.

Every collection mutation resolves one exact declared storage key from the
effective Runtime contract before touching content. Its persisted value must
already be an array of object items with unique non-empty stable ids. Add,
insert and duplicate require a new explicit stable id; insert-after requires
the referenced stable id to exist. A missing, undeclared or wrong-root
collection is an error, never an instruction to create an empty collection or
append somewhere plausible.

Scalar and collection-field writes also resolve one exact current definition
by stored JSON key. Only `source: runtime` values may persist, and the supplied
JSON value must match that definition's canonical `ValueKind` shape. Test Value,
embedded binding and animation-keyframe authoring serialize through the same
owner; they do not keep separate boolean, number or object coercions.

## 2. Instance-store ownership

`RuntimeInputInstanceDocumentStore` may delegate only these explicit operations
for one stable Module Instance id:

- update one declared runtime scalar by its explicit JSON key;
- add one complete prepared structured collection item;
- insert one complete prepared item after an explicit stable item id;
- duplicate one complete prepared item with explicit stable target-id mappings;
- move one explicit stable item by a requested relative offset;
- delete one explicit stable item;
- update one field of one explicit stable collection item;
- update an explicit set of fields on one stable collection item as one atomic
  prepared document write when declared interaction metadata requires it;
- load the exact current animation document through
  `ModuleInstanceAnimationDocumentStore`;
- save one complete prepared animation v2 document through that same store.

It must not create ids; find a collection from a label or field type; parse
Runtime Input contracts; choose a Component Variant; manufacture Overrides;
infer target mappings; calculate duration or frame origins; create UI; execute
SQL; repair current data; or accept a partial animation patch.

Explicit reconciliation after a Module Variant change may create an empty
array for a newly declared collection. It must still reject a present
wrong-root collection and malformed or duplicate item ids; normal editor
mutations are not reconciliation and never create the collection root.

The store composes current facade/domain operations and contract 59. It is not a
repository and does not replace the coordinated content/animation operations
already owned outside persistence.

## 3. Editor ownership

`RuntimeInputsCollectionEditor` retains:

- generic dictionary controls and the generic commit route;
- the explicit Design Test Values versus Production Runtime Values distinction;
- declared storage collection keys;
- stable collection item and nested target id generation;
- item insertion, duplication, reorder and delete intent;
- explicit target-id mapping when duplicating nested structures;
- explicit complete Component Variant selection and Default at new boundaries;
- local Overrides and Runtime Input envelope construction;
- track activation/removal and complete animation v2 document preparation;
- confirmations, session state and presentation.

After this phase the editor may receive `SpikeDatabase` only as a
construction/composition parameter for its typed sources and stores. It must not
retain a database field or call database methods directly.

## 4. Atomic persistence and timeline effects

The existing facade/domain coordinator remains responsible for operations that
must update content and animation together, such as duplicating or deleting an
item with owned animation targets. The store passes explicit prepared items and
target mappings unchanged.

When a declared field transition changes more than one value in the same
collection item, the editor prepares the complete explicit field set and the
store delegates one atomic content write. It must not issue sequential writes
that expose an invalid current document between values.

The Module Instance repository owns SQL, current-root validation and synchronized
complete document writes. Common temporal services own effective duration. No
read, scalar update or collection UI action may persist absolute Shot frames,
derived origins or a calculated duration outside that established coordinator.

## 5. Preserved contracts

- Stored collection and animation ownership binds through stable ids, never
  indices.
- Stored collection items are objects with unique non-empty stable ids and the
  storage key comes from the effective Runtime contract.
- Persisted scalars and item fields are exact declared Runtime values with the
  JSON shape owned by their `ValueKind`.
- Full Component Variant references remain stored values; labels are display
  only.
- Forwarding and local Overrides remain explicit.
- Crossing a new Component boundary still selects an explicit Default Variant.
- Keyframes remain relative to their stable owner.
- Reorder and move alter effective projection without rewriting local
  keyframes.
- Design Test Values remain transient; instance Runtime Values remain persisted
  Production payload.
- Preview resolves the complete payload before the generic bridge and renderer.

## 6. Enforcement and tests

Architecture enforcement must verify:

- `RuntimeInputInstanceDocumentStore` owns the remaining database dependency of
  the Runtime Inputs editor and contains no SQL;
- animation access composes `ModuleInstanceAnimationDocumentStore`;
- the store contains no UI, dictionary, forwarding, Variant-selection or frame
  calculation logic;
- `RuntimeInputsCollectionEditor` retains no database field and performs no
  direct database call;
- every scalar, collection and animation write in that editor delegates through
  the typed store;
- this contract is linked from `AGENTS.md` and the architecture index.

A disposable-database test must prove animation reads are byte-for-byte
read-only, exercise explicit scalar, add, insert, duplicate, field update, move
and delete operations, verify stable item order/content, and round-trip one
complete current animation v2 document. It must also reject undeclared or
wrong-root collections, missing/duplicate ids and a missing insert anchor
without changing persistence. Undeclared keys and fields, nulls and wrong-shape
values must likewise fail without changing persistence.

## 7. Out of scope

This phase does not redesign Runtime Inputs or collections, change Add/Duplicate
behavior, alter Overrides or forwarding, fix keyframe dragging, change tables
or JSON, migrate data, add Render Mode/export or modify parity assets.

## 8. Forbidden shortcuts

- binding collection operations by row index without the stored stable id;
- deriving target mappings from labels, names, component types or positions;
- saving a partial animation track or absolute Shot frame;
- treating Test Values as persisted instance content;
- creating ids or choosing Variants inside the store;
- creating a missing collection root or appending after a missing stable id;
- accepting an undeclared Runtime key or coercing an invalid value to a
  plausible false, zero, string, object or array;
- adding duration formulas or timeline synchronization to the editor/store;
- bypassing owning repositories with local SQL.
