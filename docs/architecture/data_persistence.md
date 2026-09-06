# Data and persistence

Status: normative.

## Database scope

The desktop application persists one complete Project workspace in SQLite.
Schema version `16` is the only current schema. Every row belongs directly or
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
`shots.device_override_id` is one nullable restricted foreign key. `NULL`
inherits the required Actor default; a local value must resolve inside the Shot
Project. It is the only Device reference used by every Screen so canvas and
screen geometry remain stable for the complete Shot.
`module_instances.theme_override_id` is independently nullable and inherits the
Shot Actor default Theme. `module_instances.device_overrides_json` is a required
object containing sparse Screen-local values from the declared non-geometric
Device subset (`device.metrics.moduleTransparency.*`). Geometry, manufacturer,
model and OS cannot be overridden by a Screen. Every key is an exact current
dictionary field id and every value is its current scalar storage string;
Restore removes that key.
`module_instances.start_frame` is the signed Shot-frame origin of the Screen.
It may place a Screen before frame zero or beyond the Shot end.
`shots.shot_number` is a positive stable identity owned by MOCKUPS and unique
inside its Episode. `shots.slug` stores the explicit Shot Code, which is unique
inside its Episode and accepts letters, numbers, hyphen and underscore. It is
not an identifier and is never regenerated after creation. The Project stores
the manual portable Production Output naming and route contract plus its
explicit output mode. In Shot Managed mode the portable row stores the selected
external Production id/slugs and workstream/folder name/suffix; it never stores
the workstation's absolute `production.json` path or root. Episodes store an
`associated|free` state plus retained Production id, Episode id, order, slug
and the exact `episodePathSegments` array captured from Shot Manager. Order is
presentation metadata; managed route resolution uses only the captured path
segments. Shots store the same state plus retained Production id, Shot id and
canonical name. An associated Shot requires an associated owning Episode with
matching provenance. Changing the Episode association makes every child Shot
free while retaining its reference.
Manual naming segments are concatenated literally; optional Episode and Shot
prefixes generate initial codes only and may contain letters, numbers, hyphen
and underscore.
`shots.reference_video_json` is one required current Shot-owned object. It
stores a Project-relative or absolute video path, a nullable non-negative
Project-FPS In frame and stable video-relative markers with non-negative frames
and explicit text. `null` means the In has not been marked yet; integer zero is
a real In at video frame zero. An empty path represents no associated reference.
Relative parent traversal is invalid current data.
Definition references are also restricted: authored Production data must be
updated explicitly before its referenced definition can be removed.

`ProjectReferenceIntegrity` is the single cross-domain data guard for
Project-owned relational references. Focused repositories invoke it before
writes and startup validation invokes the same owner read-only. Actor Device
and Theme, Shot Actor and its optional Device override, Screen Theme override,
and Theme Icon Theme, Status Bar and
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
execute in that Design owner and are exposed through the focused
`IDesignRecordFieldStore` capability. Module definition reads,
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
persistence. `SqliteProductionOwner` also owns the authored Production Output
context; the aggregate delegates those operations and no longer constructs
Production repositories itself. Shot settings, portable manual render-name
resolution, external association fields and Shot field writes execute in
Production as well. Desktop composition resolves that context against either
the manual contract or its workstation-local, read-only Shot Manager document.
Screen reads, identity, transition projection, ordering, renaming and effective
Module Variant resolution execute there through `IModuleVariantCatalog`, a
read-only contract implemented by Design and declared in Contracts. Batch
module-name projection avoids per-Screen database reads. Production has no
project reference to Design and cannot access its repositories or authoring
operations. Screen creation, Runtime payload edits, collection identity
operations, Variant transitions and their animation cleanup also execute in
Production. Composition supplies the valid same-Project Actor id set required
by strict owner-specific Runtime documents; this does not grant Production a
Resources reference. Production owns each Screen's selected Module-allowed
duration policy, calculated Screen duration resolution and Shot duration
synchronization. Composition invokes that operation only after a cross-owner
write that can affect the timeline. A structured item's signed entry offset and
explicit presence duration remain child timing: neither changes the calculated
or explicit Screen duration. Preview and Render clip that child timing to the
independently resolved Screen interval.

Shot duplication is one Production-owned aggregate transaction. It persists
the new Shot and duplicates every ordered Screen through the same generic
Screen duplication contract, assigning new Shot and Screen identities while
preserving each Screen document exactly. The duplicate retains the authored
Shot context and local documents but clears every Shot Manager association
field so reassociation is always explicit. Failure to duplicate any Screen
rolls back the complete new Shot.

