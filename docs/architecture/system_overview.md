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

MOCKUPS owns the Production hierarchy and derives technical Shot names and
portable output routes from one Project-owned Production Output contract.

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
        ├── Project-owned Production Output
        │       ├── strict portable naming contract
        │       ├── stable Episode and Shot codes
        │       ├── workstation-local root
        │       └── validated folder materialization
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
- Every Shot owns a stable number. Its code, technical render name and portable
  route are derived from its Project and Episode contracts.

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
the shared worker and remains accessible independently of local-root
availability. The Project owns the portable relative route; the worker securely
creates its missing directories at job start before publishing the immutable
output.

## Layer ownership

### Physical .NET boundaries

Every extracted layer is a separate project. A source file cannot cross a
layer unless its project declares that exact `ProjectReference`; the evaluated
project graph is an executable repository contract. Repository-wide MSBuild
configuration disables transitive project compilation and marks package
compile assets as private. A consumer therefore receives neither a referenced
project's project references nor its package APIs: every compile capability
must be declared directly by the consuming project.

`Mockups.Domain` owns dependency-free current value objects and strict document
rules. It has no project or package references. The desktop application may
consume Domain, but Domain cannot see Avalonia, SQLite, Preview runtime or the
desktop assembly. Each later extraction must preserve that direction and add
its exact allowed edge to the project-boundary test.

`Mockups.Application` owns UI-independent application contracts, DTOs and the
window-session transition owner. `EditorWorkspaceCoordinator` consumes only
`IEditorNavigationDataSource` and publishes immutable `EditorSessionState`
snapshots and explicit effects for workspace, Production, navigation, editor
and Preview changes. Its public tree-loading surface is asynchronous: it runs
the synchronous data-source read on a controlled worker, cancels the prior
intent and commits only when the operation revision remains current. The
synchronous convenience methods are internal and cannot be called by Desktop.
`RuntimeInputDocumentContract` owns the UI- and storage-independent Runtime
document rules: explicit source ownership, collection storage-key selection,
stable-id reconciliation for projected collections and strict validation of
current scalar and collection payloads. SQLite composition consumes this
contract; it does not reimplement those semantics.
Application may reference Domain and has no package
capabilities. In particular, its project cannot compile a reference to
Avalonia or `Microsoft.Data.Sqlite`.
`EditorOperationCoordinator` is the session-owned execution boundary for
ordinary editor persistence. It serializes submitted operations, runs their
synchronous repository work on a controlled worker and cancels queued work
when the session closes. Visual controls apply successful results only after
the worker completes; UI effects never execute inside that worker.

`Mockups.Application.PersistencePorts` owns the synchronous persistence-facing
port contracts. It references Application models and Domain value objects but
has no package capabilities. An Application-only consumer cannot compile those
ports accidentally; a project must request that physical capability through an
exact direct reference.

`Mockups.Persistence.Sqlite.Core` owns the SQLite context, connection and
transaction primitives, cross-Project reference guard and current schema.
`Mockups.Persistence.Sqlite.Contracts` owns the internal focused repository
contracts shared by composition and their exact implementation owner.
`Mockups.Persistence.Sqlite.Design` owns App, Module, Component Class and
editor-layout repositories. `SqliteDesignOwner` is their only production
constructor; the aggregate can use only the owner's focused contract
properties. App configuration and metadata reads and edits already execute
inside this owner. Module definition reads, strict configuration projection,
field writes and Variant authoring execute there as well, including exact
Component Variant reference validation and session-only Default Variant
editing state. The temporary composition layer performs only the cross-owner
Production usage check before asking Design to delete a Module Variant.
Component Class settings, Variant catalogs and Variant lifecycle mutations
also execute in Design. The temporary composition layer performs the
cross-domain Usage check before asking Design to delete a Component Variant.
Component configuration-field writes, strict snapshot replacement and exact
embedded-slot reference validation execute in Design as well. Embedded
boundary writes for Component Classes, Component Variants, Modules and Module
Variants now execute in that same Design owner. Component reference lookup,
embedded Variant traversal, complete Preview base-config projection and
Variant Runtime Input projection also execute in Design. The temporary
composition layer resolves the field-option list when one dictionary field
combines Design-owned Component choices with Resource-owned palette or font
choices, then passes that data into Design. The composition-only
`ComponentFieldOptionResolver` is the single owner of that cross-owner
aggregation and receives only two option-specific contracts;
`SqliteProjectEngine` contains no option-selection policy.
Component Class, Variant,
inherited embedded and Runtime Override field-value projection now executes
in Design. Composition only routes the authored owner and supplies the
resolved option data; it does not calculate inheritance or traverse embedded
slots.
`Mockups.Persistence.Sqlite.Production` owns
Episode, Shot and Screen repositories. Its owner contains Project/Episode and
Production Output application operations; Shot settings, field writes and
render identity reads also execute there.
Screen settings, identity, transition projection, ordering, renaming and
effective Module Variant resolution execute in Production through the narrow
`IModuleVariantCatalog`; Production cannot reference or construct the Design
owner. Screen creation, Runtime payload mutation, exact Runtime field and
collection validation, Module Variant transitions, orphaned animation-track
cleanup and animation writes execute in the same owner. Composition supplies
only the valid Actor ids resolved from Resources when the Module's strict
Runtime document contract requires them. Production also owns calculated
Screen duration resolution and Shot duration synchronization; composition only
invokes that owner after a cross-owner operation that can affect the timeline.
`Mockups.Persistence.Sqlite.Resources` owns Actor, Device, Palette, Theme,
Production Font and Icon Theme repositories and their resource-specific field,
asset and token behavior. Resource behavior that needs Production context
receives its narrow contract; Resources still cannot see the Production
implementation. These owner projects can see
Contracts and Core, but cannot reference the composition assembly or one
another. The temporary `Mockups.Persistence.Sqlite` assembly owns composition,
validation and application operations not yet extracted; each remaining owner
moves out without adding a reverse reference.
`SqlitePersistence` returns a composition-only `SqliteProjectSession`; the
session has no data methods and each exposed port is a distinct adapter that
cannot be cast to an unrelated port. Persistence may reference Application and
Domain and only persistence projects may reference SQLite packages. They
cannot reference Avalonia or Desktop. Each SQLite context owns its write
coordination; opening an unrelated database never shares a process-global
write lock.

