# Design and Production UX Separation

This document defines the intended UX direction for separating system design work
from day-to-day production work in the desktop editor.

It is not a replacement for the shot/module architecture contract. The shot,
module and instance workflows are still under active implementation. This
document focuses on how the existing component and module design surface should
be organized so it does not become polluted by production concerns, and so
production later does not need to expose low-level component design controls.

## Problem

The current editor already has a strong component design foundation: component
classes, variants, dictionary-backed fields, embedded components, design preview
and usage navigation are largely oriented toward building a reusable design
system.

The production side has a different purpose. Once components and modules are
defined, day-to-day work should center on shots: preview, timing, approval,
versioning, review state and render readiness.

If both concerns share the same navigation and editing surface, two problems
appear:

- component design decisions can be made accidentally while working on a shot;
- production users are forced to understand low-level component internals when
  they mainly need to assemble, preview and approve concrete shots.

## Top-Level Navigation

The app should expose two clear root areas from the global app header:

```text
[Design] [Production]
```

These should behave as primary workspace switches, not as small filters inside
the current tree.

`Design` owns reusable system definition:

- component classes;
- component variants;
- embedded component composition;
- runtime input contracts;
- test values and design preview fixtures;
- app definitions;
- module definitions;
- themes, palettes, fonts, icons and base media;
- usage impact across the design system.

`Production` owns concrete audiovisual work:

- productions;
- episodes;
- shots;
- module instances;
- actor/app personalization;
- shot preview by frame;
- timing;
- approval state;
- versioning;
- render/export readiness.

Switching between the two root areas should change the left navigation, editor
surface, preview meaning and available actions. The active workspace should be
visually obvious in the app header.

## Terminology

The term `Variant` applies to reusable Component and Module design alternatives.
Production does not create another kind of Variant: it selects one explicit
Module Variant when creating a Module Instance.

Recommended vocabulary:

```text
Component -> variants
App -> app configuration
Module -> module definition / module variants
Module Instance -> one named Screen inside a Shot
Actor -> personalization
Shot -> production context
```

For example, a production may define a single WhatsApp app configuration. If two
actors use WhatsApp, they should not create two separate app identities. They
should share the same app and resolve small contextual differences through actor
or production personalization:

- theme;
- wallpaper;
- navigation bar details;
- actor avatar;
- visible actor/user labels;
- other explicitly allowed contextual values.

The app defines identity and base behavior. The actor or production context
defines personalization. The shot consumes the resolved result.

## Design Workspace Structure

The Design workspace should present design-system entities directly, instead of
burying them in a production tree.

Suggested navigation:

```text
Design
  Components
    Bubble
    Text Input Bar
    Keyboard
    Status Bar
    Navigation Bar
    Media
    Audio
  Modules
    Conversation
  Apps
    WhatsApp
  Themes
  Assets
  Tokens
```

The component editor should tell a consistent story:

```text
Identity
Visual properties
Variants
Embedded components
Design preview
Runtime Inputs / Test Values
Usage
```

The exact card order can evolve, but `Runtime Inputs / Test Values` and `Usage`
should be visible as normal editor cards, not only modal or header-only actions.

## Runtime Inputs / Test Values

Runtime inputs should become the single place where design preview test data is
defined.

This card has two responsibilities:

- document the public runtime API of the component or module;
- provide editable test values used by the isolated design preview.

The card should separate inputs owned by the current entity from passthrough
inputs exposed because of embedded components.

Suggested columns:

```text
Input
Origin
Dictionary
Type
Required
Default
Test value
```

Column meaning:

- `Input`: public runtime input name.
- `Origin`: `Own` or `Embedded: <component>`.
- `Dictionary`: the dictionary/control family that owns the value type.
- `Type`: concrete runtime value type expected by the resolver.
- `Required`: whether runtime data must provide the value.
- `Default`: fallback when runtime data does not provide a value.
- `Test value`: editable preview fixture value.