Every structured Runtime collection lifecycle operation is one generic
Application mutation addressed by a typed stable collection/item path. Add,
duplicate, move and delete use discriminated commands; insertion is expressed
by a stable `beforeItemId`, never by an ordinal persisted across a write. The
Application owner creates replacement ids, rebases only declared nested ids
and forwarding references, and derives the affected animation targets.
Production applies the command to cloned candidate `content_json` and
`animation_json`, validates both complete documents, and persists them in one
transaction together with derived duration synchronization. Desktop never
mutates a local collection first, fabricates id mappings or performs animation
cleanup as a separate commit; this rule is identical for top-level and nested
collections.

`module_instances.transition_json` is one complete current `Motion` document.
It uses the same strict transition, direction, bounds, fade, translate and
scale fields as reusable Component boundary Motion. A retired cut discriminator
or a partial Motion is invalid current data. Production owns its read and
write; the Shot timeline consumes the prepared value without reconstructing it
from a label or Screen position.
`module_instances.action_delay_frames` is the non-negative authored wait
between completion of the Screen's entry boundary and the start of its internal
timeline. Production owns the scalar write and resynchronizes the derived Shot
duration after either Motion or delay changes. `duration_frames` remains the
calculated or explicit action duration; it does not absorb transition or delay
frames.
`Mockups.Persistence.Sqlite.Resources` owns Palette,
Theme, Device, Actor, Production Font and Icon Theme persistence plus their
resource-specific field, token and asset operations.
The three owner assemblies reference Contracts and Core, never another owner
or the composition assembly. The compiler therefore rejects a
Design repository that tries to call a Production or Resources implementation,
and the same rule applies in every direction. `Mockups.Persistence.Sqlite`
composes those projects with read-only startup validation and explicit
cross-owner application stores. `SqliteProjectSessionFactory` constructs the
graph in local variables and publishes only the named session ports; there is
no universal project engine object or Application interface implementation.
UI packages are unavailable to every persistence assembly.
Persistence integration tests may compose raw owners through the test-only
`SqliteProjectTestContext`; that fixture is compiled in the test assembly and
is not a production capability or a session dependency.
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
Desktop Runtime Input and animation document stores additionally require the
session operation coordinator. Their mutation surface is task-returning and
captures mutable JSON values before queueing synchronous persistence work.
Embedded Runtime Override editing follows the same complete task boundary. It
updates a copied candidate, persists the exact stable collection-item field and
only then replaces the visible authored snapshot. A failed write retains the
last confirmed document.
Persistence keeps adapters only for ports exposed by the current session;
retired area-wide adapters are removed with their contracts.

Navigation Add behavior has one Application-owned declaration in
`EditorAddOperationCatalog`. It distinguishes authored record creation from
Device/Font import, Icon Theme discovery/refresh and bounded Screen selection.
An authored record is prepared through `PrepareRecordCreation`, validated as a
complete Dictionary field set and committed through `CreateRecord`; focused
repositories expose no parallel `AddChild`, `AddShot` or `AddTheme` route.
Records whose declared defaults are already complete may skip the form, but
they use the same prepare/validate/commit contract. Records requiring explicit
references cannot be inserted with empty placeholders for later editing.

