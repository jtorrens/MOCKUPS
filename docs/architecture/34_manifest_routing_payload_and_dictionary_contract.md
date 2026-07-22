# Manifest, Routing, Payload and Dictionary Contract

Status: normative.

This document governs the identities and entrypoints of desktop Preview
components and modules, registry behavior, payload preparation, Runtime Input
forwarding, recursive temporal context and dictionary value-kind dispatch. If
an older implementation note conflicts with this contract, this contract wins.

It extends, without weakening:

- `23_embedded_component_composition_contract.md`;
- `24_desktop_preview_component_architecture.md`;
- `26_desktop_preview_pipeline.md`;
- `29_animation_parameter_timeline_contract.md`;
- `31_structural_stacks_slots_and_module_instances.md`;
- `33_persistence_and_migration_contract.md`.

## 1. Objective

The Preview route has one explicit owner for each decision:

```text
committed current data
→ canonical Preview manifest
→ payload preparation and explicit forwarding
→ id-only registry routing
→ owning resolver and renderable
→ common resolved-value helpers
→ generic renderer
```

No layer may compensate for an incomplete previous layer with an inferred
component, module, Variant, value kind, default, runtime value or renderable.
Unknown current data is an error to fix or migrate, not a signal to guess.

## 2. Canonical Preview manifest

`src/desktop-preview/desktopPreviewManifest.json` is the single committed
authority for Preview identities and ownership metadata shared by TypeScript
and the desktop shell.

Each component entry declares:

- its stable `componentType`;
- its navigation category: `atom`, `component` or `system`;
- its migration status;
- its contract, resolver and renderable entrypoints;
- the concrete component types it is allowed to embed.

Each module entry declares:

- its stable record-class id;
- its label;
- its resolver and renderable entrypoints;
- the concrete component types it is allowed to embed.

The manifest may identify and route owners. It must not contain field defaults,
layout algorithms, token resolution, runtime values, Overrides or renderable
construction.

The following are forbidden as parallel authorities:

- a C# switch that reclassifies component types;
- a separate module option list in the editor;
- a hard-coded architecture-check dependency list;
- a registry-only component or module absent from the manifest;
- a database component or module class absent from the manifest.

Adding a component or module requires one complete manifest entry and all of
its declared entrypoints in the same change. A module without an implemented
resolver/renderable route is not creatable. There is no `module.generic` or
generic Add Module fallback.

### 2.1 Definition lifecycle

App and Module definition nodes are development-owned. In the desktop editor
their only lifecycle action is Rename. The editor must not create, duplicate or
delete either definition, and the Apps root must not expose Add. A new App or
Module enters current data only through an explicit development/scaffolding
change that supplies its stable identity, complete contract, manifest route,
resolver, renderable and required migration together.

Module Variants are user-authored data inside an existing Module definition.
The active complete Variant may be cloned to create or duplicate another
Variant. Variants may be renamed without changing their stable id or any full
Variant reference. A Variant may be deleted only when it is unused, unlocked
and not protected. The protected default Variant may be renamed but cannot be
deleted. No operation may infer a Variant from its name, index or position.

## 3. Registries route only

Component, module and design-kind registries are maps from an explicit stable
id to one owning entrypoint. Their only behavior is:

1. receive a prepared payload;
2. locate the exact declared route;
3. call it;
4. reject an unknown route.

Registries must not:

- apply Runtime Input forwarding;
- fill missing payload members;
- select a default Variant;
- merge defaults or Overrides;
- parse component configuration;
- resolve tokens or assets;
- construct boxes, styles, error surfaces or renderable nodes;
- contain component or module business rules;
- return a visual "unsupported" fallback.

Recursive component composition re-enters the same component payload boundary
and then the same registry. The parent may import an embedded child directly
only when the manifest declares that owned dependency and the parent owns the
child slot contract.

## 4. Payload boundary and forwarding

The payload boundary is the only Preview routing layer allowed to prepare a
payload before registry dispatch. It validates required payload documents and
applies the generic explicit forwarding contract.

Forwarding remains data, never convention. It requires stable source and target
field ids, declared JSON keys and explicit projection metadata. It must not be
derived from labels, names, kinds, component types, collection order, hierarchy
depth or field position.

Forwarding changes the prepared owner configuration by publishing the declared
runtime values at the declared child boundary. It does not mutate the persisted
instance payload, invent missing values or select a Variant. Crossing a new
component or module boundary still requires a complete concrete Variant
reference and the explicit default Variant when the boundary is first created.

The reserved `$forwardedInputs` envelope is optional, but when present it is a
JSON object whose every entry is a complete object definition. Required
Preview `inputs`, `collections` and present `actions` keep their declared array
roots. A projected collection, nested Runtime contract, metadata-key list or
forwarded value with the wrong root is invalid; payload preparation must not
replace it with an empty object or array, skip malformed entries or manufacture
a missing nested contract.

