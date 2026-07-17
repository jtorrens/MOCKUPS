# Runtime Input Owner Document Boundary Contract

Status: normative.

This document governs how the desktop Runtime Inputs editor loads its owning
Component Variant, Module, Module Variant or Screen documents and persists
isolated Design Test Values. It extends contracts 23, 31, 34, 36, 46, 47, 55,
58 and 59 without changing Runtime Input or collection documents.

## 1. Objective

Owner lookup, Runtime Input interpretation and editor presentation have
separate boundaries:

```text
validated current owner and concrete Component Variant documents
→ RuntimeInputOwnerDocumentStore
→ exact config, Preview/runtime envelope and explicit write target
→ RuntimeInputsCollectionEditor
→ metadata-driven fields, Test/Runtime Values, collections and Overrides
```

The store supplies exact current documents and delegates explicit isolated
Design Preview writes. The editor remains the owner of Runtime Input and
structured collection interpretation.

## 2. Supported owner routes

`RuntimeInputOwnerDocumentStore.Load` accepts only these explicit tree-node
kinds:

- `Module`: its complete current config and Design Preview document; isolated
  Test Value writes target that exact Module id;
- `ModuleVariant`: its complete selected Variant config and the owning Module's
  Design Preview document; writes target the explicit parent Module id;
- `ComponentPreset`: its complete selected Component Variant config and owning
  Component Class Design Preview document; writes target the explicit parent
  Component Class id;
- `ModuleInstance`: its effective selected Module Variant config and complete
  persisted runtime Preview envelope; it has no isolated Design Preview write
  target.

The route is determined only by the explicit node kind and stable ids. It must
not infer an owner from name, record label, component type, hierarchy depth,
array position or current selection elsewhere in the shell.

## 3. Store ownership

`RuntimeInputOwnerDocumentStore` may:

- load the exact documents declared above;
- delegate a complete isolated Design Preview JSON write to one explicit Module
  or Component Class id;
- load the effective Runtime Input envelope of one full concrete Component
  Variant reference;
- load the Project, component type, record class and complete config associated
  with that full Component Variant reference.

It must not parse Runtime Input definitions; choose Default; shorten a Variant
reference; apply forwarding; merge Overrides; modify a structured collection;
write Module Instance content or animation; create fields or controls; execute
SQL; repair current JSON; or turn a Screen runtime payload into Test Values.

The store composes current facade/domain operations. It is not a repository;
table SQL, row mapping, current-root validation and write synchronization remain
in their contract 46/47 owners.

## 4. Editor ownership

`RuntimeInputsCollectionEditor` retains:

- exhaustive declared Runtime Input and `ValueKind` interpretation;
- generic dictionary controls for scalar values;
- the distinction between transient Design Test Values and persisted Screen
  Runtime Values;
- structured collection item identity, ordering and presentation;
- explicit complete Component Variant selection for component items;
- local explicit Overrides and embedded navigation;
- explicit forwarding envelopes and action presentation;
- animation activation and stable target reconciliation through their declared
  animation document boundary.

Contract 61 owns the separate persisted Module Instance scalar, collection and
animation mutation slice. Together the two boundaries remove the editor's
general database handle while keeping owner lookup and instance writes
independent.

## 5. Preserved contracts

- Stable ids and full `componentClassId::preset::presetId` references remain
  authoritative.
- New Component boundaries still begin with an explicit Default Variant.
- Existing current references never fall back to Default.
- Forwarding and local Overrides remain explicit and independent.
- Design Test Values remain session-only until an explicit Save Defaults action.
- Module Instance Runtime Values remain persisted Production payload.
- Scalar fields continue through `FieldDefinition → ValueKind → dictionary
  control → generic commit`.
- Owner-relative animation and complete Preview resolution remain unchanged.

## 6. Enforcement and tests

Architecture enforcement must verify:

- `RuntimeInputOwnerDocumentStore` owns the declared owner/preset database reads
  and contains no SQL;
- the Runtime Inputs editor creates one store per editor service;
- owner resolution delegates to the store and no longer calls the retired
  owner/preset facade methods directly;
- Component preset config lookup continues through
  `ComponentPreviewInputDataSource` rather than being duplicated;
- Module Instance isolated Design Preview writes fail explicitly;
- this contract is linked from `AGENTS.md` and the architecture index.

A disposable-database test must compare every supported owner route and concrete
Component Variant source with exact current facade values, prove reads are
byte-for-byte immutable, round-trip explicit Module and Component Class Design
Preview writes, and reject a Design Preview write for a Module Instance.

## 7. Out of scope

This phase does not move Module Instance scalar or collection mutations, change
Save Defaults, redesign Runtime Inputs, alter collections or Overrides, fix
keyframe dragging, change tables/JSON, migrate data, add Render Mode/export or
modify parity assets.

## 8. Forbidden shortcuts

- selecting an owner or Variant from its display name, type or position;
- persisting Design Test Values as Screen runtime content;
- allowing a Module Instance to write an isolated Design Preview document;
- accepting short Component preset ids or incomplete Variant config;
- returning `{}` for a missing or malformed current document;
- moving forwarding, collection or animation semantics into the store;
- bypassing owning repositories with local SQL.
