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

Render Queue is a workstation-local Production workflow. A concrete Shot
creates one immutable child job per requested appearance. Output mode, route,
name and version belong to that queued job; they are not authored Shot or
Project records.

An optional local integration may let VFX Shot Manager govern a Project's
Production, Season and Episode identities, technical Shot names and directory
layout. MOCKUPS still creates and owns the Shot, its Actor, Screens and
creative data.

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
        ├── optional Shot Manager integration
        │       ├── strict loopback read-only client
        │       ├── Episode identity synchronization
        │       ├── validated local folder materialization
        │       └── portable predefined output routes
        │
        ├── Preview payload preparation
        │       ├── explicit context and forwarding
        │       ├── exact manifest routing
        │       ├── owner resolver
        │       ├── owner renderable
        │       ├── common resolved primitives
        │       └── generic web renderer
        │
        └── workstation-local Render Queue
                ├── immutable Shot frame snapshot
                ├── sequential recoverable jobs
                ├── clean Production raster frames
                └── MOV or image-sequence output
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
- Devices, Actors and Production Fonts are exposed through Production Data.

### Production sequence

- An Episode owns ordered Shots.
- A Shot owns an explicit Actor and ordered Screens.
- A Screen is a persisted Module Instance with one exact Module Variant,
  payload, transition, duration and animation document.
- Shot time is the ordered aggregate of its Screens.
- An associated Project stores exact external Episode bindings. Every Shot
  owns a stable local number; its last portable Shot Manager render contract
  is a refreshable cache rather than a creation-time requirement.

### Preview

Preview is a resolved view of current authored state. It does not own Component
defaults, Production payload, context inheritance, runtime forwarding,
animation timing or component layout rules.

### Render Queue

The visible editor creates a complete immutable snapshot by streaming resolved
frame documents into a content-addressed local store. It never accumulates the
complete Shot in memory. A local sequential worker uses the raster Preview
pipeline to request one stored frame at a time and encode it without reading
current authored data again. Queue persistence, progress, frame storage and
last route choice are local workstation state, outside the portable Project
database.

Production exposes that owner through a permanent Render Queue section. Shot
rows open a separate batch-creation modal; the central queue panel monitors
the shared worker and remains accessible independently of Shot Manager route
availability. Shot Manager owns the portable relative route; the worker
securely creates its missing directories at job start before publishing the
immutable output.

## Layer ownership

### Physical .NET boundaries

Every extracted layer is a separate project. A source file cannot cross a
layer unless its project declares that exact `ProjectReference`; the evaluated
project graph is an executable repository contract.

`Mockups.Domain` owns dependency-free current value objects and strict document
rules. It has no project or package references. The desktop application may
consume Domain, but Domain cannot see Avalonia, SQLite, Preview runtime or the
desktop assembly. Each later extraction must preserve that direction and add
its exact allowed edge to the project-boundary test.

`Mockups.Application` owns UI-independent application contracts, DTOs and the
window-session transition owner. `EditorWorkspaceCoordinator` consumes only
`IEditorNavigationDataSource` and publishes immutable `EditorSessionState`
snapshots and explicit effects for workspace, Production, navigation, editor
and Preview changes. Application may reference Domain and has no package
capabilities. In particular, its project cannot compile a reference to
Avalonia or `Microsoft.Data.Sqlite`.

`Mockups.Persistence.Sqlite` owns the SQLite context, focused repository
implementations, table mapping and the transitional `SpikeDatabase`
compatibility facade. It may reference Application and Domain and is the only
production project allowed to reference the SQLite packages. It cannot
reference Avalonia or Desktop. Each SQLite context owns its write
coordination; opening an unrelated database never shares a process-global
write lock.

`Mockups.Desktop.Host` is the executable composition boundary and the only
production project allowed to reference both Desktop and Persistence.Sqlite.
It opens the current SQLite compatibility facade and projects it into the
narrow Application ports required by one desktop session.
Its `ApplicationStartupCoordinator` validates the manifested Preview bundle
and opens the current database on a controlled worker. Startup returns one
typed result; only `Success` can create `MainWindow`. Missing or invalid inputs
open a recovery surface without constructing a partial editor session.
`Mockups.Desktop` references Application only; Domain remains reachable solely
through the contracts exposed by Application. `MainWindow`
receives an already composed session and cannot compile a reference to the
database context, SQLite packages or the Persistence assembly.

### SQLite and repositories

Repositories own table SQL, row mapping and prepared writes. They do not own
UI, runtime composition, timing, Preview resolution or migration behavior.

### Domain services and document stores

Typed services own complete current documents, exact context, reference
discovery, collection operations and animation persistence. A service consumes
focused data sources instead of a general database handle whenever the route
crosses domains.

### Editor shell

`EditorWorkspaceCoordinator` is the single owner of the loaded tree, current
workspace, active Production, selected node, embedded editor context, remembered
workspace and Variant selections, Preview transition revision and obsolete
tree-load cancellation. Its state and transition tests compile without
Avalonia or SQLite.

`MainWindow` owns window initialization, the three-panel shell, generic
editor-card composition, Preview host wiring, generic modal hosting and
application of coordinator transitions to visual controls. It does not retain
parallel mutable copies of workspace session state.

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

Shot Manager editor
→ integration coordination service
→ authenticated read-only client + folder materializer
→ focused integration and Shot repositories

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
- Optional external hierarchy uses exact Production, Season and Episode ids;
  it never substitutes an external Shot id for the local Shot identity.

Names, types, hierarchy depth, sibling order and visual position are never
substitutes for explicit identity.
