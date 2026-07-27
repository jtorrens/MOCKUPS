# Data and persistence

Status: normative.

## Database scope

The desktop application persists one complete Project workspace in SQLite.
Schema version `5` is the only current schema. Every row belongs directly or
indirectly to a Project and cross-Project lookup is invalid.

The current tables are:

| Domain | Tables | Ownership |
| --- | --- | --- |
| Workspace | `projects` | Root of all authored data |
| Production | `episodes`, `shots`, `module_instances` | Project → Episode → Shot → ordered Screen |
| Definitions | `apps`, `modules`, `component_classes` | Project-owned reusable definitions |
| Visual resources | `palette_colors`, `themes`, `icon_themes` | Project-owned semantic resources |
| Production resources | `actors`, `devices`, `production_fonts` | Project-owned Production Data |
| Editor description | `editor_layouts` | Project-owned layout metadata |

`shots.owner_actor_id` is required and uses a restricted foreign key.
`shots.shot_number` is a positive stable identity owned by MOCKUPS and unique
inside its Episode. `shots.slug` stores its generated Shot code. The Project
stores the portable Production Output naming and route contract.
Definition references are also restricted: authored Production data must be
updated explicitly before its referenced definition can be removed.

`ProjectReferenceIntegrity` is the single cross-domain data guard for
Project-owned relational references. Focused repositories invoke it before
writes and startup validation invokes the same owner read-only. Actor Device
and Theme, Shot Actor, and Theme Icon Theme, Status Bar and
Navigation Bar references must resolve inside the owner's exact Project.
Status and Navigation references additionally require a complete existing
Component Variant of their exact declared type.

## Repository ownership

Synchronous persistence-facing ports live in the separate package-free
`Mockups.Application.PersistencePorts` assembly. This keeps storage capability
out of the base Application contract graph and makes every current consumer
declare that capability directly.

SQLite production code is split across independent persistence projects.
`Mockups.Persistence.Sqlite.Core` owns connection construction, the per-context
write gate, command execution, cross-Project reference integrity and the
current schema. `Mockups.Persistence.Sqlite.Contracts` owns only internal
focused repository contracts. `Mockups.Persistence.Sqlite.Design` owns App,
Module, Component Class and editor-layout persistence. `SqliteDesignOwner`
constructs its definition repositories; composition no longer constructs
those implementations directly. App configuration and metadata operations
execute in that Design owner and are exposed through temporary delegations
while the broad application ports are decomposed. Module definition reads,
strict configuration projection, field writes, Variant mutation and
session-only Default Variant editing state follow the same owner. Component
Variant references written into Module configuration are validated inside
Design against the exact Project, class type and stable Variant id. Deleting a
Module Variant is split deliberately: composition queries its Production
usage, while Design alone validates and mutates the authored Variant array.
Component Class settings, Design Preview documents and Variant lifecycle
mutations are owned by the same Design assembly. Component Variant deletion
uses the same split: composition queries typed cross-domain Usage edges, then
Design alone validates protection/lock state and mutates the authored Variant
array. This does not grant Design access to the Usage implementation.
Component configuration-field writes and complete Variant snapshot
replacement also execute inside Design. Before persistence, Design validates
the current Component contract and every embedded slot against the exact
Project, Component type, class id and Variant id.
Local Override writes at embedded boundaries also execute inside Design,
including boundaries authored by a Module or Module Variant. The owner updates
only the exact local Override document and preserves the selected full Variant
reference. Component reference lookup, embedded Variant traversal, complete
Preview base-config projection and Component Variant Runtime Input projection
are read models of the same Design owner. Composition does not reconstruct
those read models. Basic Component Class and Variant field values are also
projected in Design from an explicit option list. Composition supplies that
data list only because a dictionary field may combine Design-owned Component
choices with Resource-owned palette or font choices; Design receives no
Resource service. `ComponentFieldOptionResolver` is the one composition-only
owner of that aggregation and receives two narrow option-only contracts, not
the concrete Design or Resources owners.
Inherited embedded and Runtime Override field projection,
including local inheritance removal and nested slot traversal, also executes
inside Design. Composition routes the authored owner and supplies resolved
option data only.
`Mockups.Persistence.Sqlite.Production` owns Project/Episode, Shot and Screen
persistence. `SqliteProductionOwner` also owns the derived Production Output
operations; the aggregate delegates those operations and no
longer constructs Production repositories itself. Shot settings and portable
render-name resolution and Shot field writes execute in Production as well.
Screen reads, identity, transition projection, ordering, renaming and effective
Module Variant resolution execute there through `IModuleVariantCatalog`, a
read-only contract implemented by Design and declared in Contracts. Batch
module-name projection avoids per-Screen database reads. Production has no
project reference to Design and cannot access its repositories or authoring
operations. Screen creation, Runtime payload edits, collection identity
operations, Variant transitions and their animation cleanup also execute in
Production. Composition supplies the valid same-Project Actor id set required
by strict owner-specific Runtime documents; this does not grant Production a
Resources reference. Production owns calculated Screen duration resolution
and Shot duration synchronization. Composition invokes that operation only
after a cross-owner write that can affect the timeline.
`Mockups.Persistence.Sqlite.Resources` owns Palette,
Theme, Device, Actor, Production Font and Icon Theme persistence plus their
resource-specific field, token and asset operations.
The three owner assemblies reference Contracts and Core, never another owner
or the temporary composition assembly. The compiler therefore rejects a
Design repository that tries to call a Production or Resources implementation,
and the same rule applies in every direction. `Mockups.Persistence.Sqlite`
composes those projects with read-only startup validation and explicit
cross-owner application stores. Its internal `SqliteProjectEngine` implements
no Application interface. UI packages are unavailable to every persistence
assembly.
`Mockups.Desktop.Host` is the only executable composition project allowed to
see both Desktop and Persistence.Sqlite. It opens a composition-only
`SqliteProjectSession` and passes its named ports into Desktop. SQL packages
and Persistence source files are unavailable to the
Desktop assembly. Project references are non-transitive and package compile
assets are private, so neither Desktop nor Host inherits SQLite APIs merely by
referencing Persistence. A project that intentionally spans layers, such as an
integration-test project, must declare every capability it compiles against.