The Node Command membrane exposes exactly `IEditorNodeCommandStore`; it has no
child creation, Module Instance, timeline or Reference Usage members and uses
no runtime casts to recover them.
Every public session capability is tested against its declared interface:
the concrete adapter may expose no additional public method. This applies
equally to Preview and Dictionary, whose retired sibling capabilities are not
present on their runtime adapter types.
Component documents, Component fields, Production record fields, Design record
fields, Resource record fields, core fields, Screen collections, child creation
and node commands are composed from the exact owners they require. Each record
field capability has a distinct adapter and cannot be cast to either sibling.
Component fields and Component documents likewise use distinct non-castable
adapters even though both route to the same Design-owned document store.
None is implemented by or recoverable through a universal persistence object.
`SqliteEditorNavigationStore` owns the complete read-only tree projection.
Navigation receives only that store's tree-loading function behind its exact
membrane; a consumer cannot cast the function or adapter back to the store.
All Component document and field overloads belong to
`SqliteComponentDocumentStore`; composition contains no parallel
Component document implementation.
Component reference catalogs, Runtime contract reads and reference validation
are consumed directly from `SqliteDesignOwner` through their focused session
adapters; composition exposes no mirror query surface.
Component Variant commands are owned by `SqliteEditorNodeCommandStore` and
reference details by `SqliteComponentDocumentStore`.
Module Variant fields and selection use the focused record store, lifecycle
commands use the node-command store, and effective Runtime reads use the
Production owner.
Module Instance Runtime writes belong to
`SqliteRuntimeInputInstanceStore`, collection lifecycle to
`SqliteModuleInstanceCollectionStore`, scalar fields to
`SqliteProductionRecordFieldStore`, and animation/read models to the Production
owner. The session composes one Runtime Input store instance.
Shot scalar writes and inherited Device projection remain Production-owned.
Screen scalar writes, inherited Theme projection and the Screen-local
non-geometric Device settings override document remain Production-owned. The sparse document
is exposed independently through `IRecordReferenceOverrideStore`; its session
adapter cannot be cast to the Production scalar-field adapter. The generic
editor supplies the metadata-declared owner, document id and field set, while
the focused persistence implementation alone maps that declared document to
the concrete Screen row and validates the Shot's effective Device. App and Module scalar fields belong to
`SqliteDesignRecordFieldStore`; Palette, Device, Actor, Theme, Icon Theme and
Production Font scalar fields belong to `SqliteResourceRecordFieldStore`.
Default database path discovery belongs to
`SqlitePersistence`. Read-only startup validation belongs
exclusively to `SqliteCurrentDatabaseValidator`, which receives the shared
context plus the three already constructed owners, opens its own validation
connection and rejects a missing, empty or non-current database without
repairing it. `SqliteProjectSessionFactory` invokes validation before it
publishes the session.

Preview reads are also separated by owner. Generic authored Preview input does
not inherit Actor, Component Preview or Module Instance timeline access, and
dictionary context does not inherit generic Preview. Payload preparation
declares every read capability it coordinates. Component Preview and Variant
history route directly to Design; Actor, Icon Theme and Theme-token reads route
directly to Resources.
Generic Preview composes Production Shot/Screen identity, Design authored
documents and Resources visual data explicitly.
Dictionary context likewise composes authored Component data from Design with
Theme, palette and Icon Theme data from Resources.

Editor presentation context also routes directly to Resources. It exposes only
Project, Theme and Production Font presentation reads.

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
`IModuleInstanceAnimationStore` owns only animation writes and does not inherit
timeline reads. Production owns the animation row and final write. The focused
SQLite adapter composes the effective Runtime declarations with the exact
same-Project Resources ids to validate animated record-reference keyframes,
then delegates the complete write to Production. Animation authoring declares
the Production timeline capability independently for its prepared snapshot.
The visual animation owner submits serialized semantic commands. A command is
applied to the latest confirmed document only when it reaches the front of the
queue; persistence success supplies the next confirmed snapshot and failure
leaves the prior snapshot current.

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
Output plan without creating a general persistence aggregate.

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
composition assembly retains only explicit cross-owner stores, adapters and
read-only validation. There is no internal universal engine, and no
composition object can be passed to Desktop as a store. New SQL, connection
construction, table mapping or write synchronization belongs in the focused
assembly and repository that own the table.

Every `SqliteProjectContext` owns its own write gate. Focused repositories route
writes through that exact context, and a compound write holds the same gate for
the complete explicit transaction. No process-global SQLite write lock exists:
two different database files do not serialize one another, while concurrent
writes inside one context remain ordered.

Desktop field commits, tree lifecycle commands, Screen collection mutations
and Icon Theme resource writes cross one session-owned asynchronous operation
boundary before reaching these synchronous repositories.
The boundary preserves submission order, performs the database work on a
controlled worker and is canceled during window shutdown. Repositories remain
synchronous transaction owners; they do not capture Avalonia controls or
dispatch UI effects.
Presentation reads required after Theme or Production Font commits also cross
that boundary; repository values are never requested by the visual callback.

The context also creates one immutable `IProjectPathResolver`. The workstation
database is `~/Library/Application Support/MOCKUPS/mockups.sqlite` on macOS,
while each Project's optional `media_root` is an absolute external directory
that may live anywhere available to that workstation. Application data never
owns, copies or synchronizes Project assets. The resolver travels explicitly
with the desktop session and resolves media-relative paths only against the
owning Project's absolute media root. Opening another database creates another
resolver; it cannot change the root observed by an already open context. There
is no process-global current Project root.

Repositories return current validated records. Interpretation, Variant
selection, forwarding, animation, context resolution, Preview preparation and
UI behavior stay outside persistence.