Missing required forwarding metadata or a partially supplied forwarding group
is an error. A registry, resolver, bridge or renderer must not repair it.

Declarative `actions` and collection `itemActions` are also strict current
contracts. Every entry is an object with a unique stable id, explicit label,
play input, time key/unit, completion behavior and finite duration source.
Optional booleans, numbers, string lists, target options and paired target or
visibility metadata keep their exact JSON types and completeness. Readers must
not omit malformed actions, derive id from `playInputId`, supply `Play`, default
an undeclared time unit, coerce numeric/boolean strings or filter malformed list
members.

## 5. Runtime payload and temporal owner envelope

`DesignPreviewPayload` carries two related but different views:

- `designPreviewJson` is the runtime value document for the resolver at the
  current boundary. It becomes local when an embedded component is entered.
- `runtimeContractJson` is the effective complete runtime envelope for the
  selected top-level temporal owner. It contains the declared input/collection
  metadata together with the effective current owner values needed by the
  generic owner timeline.

`runtimeContractJson` is therefore not a schema-only document and is not a
second persisted payload. At the top-level boundary it is materialized from the
same explicit effective context as the runtime values:

- isolated Design inspection may apply session-only Test Values;
- a Module Instance uses its persisted instance content and selected Module
  Variant contract;
- a Shot selects its explicit active Module Instance and translates the Shot
  playhead to that Screen's local frame.

Test Values are never written to a Module Instance by previewing or pressing
Play. Production instance payload remains the persistent authority.

When composition enters an embedded component, `designPreviewJson` changes to
that child's explicitly forwarded/local inputs. `runtimeContractJson` must be
preserved unchanged through every recursive child boundary. Replacing it with
the child's local values destroys recursive temporal ownership: the child can
no longer resolve its first-appearance origin relative to the Screen.

The owner envelope is consumed by the generic timeline using stable owner and
target ids. It does not authorize a resolver to infer ownership from indices,
positions or component names.

## 6. Resolver and renderable ownership

A component or module resolver owns semantic interpretation of its own current
contract, including frame state. Its paired renderable owns composition into
standard final primitives and explicitly declared child slots.

The boundary is:

```text
manifest route
→ owner resolver
→ owner renderable
→ standard renderable nodes
```

Resolver/renderable modules may use parameterized common helpers. Common
helpers must not import concrete component or module owners, route by concrete
name or supply owner-specific defaults.

The bridge may translate only generic resolved values such as tokens, palette
colors, alpha, assets, design units, placement, text, image, SVG, surface and
shadow primitives. The renderer paints final nodes. Neither may know component
contracts, Variants, Overrides, forwarding, database records or animation
business rules.

`MainWindow` remains shell-only and cannot participate in payload semantics,
component/module routing, forwarding or resolver selection.

## 7. Dictionary and Runtime Input value kinds

Every editable scalar definition declares one current canonical `ValueKind`.
The dictionary registry is exhaustive over the `ValueKind` enum and routes each
kind to a registered dictionary control. Text controls are explicit entries for
their declared text kinds; they are not a fallback for unknown kinds.

Runtime Input definitions also persist an explicit canonical `valueKind`.
Their `kind` describes the runtime input shape; `valueKind` selects the editor
and serialization contract. The shared mapping between both vocabularies must
be exhaustive. A current definition's stored `kind` must exactly match the
canonical shape declared for its stored `valueKind`. The mapping constructs new
definitions and validates current ones; normal reads never derive or replace a
missing/mismatched value from the other field.

The same owner parses `defaultValue` into the runtime shape declared by
`ValueKind`. Boolean, numeric, icon-list, structured-collection and
`BehaviorTiming` defaults are strict; malformed values are not converted to
`false`, zero, `{}`, `[]` or text. A projected `StructuredCollection` with an
explicit collection contract owns an empty initial array even when it has no
scalar `defaultValue`. Every other current Runtime Input definition requires an
explicit string default, including an explicitly empty string where empty is
meaningful.

Persisted Runtime values and editor-authored Test Values use the same
`ValueKind` serialization owner. Top-level Runtime Inputs and structured
collection fields must be declared exactly once in the effective contract,
must persist only when their source is `runtime`, and must match their declared
boolean, numeric, string, array or object shape. Animation keyframe authoring
uses the same value serializer; it cannot turn invalid input into false or
zero.

Compound dictionary values use the same rule before controls render or commit.

Component Class config, every complete Component Variant config and explicit
local Overrides use that same dictionary `ValueKind` owner. A descriptor
default applies only when its path is absent. Once a field path is present, its
stored JSON scalar/object/array shape is current data and must validate exactly;
readers and writers must not coerce it to false, zero, text, `{}`, `[]` or the
descriptor default.