Session ports are capability membranes, not UI-area facades. Child creation,
node commands, Module Instance collections, Icon Theme assets, Theme tokens,
Runtime Input owner writes, Runtime Input instance writes, animation and
Reference Usage use distinct adapter instances. A Desktop controller that
needs more than one capability declares each one in its constructor.
Persistence keeps adapters only for ports exposed by the current session;
retired area-wide adapters are removed with their contracts.
The Node Command membrane exposes exactly `IEditorNodeCommandStore`; it has no
child creation, Module Instance, timeline or Reference Usage members and uses
no runtime casts to recover them.
Every public session capability is tested against its declared interface:
the concrete adapter may expose no additional public method. This applies
equally to Preview and Dictionary, whose retired sibling capabilities are not
present on their runtime adapter types.
Component documents, Component fields, record fields, core fields, Screen
collections, child creation and node commands are composed from the exact
Design, Production, Resources, Usage and context owners they require. None is
implemented by or recoverable through `SqliteProjectEngine`.
`SqliteEditorNavigationStore` owns the complete read-only tree projection.
Navigation receives only that store's tree-loading function behind its exact
membrane; a consumer cannot cast the function or adapter back to either the
store or the engine. `SqliteProjectEngine` contains no tree read or node
command method and no Design, Production or Resources owner pass-through
method.
All Component document and field overloads belong to
`SqliteComponentDocumentStore`; the project engine contains no parallel
Component document implementation.
Component reference catalogs, Runtime contract reads and reference validation
are consumed directly from `SqliteDesignOwner` through their focused session
adapters; the project engine exposes no mirror query surface.
Component Variant commands are owned by `SqliteEditorNodeCommandStore` and
reference details by `SqliteComponentDocumentStore`; the project engine has no
parallel Variant command implementation.

Preview reads are also separated by owner. Generic authored Preview input does
not inherit Actor, Component Preview or Module Instance timeline access, and
dictionary context does not inherit generic Preview. Payload preparation
declares every read capability it coordinates. Component Preview and Variant
history route directly to Design; Actor, Icon Theme and Theme-token reads route
directly to Resources.
Generic Preview composes Production Shot/Screen identity, Design authored
documents and Resources visual data explicitly; `SqliteProjectEngine` does not
implement the Preview input port.
Dictionary context likewise composes authored Component data from Design with
Theme, palette and Icon Theme data from Resources; the project engine does not
implement the Dictionary port.

Editor presentation context also routes directly to Resources. It exposes only
Project, Theme and Production Font presentation reads and does not pass through
the project engine.

Module Instance row, slot, contract and Runtime Preview reads route directly to
Production. Effective Theme-token resolution is not part of that timeline
contract: it is a separate Resources capability composed only by consumers
that calculate Theme-dependent timing.

