# App and Module Definition Persistence Contract

Status: normative.

This document governs the App and Module definition slice of the staged desktop
repository extraction. It extends contracts 26, 31, 33, 34, 35 and 36 without
changing schema, current data, lifecycle actions, Variants, Runtime Inputs or
Preview behavior.

## 1. Definition persistence ownership

App and Module definition rows follow one persistence route:

```text
apps + modules current definition rows
→ AppModuleRepository
→ SpikeDatabase compatibility facade
→ field, Variant, tree or payload service
```

`AppModuleRepository` owns exact definition lookup, coordinated tree row
materialization, complete current JSON documents, direct scalar/document
writes, sort-order persistence and Rename/node writes for `apps` and `modules`.

The repository returns persistence records, not tree nodes, dictionary controls,
Variant controls, Runtime contracts or renderables. Existing editor callers
continue through `SpikeDatabase` while the facade is reduced in vertical
slices.

## 2. Current JSON and Variant envelopes

App `config_json` and `metadata_json` are required current objects. Module
`config_json`, `design_preview_json` and `metadata_json` are required current
objects. Every Module metadata document contains the complete required Variant
array governed by contract 35.

Repository reads validate these roots and the Module Variant envelope without
repair. Document writes validate the complete replacement before the first
mutation. They never supply `{}`, create a missing Variant, filter malformed
Variants or infer protection from an id.

## 3. Domain behavior stays outside persistence

The following remain in their owning domain/services:

- App wallpaper and icon field-to-JSON paths;
- the rule that system Apps inherit Actor wallpaper;
- Module configuration field paths and embedded component Overrides;
- Component Variant reference validation;
- Module Variant creation, duplication, rename, lock and protected deletion;
- explicit Runtime Input forwarding and effective contract preparation;
- Module Instance payload reconciliation and animation-track cleanup;
- Screen duration, temporal ownership and Preview resolution.

Those owners may prepare a complete current document and ask the repository to
persist it. They must not retain direct definition-table SQL.

App wallpaper/icon numeric fields are parsed strictly by their field domain
before the complete prepared document is sent to the repository. Invalid or
non-finite input is rejected and must not be converted to zero.

Cross-domain Module Instance and validation queries may temporarily join the
definition tables while their own repository/payload slices remain pending.
They cannot mutate or repair App/Module definitions.

## 4. Lifecycle

App and Module definition nodes expose Rename as their only lifecycle action.
The repository supports Rename because it is an explicit existing action. It
does not expose create, duplicate or delete for definitions.

Creating or retiring a definition remains an explicit development/scaffolding
workflow that must also provide its stable id, manifest route, resolver,
renderable, contracts and data migration. Module Variants remain authored data
and preserve their existing lifecycle and full references.

No definition or Variant is selected from name, type, ordering or tree position.
All relationships use exact stable ids and full Variant references.

## 5. Validation

Automated enforcement verifies:

- `IAppModuleRepository` and `AppModuleRepository` are explicit;
- `SpikeDatabase` constructs and delegates through the repository;
- ProjectContent, ModuleVariants, ComponentClasses and tree orchestration retain
  no direct `apps` or `modules` CRUD SQL;
- repository reads match the facade for App, Module and Module-to-App context;
- direct fields and prepared document writes round-trip on a disposable copy;
- malformed App/Module JSON and incomplete Variant envelopes fail without a
  partial write;
- Rename-only lifecycle behavior remains unchanged;
- the committed database remains byte-for-byte unchanged by extraction.

## 6. Forbidden shortcuts

- passing the repository into MainWindow or an editor control;
- moving field-path, forwarding, Override or Runtime behavior into persistence;
- adding App/Module create, duplicate or delete to normal editor lifecycle;
- accepting or repairing an incomplete Module Variant envelope;
- falling back from a missing Variant config to Module class config;
- deriving identity or references from a label, type or order;
- mutating definitions from a reader, startup validator or Preview path;
- changing parity data as an incidental extraction step.