String-backed compound kinds also have exact current grammars. `IntegerPair`
contains two integers; Theme-token and Palette-color pairs contain two non-empty
members; Palette-color-alpha pairs contain two colors plus two finite alpha
values from zero to one. `Alpha` and `HueDegrees` keep their intrinsic ranges.
Missing pair members or out-of-range values are invalid, not empty/default
members for a control to reconstruct.
Integer and Decimal controls likewise require an exact finite current value and
enforce any declared `NumberDefinition` minimum/maximum. A temporary text draft
may be incomplete while the user types, but it must not change or commit the
current value until it parses exactly and falls inside the declared range.
Every pair field also declares two non-empty presentation labels explicitly.
Labels are metadata only and do not change the stored value, but they may not be
derived from a field id, JSON key, type, hierarchy or position. A Runtime Input
pair without both current label fields is an invalid definition; generic
readers and controls must not supply `W`/`H`, `X`/`Y`, `Light`/`Dark` or `A`/`B`.
`ComponentInputBindings` requires an object and a valid explicit forwarding
envelope. `StructuredCollection` and `IconSlots` require arrays of object items
with unique non-empty stable ids. Blank, malformed, wrong-root or duplicate-id
documents never become `{}`, `[]` or position-derived item identities.

Runtime Input option lookup additionally follows contract 55. The typed data
source supplies exact current Actor, Palette and complete Component variant
options; generic factories retain the metadata-driven `ValueKind` and declared
dynamic-list mapping before the registered dictionary control is created.

The following are invalid current data:

- missing or case-insensitive `valueKind` names;
- an unknown Runtime Input `kind`;
- a `kind`/`valueKind` pair that does not match the canonical shared mapping;
- a missing or malformed default for its declared `ValueKind`;
- a pair definition without two explicit non-empty presentation labels;
- a malformed `BehaviorTiming` object or unsupported timing mode;
- an undeclared persisted Runtime key or collection field;
- a persisted or Test Value whose JSON shape contradicts its `ValueKind`;
- a dictionary registry miss;
- a silent generic text control;
- an incomplete id, label or JSON key;
- a local editor control that bypasses `FieldDefinition` and the dictionary.

Changing either vocabulary requires an explicit migration of canonical source
data and the committed desktop database. No alias or compatibility parser may
remain after that migration.

## 8. Current database validation

Read-only startup validation rejects:

- component or module classes absent from the canonical manifest;
- Runtime Input definitions without stable id, label and JSON key;
- unknown or non-canonical Runtime Input kinds/value kinds;
- missing or malformed Runtime Input defaults for their current `ValueKind`;
- a non-object forwarding envelope/definition or wrong-root forwarding
  projection document;
- missing, parent-owned or wrong-shape current Runtime values;
- retired generic module layouts or records;
- any other persistence violation defined by contract 33.

Validation reports only. It must not add manifest entries, value kinds,
defaults, module routes or repaired JSON.

## 9. Architecture enforcement

`npm run check:architecture` must cover at least:

- manifest entrypoint existence and TypeScript/C# consumption;
- exact manifest/registry component and module parity;
- declared embedded dependency parity;
- registry purity and throw-on-unknown behavior;
- forwarding ownership by the payload boundary;
- absence of component-specific bridge, common-helper and renderer knowledge;
- exhaustive dictionary registry coverage;
- strict Runtime Input kind/value-kind parsing;
- strict Runtime Input default parsing through that same owner;
- exact Runtime value serialization and current-value validation;
- manifest and Runtime Input parity in the committed database;
- absence of retired generic App/Module creation and layout paths;
- rename-only App/Module definition permissions and repository enforcement.

The architecture check complements compilation, strict database validation and
behavioral tests. It does not replace them.

## 10. Required checks for this boundary

Before a routing, payload or dictionary change is complete:

```text
[ ] canonical manifest and all registries have exact component/module parity
[ ] all declared entrypoints and embedded dependencies validate
[ ] unknown routes and value kinds fail explicitly
[ ] forwarding occurs only at the payload boundary
[ ] recursive children preserve the complete temporal owner envelope
[ ] isolated Test Values do not become persisted Production payload
[ ] bridge, common helpers, renderer and MainWindow remain generic
[ ] committed database passes strict read-only validation
[ ] Preview and desktop animation suites pass
[ ] architecture check and desktop build pass
```

## 11. Forbidden shortcuts

- adding a component or module to only one language or registry;
- creating, duplicating or deleting an App or Module from generic tree actions;
- creating a generic module before its owner resolver/renderable exists;
- returning an unsupported visual node for unknown current data;
- defaulting a missing payload JSON document to `{}`;
- replacing the root temporal envelope with an embedded child's inputs;
- applying forwarding inside a registry;
- inferring a `ValueKind` while reading current data;
- routing an unknown `ValueKind` to text;
- teaching the bridge, renderer or shell a concrete component/module rule;
- retaining a fallback because old committed data has not been migrated.