Example:

```text
Input          Origin            Dictionary  Type      Required  Default       Test value
messageText    Own               text        string    yes       -             Hola...
senderAvatar   Embedded: Avatar  media       assetRef  no        actor.avatar  avatar_marta
isOutgoing     Own               boolean     bool      yes       false         true
bubbleColor    Own               color       token     no        theme.surface theme.accent
```

The editable `Test value` cells must use the same dictionary/value-kind control
route as normal scalar editor fields where applicable:

```text
FieldDefinition
  -> ValueKind
  -> DictionaryFieldControl / registered dictionary control
  -> generic commit path
```

Test values are preview fixtures. Normal editing must not mutate the component
contract, component variant or production data.

The explicit `Save as defaults` command is the only exception: it deliberately
promotes the current test-value set into the component or module input defaults.
It remains separate from variant editing and never changes production data.

## Preview Responsibility

Moving test data into `Runtime Inputs / Test Values` should remove the need for
a separate input panel inside the design preview.

The preview should keep only the external context controls that the designed
entity cannot resolve itself.

For isolated component design preview, keep:

```text
Device
Theme
Mode
Orientation
```

A component cannot fully resolve those values on its own.

For module instance design preview, keep only:

```text
Mode
Orientation
```

Device and theme should be resolved by the shot-like context feeding the module
instance. The module instance should not pretend to own those values.

General rule:

```text
Runtime Inputs / Test Values controls runtime data.
Preview controls only unresolved external context.
```

## Usage Card

The existing header Usage button can remain as navigation to the editor location
that shows usage detail.

Usage should also have a full editor card. The card should help a designer
understand the impact of changing a component, variant, app configuration or
module definition.

Minimum useful content:

- total usage count;
- usage grouped by type;
- direct links to each usage location;
- whether the usage is direct, inherited, passthrough or overridden;
- warnings for broken, unresolved or migration-pending references.

Suggested groups:

```text
Design usage
Production usage
```

`Design usage` shows dependencies inside the reusable system:

- embedded component slots;
- parent components;
- modules;
- app configurations;
- theme or asset references where relevant.

`Production usage` shows concrete shot/module-instance usage once production is
implemented:

- production;
- episode;
- shot;
- module instance;
- approved version impact, when versioning exists.

The Usage card may navigate to production records, but it should not edit
production data from the design workspace.

## Production Boundary

Production should use design outputs without exposing all low-level design
controls by default.

Production may:

- select an app configuration;
- select modules;
- create module instances;
- select the Module and its concrete Variant and assign the instance tree name;
- rename, duplicate or delete the concrete instance without mutating reusable
  Module or Variant definitions;
- feed real runtime inputs;
- resolve actor/app personalization;
- preview by frame;
- manage timing, approval and versions;
- apply local overrides only where explicitly allowed.

Production should not silently redefine global component design. If a user needs
to change reusable design behavior while working in production, the action should
be explicit:

```text
Open in Design
```

This protects the design system from accidental shot-specific drift.

## Implementation Phases

Recommended order:

1. Add the two root workspace switches in the global app header.
2. Split navigation models for Design and Production.
3. Add the read/write `Runtime Inputs / Test Values` card to design editors.
4. Route design preview test data through that card.
5. Remove the duplicated runtime input panel from design preview.
6. Add the detailed Usage card and keep the header Usage button as a shortcut.

## Follow-up UI Work

- When a nested standard editor card expands, keep its full bounds visible in
  the owning editor scroll area when the viewport permits it. The current
  deferred scroll helper needs one consolidated pass across editor, runtime
  input and tree card hosts; do not add local scroll offsets per editor.
7. Add production usage links once shot/module instance workflows are complete.

The first useful milestone is not a complete production workflow. It is a clean
Design workspace where components and modules can define contracts, test values
and usage impact without depending on unfinished shot editing.