The session exposes child creation, node commands, Module Instance collections,
Icon Theme assets, Theme tokens, Component documents, Runtime Input owner and
instance writes, animation and Reference Usage as separate capabilities. The
desktop composition passes those capabilities independently; it has no
dialog-, header- or collection-wide persistence store. Actor Preview is backed
directly by the Resources owner. Component Preview and Variant History are
backed directly by the Design owner. Generic Preview input, Component Preview,
Module Instance timeline, Actor Preview and dictionary context are independent
ports; no one of them can be cast to another. Remaining cross-domain
application operations move to focused owners as their contracts are
decomposed. Retired area-wide adapters are deleted rather than retained as
unused forwarding code.
Generic Preview is a focused read composition over Production, Design and
Resources; the project engine does not implement its Application port.
Dictionary context is a separate Design/Resources composition and is not an
Application port implemented by the project engine.
The Desktop integration tests compare every session adapter's declared public
methods with its Application port, including inherited contracts. An adapter
cannot retain public sibling methods after its port is narrowed.

The Module Instance timeline is backed directly by Production and contains no
Theme access. Effective Module Instance Theme tokens are a separate Resources
query. Timeline calculations that need natural motion pacing receive both
ports explicitly at the application boundary.

Editor presentation context is backed directly by Resources because its exact
contract is Project, Theme and Production Font presentation data. The project
engine does not implement that application port.

Module Instance collection commands do not inherit timeline reads. The Shot
collection surface declares collection mutation, Production timeline and
Resources Theme-token capabilities independently.

Module Instance animation persistence is also backed directly by Production.
Runtime Input Instance writes do not inherit animation or timeline capability;
the Runtime collection editor receives payload writes, animation writes and
Theme-token reads as three distinct dependencies.

Cross-owner Runtime Input Instance mutation is implemented by the focused
`SqliteRuntimeInputInstanceStore`, not by the project engine contract. It
coordinates Production contract validation with the exact same-Project Actor
set from Resources while exposing only Runtime payload mutation capability.

Authored Runtime owner documents are backed directly by Design. The
`IRuntimeInputOwnerStore` contains no Screen or timeline reads; the Desktop
document adapter receives the Production timeline separately when the selected
node is a Module Instance.

Reference Usage is implemented by its focused SQLite query service. Node
commands do not inherit that query: delete coordination receives the command
store and Usage query as independent capabilities. The Node Command session
adapter contains only the members declared by its port and cannot recover
child, Module Instance or timeline operations by casting its target.

Render Queue is the sole Desktop consumer of the aggregate
`IRenderSnapshotDataSource`. The session names that capability
`RenderSnapshots`; there is no alias that presents the same broad read surface
as a generic Production-navigation dependency. Its SQLite membrane composes
the focused Preview, Actor, Component, timeline, Theme and Production Output
owners; the project engine is not the Render Snapshot target.

`Mockups.Desktop.Host` is the executable composition boundary and the only
production project allowed to reference both Desktop and Persistence.Sqlite.
It opens the current SQLite session and composes the named narrow Application
ports required by one desktop session.
The Host acquires one workstation-user visual-editor lease before Avalonia
startup. A second editor launch exits before constructing Avalonia, services
or SQLite and therefore never becomes another visual application process.
Its `ApplicationStartupCoordinator` validates the manifested Preview bundle
and opens the current database on a controlled worker. It also prepares the
first immutable navigation-tree snapshot before publishing the session, so
`MainWindow` performs no startup SQLite read. Startup returns one typed result;
only `Success` can create `MainWindow`. Missing or invalid inputs open a
recovery surface without constructing a partial editor session.
`Mockups.Desktop` declares its two allowed code dependencies directly:
Application for application ports and session coordination, and Domain for the
pure value objects used by visual adapters. `MainWindow`
receives an already composed session and cannot compile a reference to the
database context, SQLite packages or the Persistence assembly.

External raster and encoding processes are not editor instances and do not
acquire the visual-editor lease. They consume only the Render Queue's immutable
snapshot or generated frames and cannot open the Project database.

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
tree-load cancellation. Rapid workspace changes and window disposal invalidate
the in-flight read; a late result or late failure cannot replace current UI
state. Its state and transition tests compile without Avalonia or SQLite.

`MainWindow` owns window initialization, the three-panel shell, generic
editor-card composition, Preview host wiring, generic modal hosting and
application of coordinator transitions to visual controls. It does not retain
parallel mutable copies of workspace session state.
Window closing disposes both workspace coordination and the Preview controller;
it also cancels queued editor persistence. No tree read, field or lifecycle
write, playback preparation, preload, timer, frame reservation or external
rasterizer process may outlive that window.

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

Production Output card
→ Project-owned portable contract + workstation-local root
→ derived Shot plan
→ Render Queue folder materializer

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
