# Design system and reusable definitions

Status: normative.

## Design workflow

Design is the authoring workspace for reusable definitions and visual
resources. It establishes the exact contracts that Production later consumes:

- System Palette Colors and Icon Themes;
- Component Classes and Component Variants;
- System Apps, Modules and Module Variants;
- isolated Preview fixtures and temporary Test Values.

Design does not create Production sequences or persist Screen payloads.

Themes and Production Palette values are not Design editors. They are
Production-owned resources presented inside Production Data.

## Tokens, palette and Themes

Theme tokens are semantic values, not UI styling shortcuts. Components refer
to tokens and global Palette Color ids; resolution to the active Production's
required RGB values and final light/dark values happens in the Preview pipeline.

Positive and negative actions use `theme.colors.positive` and
`theme.colors.negative`; their foreground uses `theme.colors.onAction`.
Components never hardcode resolved RGB values. A direct Palette id is stable
across Productions while each Production owns its RGB; Theme owns semantic
light/dark selection and contrast.

Typography never stores a concrete Production Font id outside Theme. Reusable
and Runtime documents use only `theme`, `theme.system` or `theme.emoji`; Theme
maps those roles to its exact text, system and emoji Production Fonts.

Visual spacing fields use `theme.spacing.*` tokens. Compound light/dark values
preserve their explicit pair labels and use their registered dictionary
control.

A Production Screen receives Theme context only through its exact ownership:

```text
Screen → exact Screen Theme
```

The Shot Actor default Theme initializes a newly created Screen once. Changing
that Actor default later does not change an existing Screen. There is no Theme
inference or runtime fallback from Actor, App, Module, Variant, label, order or
type.

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

Component Classes and their complete Variants are System records. They have no
Project owner and appear in every Production's Design tree through the same
stable ids. Editing one class or Variant therefore changes the definition used
by every Production; Production-specific visual resolution remains owned by
that Production's Theme and Palette values.

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

Default persists as `protected: true` and `locked: true`. Unlocking Default
changes only the current editor session; it permits authored writes while that
session remains open but never rewrites the Variant envelope. A new session
therefore starts with Default locked without startup repair or any other
persistence write. Non-Default Variant locks remain authored metadata.

`Preset` is not a current authored concept. Render output choices belong to
workstation-local queue jobs and are never Component or Module Variants.

## Apps, Modules and Module Variants

Apps, Modules and their complete Module Variants are System records. They have
no Project owner and appear in every Production's Design tree through the same
stable ids. Editing any of them changes the reusable definition consumed by
all Productions. An App groups Module definitions. A Module owns:

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

Project context is supplied only by the selected Design-tree projection or by
the Production Screen's owning Shot. It is never stored on the App, Module or
Module Variant. App wallpaper and icon paths remain authored relative resource
references: Preview resolves them against the explicit Project context, so
each Production must provide the referenced files. Module Design Test Values
instead use only the System Preview fixture catalog.

## Isolated Design Preview

Design Preview uses the current selected Variant and an isolated sample
fixture. Temporary Test Values exercise declared Runtime Inputs without
changing the Variant until the user explicitly saves them as defaults.

Actor and media Test Values resolve only through the System Preview fixture
catalog installed in App Support. They never store a Production Actor id or a
Project media path. Media file and media-directory Test Values use bounded
fixture selectors instead of filesystem browsers. Production payloads reject
System Preview actor ids. They may carry a `system-preview://` media reference
only when the effective Production Runtime boundary has selected that exact
Design `defaultValue` for an empty or unavailable authored media value.

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
