# System overview

Status: normative.

## Purpose

MOCKUPS is a desktop authoring application for deterministic interface
productions. It has two independent but connected workflows:

- **Design** defines reusable resources, Component Classes, complete Component
  Variants, Apps, Modules and complete Module Variants.
- **Production** assembles Episodes, Shots and ordered Screens from those
  definitions, supplies each Screen payload and authors its animation.

Design produces reusable definitions. Production consumes exact definitions
and complete Variant references. Preview resolves the selected Design fixture
or Production frame without changing authored data.

Render Presets are Production Data, but Render Mode and a complete
render/export workflow are not part of the current application.

## System map

```text
SQLite current project data
        │
        ├── focused repositories
        │       └── strict current documents
        │
        ├── editor data sources and document stores
        │       ├── dictionary fields
        │       ├── structured collections
        │       └── owner-relative animation
        │
        └── Preview payload preparation
                ├── explicit context and forwarding
                ├── exact manifest routing
                ├── owner resolver
                ├── owner renderable
                ├── common resolved primitives
                └── generic web renderer
```

## Core domains

### Project resources

A Project owns every reusable and Production record in its workspace. Records
never resolve across Projects.

### Design definitions

- Palette Colors, Themes and Icon Themes define semantic visual context.
- Apps group Modules.
- Component Classes define schema and resolver identity.
- Component Variants are complete named snapshots.
- Modules define Production Screen behavior.
- Module Variants are complete named snapshots.
- Devices, Actors, Production Fonts and Render Presets are exposed through
  Production Data.

### Production sequence

- An Episode owns ordered Shots.
- A Shot owns an explicit Actor and ordered Screens.
- A Screen is a persisted Module Instance with one exact Module Variant,
  payload, transition, duration and animation document.
- Shot time is the ordered aggregate of its Screens.

### Preview

Preview is a resolved view of current authored state. It does not own Component
defaults, Production payload, context inheritance, runtime forwarding,
animation timing or component layout rules.

## Layer ownership

### SQLite and repositories

Repositories own table SQL, row mapping and prepared writes. They do not own
UI, runtime composition, timing, Preview resolution or migration behavior.

### Domain services and document stores

Typed services own complete current documents, exact context, reference
discovery, collection operations and animation persistence. A service consumes
focused data sources instead of a general database handle whenever the route
crosses domains.

### Editor shell

`MainWindow` owns window initialization, the three-panel shell, selected tree
state, workspace switching, generic editor-card composition, Preview host
wiring, generic modal hosting and session visual state.

Editor-specific fields, collections, persistence rules, asset logic and domain
dialogs live in their owning editor or shared editor service.

### Dictionary

All editable scalar values use:

```text
editor layout metadata
→ FieldDefinition
→ ValueKind
→ registered dictionary control
→ generic commit path
→ owning document or repository
```

### Preview preparation

Payload preparation owns the complete resolved input envelope for one route.
It applies explicit Production context and forwarding before registry
dispatch.

### Component and Module owners

Each Component or Module owns its contract, resolver and renderable module.
Registries select those owners by exact manifest id and add no semantics.

### Bridge and renderer

Common Preview helpers resolve only generic values and visual primitives. The
web renderer paints fully resolved nodes. Neither layer knows Component or
Module business rules.

## Dependency direction

Dependencies flow from shell and domain coordination toward narrow data
sources, contracts and common primitives. Generic layers never import concrete
Component owners.

```text
shell
→ editor/domain owner
→ typed data source or document store
→ focused repository
→ SQLite context

payload factory
→ manifest route
→ Component/Module resolver
→ Component/Module renderable
→ common Preview helpers
→ generic renderer
```

## Non-negotiable identities

- Stable ids identify every persisted record, Variant, collection item, slot,
  state and animation target.
- Component references use
  `componentClassId::variant::variantId`.
- Module Instances store one exact Module Variant id.
- Forwarding is explicit.
- Local Overrides are explicit.
- A new Component boundary crosses into that class's protected Default Variant.
- Keyframes are relative to their stable temporal owner.
- Preview receives a complete resolved result.

Names, types, hierarchy depth, sibling order and visual position are never
substitutes for explicit identity.
