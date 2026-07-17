# Component Definition Source Contract

Status: normative.

This document governs the sources of truth for current Component definitions
after retiring the disconnected desktop runtime seed/default catalog. It
extends contracts 33, 34, 35 and 46 without changing the persisted model.

## 1. Objective

A current Component definition is supplied by explicit, complete authorities:

```text
canonical Preview manifest and owner implementation
+ committed current Component Class row and complete Variants
+ editor layout and dictionary metadata
→ strict validation
→ repository/editor/payload consumers
```

The ordinary desktop runtime is a consumer and editor of those current
definitions. It is not a second generator of plausible defaults.

## 2. Retired runtime catalog

`SpikeDatabase.ComponentClassDefaults.cs`, `ComponentSeedRow` and their
component config/design-preview factories are retired. They were disconnected
from provisioning and normal startup, duplicated documents already stored in
the committed database, and could drift independently from the manifest,
resolver contracts and current Variants.

They must not return as:

- startup seeds or repair input;
- fallback Component config or design-preview documents;
- a hidden Add Component path;
- a source used by tests instead of current committed data;
- a reusable catalog for future scaffolding.

Removing the catalog is not a data migration. The committed database already
contains the complete current documents and must remain byte-for-byte
unchanged.

## 3. Current ownership

- `desktopPreviewManifest.json` owns stable Preview identity, category,
  entrypoints and declared embedded dependencies.
- Each Component resolver/contract/renderable module owns its semantic and
  visual behavior.
- `component_classes` owns the current persisted class documents and complete
  Variant snapshots.
- `ComponentClassRepository` owns strict persistence of prepared current rows;
  it does not construct missing definitions.
- editor layout metadata, `FieldDefinition`, `ValueKind` and dictionary
  controls own editable presentation and the generic commit route.
- `EditorUiText.IdentifierLabel` may format a stable id for display only. It
  cannot select a Component, Variant, owner, field, forwarding route or
  persisted reference.

Architecture checks must inspect the current manifest, current committed
database and active owner code. A deleted factory is not acceptable parity
evidence.

## 4. Future development scaffolding

Future Component/Atom creation must be an explicit development workflow. One
scaffolding delivery must supply and validate, as applicable:

- a stable Component identity and explicit category;
- complete owner contract, resolver and renderable entrypoints;
- the manifest route and declared embedded dependencies;
- complete current class/config/design-preview/metadata documents;
- a protected Default Variant with explicit envelope members;
- editor layout and dictionary definitions for every editable scalar;
- full concrete embedded Variant references and explicit forwarding;
- repository/parity data through an explicit provisioning or migration step;
- architecture, database, Preview and desktop tests.

Scaffolding must not infer ownership or references from a display name, type,
position, hierarchy depth or list index. It must not run during normal startup
or expose a generic Add Component operation in the editor.

This phase only establishes the boundary. It does not implement that future
scaffolding workflow.

## 5. Preserved invariants

This cleanup does not change:

- tables, current JSON roots or committed records;
- stable ids or complete Component Variant references;
- explicit Default Variant selection at a new boundary;
- forwarding, local Overrides or Variant snapshot semantics;
- editor field layouts or dictionary controls;
- payload, resolver, bridge or renderer behavior;
- the rename/Variant lifecycle permissions of current definitions.

## 6. Enforcement

Architecture enforcement must fail when:

- the retired runtime component defaults file or seed DTO returns;
- active Data sources contain the retired component factory methods;
- checks rely on dormant factory text for current contract evidence;
- current Component data is missing from the manifest or database;
- current embedded references, Runtime Input kinds or temporal metadata violate
  their explicit contracts.

The desktop build, full tests, strict database validation and byte-level hash
proof remain required before the phase closes.

## 7. Forbidden shortcuts

- copying the retired catalog into a new runtime helper;
- using a resolver or editor as a fresh-record factory;
- constructing a partial Component and relying on repository repair;
- keeping code defaults "for later" beside current persisted documents;
- weakening a current-data assertion because the old factory was removed;
- implementing scaffolding as a normal application side effect.