## Current document contract

Every JSON column has one required root kind. Blank, malformed or wrong-root
content fails explicitly. Readers and writers do not turn invalid documents
into plausible defaults.

`devices.metrics_json` additionally follows the exact Device document in
`resources_assets.md`. Startup validation and every focused Device read reject
missing, wrong-shaped or undeclared properties without repair.

The following inventory is machine-checked against schema validation:

```text
object
  projects.metadata_json
  episodes.metadata_json
  shots.canvas_json
  shots.reference_video_json
  shots.metadata_json
  apps.config_json
  apps.metadata_json
  modules.config_json
  modules.design_preview_json
  modules.metadata_json
  module_instances.device_overrides_json
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
  episodes.shot_manager_episode_path_segments_json
  production_fonts.files_json
```

The `projects` row stores the portable manual Production Output contract, its
explicit `manual` or `shot_manager` mode, and the captured Shot Manager
Production/destination values. Episode and Shot rows store their explicit
association state and retained reference values. Validation requires one exact
manual naming grammar and portable route template, a complete captured
Production/destination in Shot Managed mode, matching Production provenance,
and no associated Shot without its exact associated Episode. The resolved Shot
plan is derived data and is never cached in a second table.
For a managed Shot its relative directory is the exact captured Episode path
segments followed by the captured workstream and folder. Production, Season
and Episode slugs and Episode order never reconstruct that path.

## Local render state

Render Queue is deliberately outside SQLite. The application-data directory on
each workstation contains:

- the current local queue containing live Shot render plans and their output
  targets, never resolved Shot, Screen, frame or asset snapshots;
- terminal history, compacted after completion;
- the last selected output route per Project;
- the absolute Production Output root per stable Project id;
- the absolute Shot Manager `production.json` path and relocatable Production
  root per stable Project id;
- the workstation's pending enable/workstream setup state.

Launching a pending job resolves current authoring data and creates its
content-addressed frame documents and assets only in a unique temporary
directory. That transient preparation is deleted after completion, failure or
cancellation and is not part of local queue persistence.

The local Shot Manager document store accepts only an existing regular file
named exactly `production.json`. It is parsed strictly when connecting or
refreshing. Its containing directory initially supplies the absolute
Production root, which may later be relocated locally. Render resolution uses
the portable captured association values and therefore remains available when
the JSON is disconnected; offline state cannot create or refresh associations.

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

Manual render planning derives the technical identity and route from the
Project contract, Episode code and stable Shot number. Managed render planning
uses the captured canonical Shot name, folder suffix and exact Episode path
segments. Missing physical destination directories are never created by a
persistence write.

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
Editor layouts, the complete root or embedded field-value set and their
declared dictionary context are likewise resolved on the session operation
worker into a prepared immutable snapshot. The dictionary context follows
exact field metadata and active Variant references, rather than scanning or
loading every concrete owner. Production Screen authoring adds its exact
animation document, Screen origin and current duration to that prepared
snapshot. The visual card factory and animation controls consume those
snapshots and perform no layout, field-value, resource-option, active
Runtime-contract, animation or timeline persistence read during control
construction. Selection revision and exact owner checks prevent obsolete
preparations from reaching the visual state.
Production Screen header presentation and embedded breadcrumb Variant names
join the same prepared result. The visual header controller receives that
result and holds no repository or persistence-facing port.
Preview Setup separately prepares one immutable Project visual-context snapshot
containing Device options and metrics, Theme options and media root. Visual
refresh, playback setup and reference browsing consume that snapshot; replacing
it is revisioned and runs through the same session operation worker.
That operation also prepares the complete Production Preview session catalog:
Shot frame rates, ordered Screen lanes with signed starts, ranges and keyframes,
and exact Screen Variant configs. Each Shot entry includes its effective Device
and Actor context; each Screen entry includes its effective Theme and
non-geometric Device overrides. Timeline controls, context presentation and playback
consume the catalog and hold no timeline or Shot-context persistence data
source. Later tree reads remain candidates until this complete catalog and its
visual-context snapshot succeed. The candidate tree, catalog and selection are
then committed as one revision; a failed, canceled or obsolete preparation
leaves every prior snapshot current.
Production playback captures its request and creates every resolved payload
frame through the session operation worker. Frame iteration and runtime record
resolution do not execute from timer or visual callbacks.
Interactive Production refresh also captures one exact request and prepares its
resolved payload, renderability state and history label on that worker. The
visual controller commits only the preparation whose cancellation owner and
selection revision are both current. It does not repeat payload construction
when playback consumes the prepared frame list.
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

