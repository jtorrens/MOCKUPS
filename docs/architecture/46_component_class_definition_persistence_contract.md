# Component Class Definition Persistence Contract

Status: normative.

This document governs the Component Class definition slice of the staged
desktop repository extraction. It extends contracts 23, 24, 33, 34, 35 and 36
without changing component behavior, Variants, embedded composition, Runtime
Inputs, payloads or Preview resolution.

## 1. Definition persistence ownership

Component Class rows follow one persistence route:

```text
component_classes current definition rows
→ ComponentClassRepository
→ SpikeDatabase compatibility facade
→ component field, Variant, tree, reference or payload service
```

`ComponentClassRepository` owns exact row lookup, coordinated tree/validation
row materialization, project-scoped ordered queries, required current JSON root
validation, complete document writes and explicit Rename/node writes for
`component_classes`.

The repository returns persistence records. It does not return tree nodes,
dictionary controls, component contracts, Variant controls, renderables or
Preview payloads.

## 2. Current documents and Variants

`config_json`, `design_preview_json` and `metadata_json` are required current
objects. `metadata_json.presets` is the complete current Component Variant
array governed by contract 35 and must contain an explicit `default` Variant.

Repository reads validate every root and the complete Variant envelope without
repair. Document writes validate the complete replacement before the first
mutation. They never supply `{}`, construct a missing Variant, filter malformed
entries, infer protection/lock from an id or fall back from Variant config to
class config.

The component domain remains responsible for preparing coordinated writes when
editing the protected default Variant: `config_json` and the complete default
Variant snapshot are updated explicitly together. The repository validates and
persists the prepared documents; it does not derive, merge or synchronize one
from the other.

## 3. Component behavior stays outside persistence

The following remain in their existing domain owners:

- `FieldDefinition`, `ValueKind`, dictionary controls and field JSON paths;
- component type, manifest route, contract, resolver and renderable identity;
- Component Variant creation, duplication, rename, lock and protected deletion;
- full `componentClassId::preset::presetId` parsing and reference validation;
- embedded slots, concrete child Variants and local Overrides;
- explicit Runtime Input definitions, forwarding and test values;
- structured component collections, States and animation contracts;
- Theme/token interpretation, payload preparation and Preview resolution.

Those owners may prepare a complete current document and ask the repository to
persist it. They must not retain direct Component Class table SQL.

Whole-database validation and the explicit cross-domain Usage projection may
retain read-only inventory queries while their aggregate services are separate.
They cannot mutate, normalize or repair Component Class rows.

## 4. Lifecycle and scaffolding

A Component Class definition exposes Rename as its only normal editor lifecycle
action. Creating or retiring a class belongs to the explicit development and
scaffolding process that also supplies its stable id, manifest entry, editor
layout, resolver, renderable, contract, seeds and migration.

Component Variants remain authored data. Their existing explicit lifecycle is
unchanged and every stored/reference identity remains stable and complete.

## 5. Validation

Automated enforcement verifies:

- `IComponentClassRepository` and `ComponentClassRepository` are explicit;
- `SpikeDatabase` constructs and delegates through the repository;
- ComponentClasses, ComponentClassVariants, ComponentClassReferences and tree
  orchestration retain no direct `component_classes` CRUD SQL;
- repository and facade reads agree for definition identity/current documents;
- design-preview, coordinated config/metadata and Rename writes round-trip on a
  disposable database copy;
- malformed roots and incomplete Variant envelopes fail before mutation;
- startup remains byte-for-byte read-only;
- the committed database remains unchanged by the extraction.

## 6. Forbidden shortcuts

- passing the repository into `MainWindow` or an editor control;
- moving field paths, component types, forwarding, Overrides or resolver logic
  into persistence;
- adding Component Class create/delete to the ordinary editor;
- repairing or synthesizing the default Variant on read or write;
- silently synchronizing class/default configs inside the repository;
- deriving Variant or class identity from names, order, labels or component
  positions;
- mutating Component Classes from startup validation, Usage or Preview reads;
- changing schema, seeds or parity data as an incidental extraction step.
