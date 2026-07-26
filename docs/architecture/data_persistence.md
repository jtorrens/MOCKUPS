# Data and persistence

Status: normative.

## Database scope

The desktop application persists one complete Project workspace in SQLite.
Schema version `4` is the only current schema. Every row belongs directly or
indirectly to a Project and cross-Project lookup is invalid.

The current tables are:

| Domain | Tables | Ownership |
| --- | --- | --- |
| Workspace | `projects` | Root of all authored data |
| Production | `episodes`, `shots`, `module_instances` | Project → Episode → Shot → ordered Screen |
| Optional Shot Manager governance | `shot_manager_project_associations`, `shot_manager_episode_bindings`, `shot_manager_shot_structures` | exact Project/Season association → stable Episode bindings → last portable Shot render contract |
| Definitions | `apps`, `modules`, `component_classes` | Project-owned reusable definitions |
| Visual resources | `palette_colors`, `themes`, `icon_themes` | Project-owned semantic resources |
| Production resources | `actors`, `devices`, `production_fonts` | Project-owned Production Data |
| Editor description | `editor_layouts` | Project-owned layout metadata |

`shots.owner_actor_id` is required and uses a restricted foreign key.
`shots.shot_number` is a positive stable identity owned by MOCKUPS and unique
inside its Episode. It exists independently of Shot Manager association or the
date on which the Shot was created.
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

SQLite production code lives in the independent
`src/Mockups.Persistence.Sqlite` project. Its compile-time dependencies point
only to Application and Domain; UI packages are unavailable to that assembly.
`Mockups.Desktop.Host` is the only executable composition project allowed to
see both Desktop and Persistence.Sqlite. It opens the transitional
compatibility facade and projects it into the narrow ports declared by
Application. SQL packages and Persistence source files are unavailable to the
Desktop assembly. Project references are non-transitive and package compile
assets are private, so neither Desktop nor Host inherits SQLite APIs merely by
referencing Persistence. A project that intentionally spans layers, such as an
integration-test project, must declare every capability it compiles against.

Workspace coordination consumes `IEditorNavigationDataSource`; Preview,
dictionary, document, Usage, Render and Shot Manager consumers receive their
own read or write capability instead of the facade. Desktop controllers cannot
name `SpikeDatabase`, and neither `EditorWorkspaceCoordinator` nor its state
contract can compile a SQLite reference.

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
- `EditorLayoutRepository`
- `ShotManagerIntegrationRepository`

`SpikeDatabase` is a compatibility facade and orchestration boundary. New SQL,
connection construction, table mapping or write synchronization belongs in the
focused repository that owns the table.

Every `SqliteProjectContext` owns its own write gate. Focused repositories route
writes through that exact context, and a compound write holds the same gate for
the complete explicit transaction. No process-global SQLite write lock exists:
two different database files do not serialize one another, while concurrent
writes inside one context remain ordered.

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
  shot_manager_shot_structures.structure_json
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

`shot_manager_shot_structures.structure_json` is the last successfully
synchronized render plan for its local Shot. It is a strict portable object with
`schemaVersion`, unique `/`-separated relative `directories`, the exact
`shotOwnedDirectories` subset supplied by the file-layout owner, and complete
`entries` containing only `entryId` and `relativePath`. Its
`outputContracts` retain the exact output entry, relative directory, source
file-name prefix and version padding returned by Shot Manager. The Render
Queue selects by stable entry id but MOCKUPS owns its final
`Shot_Appearance_Version` output name. The portable snapshot never
persists a workstation root, resolved absolute path, discovery document or
credential.

## Local render state

Render Queue is deliberately outside SQLite. The application-data directory on
each workstation contains:

- the current local queue and compact references to immutable pending/active
  snapshots;
- a queue-owned local frame store containing content-addressed raster
  documents and assets plus one ordered manifest per Light/Dark child;
- terminal history, compacted after completion;
- the last selected output route per Project;
- the last successfully resolved absolute root per Shot Manager Production.

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

The Project association stores one exact external Production and Season.
Episode bindings store stable external identities. Render planning supplies
Shot Manager with the local stable Shot number and updates the portable
contract cache. The cache stores the returned technical identity but no
external Shot id because MOCKUPS owns that Shot. Missing physical destination
directories are never created by this persistence write.

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