## Backup Hub boundary

The Desktop Host is the sole owner of the Backup Hub integration. It publishes
Backup Package v1 with `applicationId` `mockups`, snapshot format
`mockups-production`, the captured SQLite `PRAGMA user_version` as the opaque
schema version, and exactly one payload file: `payload/mockups.sqlite`. WAL and
SHM sidecars, workstation locks, the repository parity database, preferences
and Project assets are never payload.

Every package is built from the SQLite Backup API into a temporary directory
inside Backup Hub's canonical inbox, validated through the strict current
persistence contract plus `PRAGMA integrity_check`, flushed, and atomically
renamed to `<packageId>.bhpkg`. Manual, pre-migration and pre-restore backups
always publish. A normal application close is serialized after editor writes
and publishes only when the hash of its consistent SQLite snapshot differs
from the startup or last-published baseline. Rendering, Preview refresh,
navigation and other read-only activity therefore create no duplicate backup.

The Host consumes only Restore Handoff v2, before the application session opens
SQLite. It strictly validates the vault marker, request, manifest, exact
payload, hashes, current database contract and integrity before showing native
confirmation. A snapshot is restorable only when its declared and actual
schema equal the schema supported by that running application; backups from an
older or newer schema are rejected, while older backups made with the same
schema remain valid. Confirmation first publishes the mandatory `pre-restore`
package, then the Host performs a journaled atomic replacement, validates the
live database, publishes the terminal result and rolls back to the verified
previous file on any replacement or verification failure. Startup never
migrates, repairs or normalizes a restored database.

Vault discovery follows Vault Location v1 exclusively: native user application
data, `com.jtorrens.backup-hub/vault`, and the exact `vault-layout.json` marker.
MOCKUPS never creates the vault, searches alternate locations or writes a
fallback backup. Backup Hub owns encryption, retention, history and
synchronization after package ingestion.

## References and lifecycle

Reference discovery, `Used` state, Usage presentation and deletion protection
consume one typed edge set. Edges come from exact relational declarations and
owner-declared JSON paths. Text scanning, substring matching and arbitrary JSON
search are not reference discovery.

Lifecycle operations prepare and validate a complete write before committing.
A conversation message Actor is required independently from direction, so a
direction write changes only that scalar and never clears or manufactures the
Actor reference.

## Parity artifacts

Desktop behavior is delivered with the corresponding current artifacts in the
same revision:

- `data/mockups.sqlite`;
- affected files under `assets/FOQN_S2`;
- affected files under `assets/system/system_icons`.

`data/mockups.sqlite` is a versioned snapshot, never a second authoring
database. The workstation database under the operating-system application-data
root is the only canonical authoring database. `npm run
desktop:workstation:bootstrap` explicitly creates it from the repository
snapshot only when it does not yet exist; it never copies the external Project
asset roots, and startup itself remains read-only.

Every repository-writing task starts with `npm run desktop:update:begin`. It
atomically creates the shared workstation maintenance lock before checking for
open database handles, validates the canonical database, and captures its exact
bytes into `data/mockups.sqlite`. Desktop startup reads that lock and refuses to
open the Project for the complete update. Desktop and maintenance also acquire
opposing application/update markers before touching SQLite, closing the race
between a startup check and the first database open. `npm run desktop:db:snapshot` and
`npm run desktop:update:checkpoint` require the active lock and always copy in
the single canonical-to-snapshot direction.

When the schema or another persisted contract changes, the explicit
maintenance workflow migrates only the canonical workstation database. A final
checkpoint produces the repository snapshot from that migrated authority; the
snapshot is never migrated independently. `npm run desktop:update:end` validates
the canonical database, requires exact byte parity and only then removes the
lock. Bootstrap and normal startup never overwrite an existing workstation
database.

Desktop tests first validate the staged snapshot, then build their disposable
process-local source by creating required Production aggregates through the
real generic mutation owners. Tests never require a mutable authored Shot or
Screen to remain in the canonical Project. Destructive lifecycle tests copy
that prepared source again before mutation.

The complete repository gate also validates a disposable byte-for-byte copy of
the staged parity database. The copy path is supplied explicitly to every
database-backed test, scaffold verifier and architecture check. Validation
never swaps the worktree database. The maintenance lock keeps the canonical
workstation database frozen while the staged snapshot is validated.
The disposable workspace receives explicit Project asset roots, preserving
strict Project font, icon and media resolution without copying or mutating
those assets.
