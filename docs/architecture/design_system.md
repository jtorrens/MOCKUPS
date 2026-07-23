# Design system and reusable definitions

Status: normative.

## Design workflow

Design is the authoring workspace for reusable definitions and visual
resources. It establishes the exact contracts that Production later consumes:

- tokens, Palette Colors, Themes and Icon Themes;
- Component Classes and Component Variants;
- Apps, Modules and Module Variants;
- isolated Preview fixtures and temporary Test Values.

Design does not create Production sequences or persist Screen payloads.

## Tokens, palette and Themes

Theme tokens are semantic values, not UI styling shortcuts. Components refer
to tokens and Palette Color ids; resolution to final light/dark values happens
in the Preview pipeline.

Visual spacing fields use `theme.spacing.*` tokens. Compound light/dark values
preserve their explicit pair labels and use their registered dictionary
control.

A Production Screen receives Theme context only through its exact Shot:

```text
Screen → Shot → Shot owner Actor → Actor default Theme
```

There is no Theme inference from App, Module, Variant, label, order or type.

## Component Classes

A Component Class owns:

- a stable class id;
- schema and dictionary field definitions;
- Runtime Input contract;
- complete Variants;
- Design Preview fixture;
- resolver identity;
- renderable implementation;
- declared embedded dependencies;
- editor layout metadata.

The manifest and committed Component Class row must agree. A generic runtime
catalog never manufactures definitions.

Atoms are the simplest Component owners and follow the same explicit contract,
route and persistence requirements as composed Components.

## Component Variants

A Component Variant is a complete named snapshot, not a partial preset.
Composition stores the full reference:

```text
componentClassId::variant::variantId
```

The parent class owns the schema and Variant list. The selected Variant owns
its complete authored config. A newly saved Variant clones the complete active
Variant and receives a new stable id.

The protected Default Variant is the entry point when a new boundary crosses
into a Component Class. It may be renamed and cannot be deleted. Other
Variants may be created, duplicated, renamed and deleted only when unlocked
and unused.

`Preset` is a separate term used by Render Presets and reserved for future
reusable recipes that are not Variants.

## Apps, Modules and Module Variants

An App groups Module definitions. A Module owns:

- an exact manifest id and route;
- its Runtime Input and collection contract;
- complete Module Variants;
- duration policy;
- resolver and renderable implementation;
- Design Preview fixture;
- editor layout metadata.

App and Module definitions expose Rename as their lifecycle action. Creating,
duplicating or deleting a definition is a development workflow because the
operation must also supply or remove its complete manifest, implementation,
contract, migration and validation surface.

Module Variants are authored data. They can be created by cloning the active
complete Variant, duplicated, renamed and deleted when unused, unlocked and
not protected. Production stores an exact Module Variant id.

## Isolated Design Preview

Design Preview uses the current selected Variant and an isolated sample
fixture. Temporary Test Values exercise declared Runtime Inputs without
changing the Variant until the user explicitly saves them as defaults.

Runtime Inputs remain product inputs. The Design Preview surface does not
create a separate input contract and does not own Component-specific behavior.

## Definition development

Creating an Atom, Component Class or Module is a strict scaffolding workflow.
The workflow must generate and validate the complete owner set in one coherent
revision:

1. stable identity and manifest entry;
2. current persisted definition and complete protected Default Variant;
3. dictionary schema and Runtime Input contract;
4. resolver and renderable owner;
5. declared embedded dependencies and forwarding;
6. editor layout metadata and Design Preview fixture;
7. migration or seed update when persisted data changes;
8. architecture, contract and Preview validation.

Normal application UI does not offer Add or Delete for these definition types.
No step may be inferred from a name, type, sibling, hierarchy position or
manifest order.
