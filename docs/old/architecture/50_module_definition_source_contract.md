# Module Definition Source Contract

Status: normative.

This document governs the sources of truth for current Module definitions
after retiring the disconnected Conversation config/runtime-contract factory.
It extends contracts 33, 34, 35, 44 and 49 without changing persisted data.

## 1. Objective

Module definitions have one active current route:

```text
canonical Module manifest entry and owner implementation
+ committed current Module documents and complete Variants
+ editor layout and dictionary metadata
→ strict validation
→ repository/editor/payload consumers
```

Normal desktop code reads and edits those documents. It must not retain a
second hard-coded example capable of drifting from the current Module Variant,
Runtime Input, collection or animation contracts.

## 2. Retired runtime factory

The disconnected `DefaultConversationConfigJson`,
`DefaultConversationDesignPreviewJson` and their seed-only helpers are retired.
They were not called by startup, provisioning, Module creation or repository
writes. The committed database already owns the complete current Conversation
documents.

Those methods must not return as:

- a startup seed, repair path or missing-document fallback;
- a generic Add Module implementation;
- an alternate Runtime Input, collection or timing contract;
- test evidence for current Module behavior;
- the template for future Module scaffolding.

Architecture assertions that previously inspected the dormant factory now
inspect the committed Module documents read-only. Removing this factory is not
a data migration and must not change the database bytes.

## 3. Current ownership

- the Preview manifest owns each stable Module class id, label, resolver,
  renderable and declared embedded dependencies;
- each Module resolver/renderable owns its semantic composition and resolved
  frame behavior;
- `modules` owns current config, design-preview/runtime contract, metadata and
  complete Variant snapshots;
- `CurrentModuleConfigContract` routes each exact `record_class_id` to its
  semantic config owner; the definition and every complete Variant pass the
  same owner on reads and writes;
- `AppModuleRepository` owns strict persistence of prepared current documents;
- Runtime Input forwarding, complete Variant application and local Overrides
  remain explicit at their established boundaries;
- Module Instances own Production payload and a full Module Variant reference;
- animation metadata in the current owner contract defines duration,
  sequencing and target-relative behavior.

The editor, payload boundary, registry, bridge and renderer cannot manufacture
missing Module definition data.

## 4. Future development scaffolding

A future new Module is an explicit development/scaffolding delivery. It must
provide together:

- a stable Module class id and definition id;
- its complete manifest route and owner entrypoints;
- complete config, design-preview/runtime contract and metadata documents;
- an explicit protected Default Variant and any required full Component
  Variant references;
- explicit Runtime Inputs, forwarding and collection schemas;
- explicit duration policy and temporal-owner metadata;
- editor layout/dictionary definitions;
- an explicit provisioning or migration step for parity data;
- database, architecture, resolver, animation and desktop tests.

Scaffolding may use reviewed templates, but those templates belong to the
development workflow and are validated before promotion. They do not live as
dormant factories inside `SpikeDatabase` and do not run on startup.

No step may infer a Module, Variant, forwarding edge, owner or reference from a
name, type, order, position or hierarchy depth.

## 5. Preserved invariants

This cleanup does not change:

- Module ids, Variant ids or complete Variant references;
- persisted JSON documents or Module Instance payloads;
- forwarding, Overrides, collection ownership or timing metadata;
- Design/Production lifecycle permissions;
- payload, resolver, renderable, bridge or renderer behavior;
- current editor fields, navigation or Preview output.

## 6. Enforcement

Architecture enforcement must:

- reject the retired Module factory methods in active Data sources;
- validate current Module ids against the manifest and registry;
- inspect committed Module documents for Runtime Input and animation contracts;
- keep strict Variant-envelope, full-reference and JSON-root validation;
- require owner validation for the current definition config and every
  complete Module Variant config;
- require this document from `AGENTS.md` and the architecture index.

The full tests, desktop build, strict database validation and unchanged hash
are required before this phase closes.

## 7. Forbidden shortcuts

- moving the retired factory to another runtime class;
- using a committed example payload as an implicit generic Module schema;
- creating a partial Module and relying on a reader or repository fallback;
- letting registry, payload, bridge or renderer supply missing defaults;
- weakening timing or Runtime Input assertions when their old code sample is
  removed;
- implementing future scaffolding as an editor or startup side effect.