`IModuleInstanceCollectionStore` owns only Screen collection mutations and
selection options. It does not inherit timeline reads; the Shot collection
editor receives collection, timeline and Theme-token ports independently.

`IRuntimeInputInstanceStore` owns only explicit Runtime payload mutations. It
does not inherit `IModuleInstanceAnimationStore`; animation is an independent
Production-owned port. Consumers that reconcile animation after a stable-id
payload edit must declare both capabilities.

The SQLite implementation of Runtime Input Instance writes is a focused
cross-owner application store. It receives the SQLite context plus Design,
Production and Resources owners for exact contract and Actor validation; it
does not expose any of those owners or their unrelated operations to Desktop.

`IRuntimeInputOwnerStore` is Design-only. Module Instance Variant and effective
Runtime Preview reads remain on the Production timeline and are composed with
the authored owner store only in the Desktop document adapter.

`IReferenceUsageQuery` is backed by the owner-declared Usage index service.
`IEditorNodeCommandStore` has no Usage read capability; workflows that guard a
delete declare both ports explicitly.

Render Queue receives the explicit aggregate `IRenderSnapshotDataSource`
because creating its immutable job snapshot requires the prepared Preview,
Actor, Component, timeline and Theme reads declared by that contract. It is
not presented as a general Production-navigation store.
The SQLite implementation composes those exact owner ports plus the Production
Output plan; `SqliteProjectEngine` does not implement Render Snapshot or
timeline merely to satisfy Render Queue.

Workspace coordination consumes `IEditorNavigationDataSource`; Preview,
dictionary, document, Usage and Render consumers receive their
own read or write capability. The session itself contains no data methods, and
each port is backed by a different adapter object that implements only that
port and its declared inherited capabilities. Neither Desktop controllers,
`EditorWorkspaceCoordinator` nor its state contract can compile a SQLite
reference.

Runtime payload ownership and shape are validated through the
storage-independent `RuntimeInputDocumentContract` in Application. Persistence
may reconcile or persist a Screen payload, but it does not own source
classification, collection-key semantics or stable-id projection rules.

`ActorPreview` is composed directly from `SqliteResourceOwner`. Its Production
theme-context validation arrives through an internal contract declared outside
the Production implementation, so Resources cannot call Production code.
The temporary aggregate delegates resource operations to that owner while
older cross-domain ports are separated incrementally.

Focused repositories own table SQL, row mapping and prepared complete writes:

- `ProjectEpisodeRepository`
- `ShotRepository`
- `ModuleInstanceRepository`
- `AppModuleRepository`
- `ComponentClassRepository`
- `PaletteRepository`
- `ThemeRepository`
- `DeviceRepository`
- `ActorRepository`
- `ProductionFontRepository`
- `IconThemeRepository`
- `SqliteEditorLayoutStore`

There is no universal persistence facade. `SqlitePersistence` validates one
current database and returns its session descriptor. `EditorLayouts` is backed
directly by `Mockups.Persistence.Sqlite.Design`. Repository implementations
already live in their Design, Production or Resources owner. The internal
engine remains a composition and validation helper during incremental source
movement, but it implements zero Application interfaces and cannot be passed
to Desktop as a store. New SQL, connection construction, table mapping or
write synchronization belongs in the focused assembly and repository that
own the table.

Every `SqliteProjectContext` owns its own write gate. Focused repositories route
writes through that exact context, and a compound write holds the same gate for
the complete explicit transaction. No process-global SQLite write lock exists:
two different database files do not serialize one another, while concurrent
writes inside one context remain ordered.

Desktop field commits and tree lifecycle commands cross one session-owned
asynchronous operation boundary before reaching these synchronous repositories.
The boundary preserves submission order, performs the database work on a
controlled worker and is canceled during window shutdown. Repositories remain
synchronous transaction owners; they do not capture Avalonia controls or
dispatch UI effects.

The context also creates one immutable `IProjectPathResolver` from its database
location. That resolver travels explicitly with the desktop session and is the
only authority for Project-relative media and asset paths. Opening another
database creates another resolver; it cannot change the root observed by an
already open context. There is no process-global current Project root.

Repositories return current validated records. Interpretation, Variant
selection, forwarding, animation, context resolution, Preview preparation and
UI behavior stay outside persistence.

## Current document contract

Every JSON column has one required root kind. Blank, malformed or wrong-root
content fails explicitly. Readers and writers do not turn invalid documents
into plausible defaults.

The following inventory is machine-checked against schema validation:

```text
object
  projects.metadata_json
  episodes.metadata_json
  shots.canvas_json
  shots.metadata_json
  apps.config_json
  apps.metadata_json
  modules.config_json
  modules.design_preview_json
  modules.metadata_json
  module_instances.transition_json
  module_instances.content_json
  module_instances.behavior_json
  module_instances.animation_json
  module_instances.metadata_json
  palette_colors.metadata_json
  devices.metrics_json
  actors.metadata_json
  production_fonts.metadata_json
  icon_themes.mapping_json
  icon_themes.metadata_json
  component_classes.config_json
  component_classes.design_preview_json
  component_classes.metadata_json
  themes.tokens_json
  themes.metadata_json
  editor_layouts.layout_json
array
  production_fonts.files_json
```

The `projects` row stores the portable Production Output contract. Validation
requires one exact naming grammar and one portable relative route template.
The resolved Shot plan is derived data and is never cached in a second table.

## Local render state

Render Queue is deliberately outside SQLite. The application-data directory on
each workstation contains:

- the current local queue and compact references to immutable pending/active
  snapshots;
- a queue-owned local frame store containing content-addressed raster
  documents and assets plus one ordered manifest per Light/Dark child;
- terminal history, compacted after completion;
- the last selected output route per Project;
- the absolute Production Output root per stable Project id.

Snapshot preparation writes one resolved frame document at a time and never
retains the complete Shot frame set in memory. Fonts, media and repeated frame
documents are written once by content hash and may be shared by both children
of a batch. The queue JSON does not embed their binary payloads.

A worker never reopens the Project database to execute an existing job. It
streams the ordered manifest, reads only the current document and registers
each referenced asset once in its persistent raster process. The absolute
production root and local snapshot-store paths are workstation state and are
never copied into the portable Project. Interrupted active jobs with a complete
snapshot return to Pending on the next application start; incomplete
preparation fails explicitly and its orphaned local files are removed.

Render planning derives the technical identity and route from the Project
contract, Episode code and stable Shot number. Missing physical destination
directories are never created by a persistence write.

Component and Module Variant arrays are required current data. Every Variant is
a complete named snapshot with:

- an explicit stable id;
- `protected`;
- `locked`;
- an object `config`.

A missing or malformed Variant or config is an error. Creating a Variant may
construct a new complete snapshot; reading or editing current data never
repairs one implicitly.

## Startup and migrations

Opening an existing database, constructing repositories and validating the
schema and documents is read-only. Application startup never creates,
normalizes, repairs, retires or synchronizes schema or data.

The executable Host performs this validation outside the UI thread and reports
missing and invalid databases as typed startup results. `MainWindow` is created
only after the current database, Preview bundle and initial immutable tree
snapshot validate. Later tree refreshes execute the synchronous repository read
on the Application coordinator's controlled worker. Revision checks discard a
result when selection, workspace intent or shutdown has made it obsolete.
Cancellation or failure cannot publish a partial desktop session.

A schema, vocabulary, field or identifier change requires one explicit
maintenance migration:

1. update the canonical schema and seeds;
2. convert every affected current record;
3. update the committed parity database and required assets;
4. validate the complete result;
5. remove temporary migration code in the same revision.

Normal readers know only the resulting current contract. They contain no
aliases, coercions, fallback fields or startup repair paths.

## References and lifecycle

Reference discovery, `Used` state, Usage presentation and deletion protection
consume one typed edge set. Edges come from exact relational declarations and
owner-declared JSON paths. Text scanning, substring matching and arbitrary JSON
search are not reference discovery.

Lifecycle operations prepare and validate a complete write before committing.
Changes that affect several fields, such as changing a conversation message
direction and Actor ownership, are atomic.

## Parity artifacts

Desktop behavior is delivered with the corresponding current artifacts in the
same revision:

- `data/desktop-editor-spike.sqlite`;
- affected files under `assets/FOQN_S2`;
- affected files under `assets/system/system_icons`.

Tests that exercise destructive lifecycle behavior use disposable database
copies. The committed Project keeps its intentional authoring content.

The complete repository gate also validates a disposable byte-for-byte copy of
the staged parity database. The copy path is supplied explicitly to every
database-backed test, scaffold verifier and architecture check. Validation
never swaps the worktree database, so an active workstation database may retain
its local authoring state without affecting or being overwritten by the gate.
The disposable workspace links the repository asset root at the same relative
boundary, preserving strict Project font, icon and media resolution without
copying or mutating those assets.
