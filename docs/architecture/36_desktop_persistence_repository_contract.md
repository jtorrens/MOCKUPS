# Desktop Persistence Repository Contract

Status: normative.

This document governs the staged split of the desktop `SpikeDatabase` class
after field access has been delegated. It extends the strict startup,
migration and current-JSON rules in contracts 33 and 35 without changing the
persisted model.

## 1. Objective

Desktop persistence has one shared infrastructure boundary and focused table
owners:

```text
validated current database
→ shared SQLite project context
→ focused repository
→ SpikeDatabase compatibility facade
→ editor/tree/payload caller
```

The facade preserves the existing desktop API while repositories are extracted
in independently testable vertical slices. Extraction must not change table
shape, stored data, ids, Variant references, forwarding, temporal ownership,
payloads, Preview resolution or editor presentation.

## 2. Shared SQLite project context

`SqliteProjectContext` owns:

- the normalized database path;
- project-root configuration derived from that path;
- the ordinary foreign-key-enabled connection string;
- the foreign-key-enabled read-only validation connection string;
- creation and opening of both connection types;
- the process-wide write synchronization gate used by existing desktop writes.

`SqliteCommandExecutor` owns the reusable low-level command operations used by
the facade and repositories: parameter binding, non-query execution, scalar
reads, reader string handling and shared sort-order queries.

The validation connection must continue to use `SqliteOpenMode.ReadOnly`.
Constructing the context does not create, seed, migrate, normalize or repair a
database. `SpikeDatabase` still validates the current contract read-only before
normal editor operations are exposed.

## 3. Repository contracts

Repository interfaces and their DTOs are top-level persistence contracts, not
nested implementation details of `SpikeDatabase`. A repository:

- owns SQL and row mapping for its declared slice;
- opens connections through the shared context, or accepts the facade's
  already-open connection for one coordinated tree operation;
- validates JSON at the existing explicit write boundary;
- throws for missing rows, unknown fields or invalid current data;
- performs writes only for an explicit caller action;
- never runs startup repair, migration, normalization or compatibility logic.

The first extracted owners are:

- `EditorLayoutRepository`: load and explicit save of `editor_layouts`;
- `ProjectEpisodeRepository`: project/episode settings and field writes,
  project/episode tree rows, and explicit Episode lifecycle persistence;
- `RenderPresetRepository`: settings, options, tree rows, current creation
  defaults and explicit Render Preset lifecycle persistence.

Episode duplication continues the current aggregate behavior of copying its
Shots. It must preserve stable identities by generating new ids and must not
infer ownership from names, ordering or display text.

## 4. `SpikeDatabase` compatibility facade

`SpikeDatabase` remains the entrypoint used by the current editor while the
split is incomplete. It may:

- construct the shared context and repositories;
- run strict startup validation;
- delegate its existing public methods;
- coordinate a tree operation that spans repositories;
- construct `ProjectTreeNode` presentation objects;
- perform cross-domain Usage checks before delegation.

For an extracted slice it must not own:

- connection strings or connection construction;
- table SQL;
- row materialization;
- JSON serialization owned by that repository;
- table-specific defaults or lifecycle writes.

Existing callers must not need to choose a repository. This preserves behavior
while preventing the facade from becoming a service locator in the UI.

## 5. Tree and cross-domain boundaries

The navigation tree is an aggregate projection over several repositories. It
may request typed rows from repositories using one already-open connection so
the current load remains coordinated. Repositories return persistence records;
they do not create tree nodes, labels, cards, dialogs or navigation actions.

Usage discovery is cross-domain and remains outside an individual table
repository until a dedicated reference/usage service is extracted. The facade
checks Usage first, then asks the owning repository to delete. A repository
must not silently cascade around an explicit Usage prohibition.

Schema definition and strict whole-database validation also remain their own
boundaries. Moving table access to a repository does not move or weaken the
canonical schema or startup validator.

## 6. Transactions and write synchronization

The initial extraction preserves the current connection and locking semantics.
All repositories use the same process-wide write gate as the remaining facade
code. A later transaction coordinator may replace this mechanism only through
a separately reviewed phase with concurrency and rollback tests.

Do not create repository-local locks, alternate connection strings or a second
SQLite provider. Do not hide multi-step writes behind best-effort recovery.

## 7. Enforcement and tests

Architecture enforcement must verify:

- the read-only connection is owned by the shared context;
- the facade constructs and delegates to the extracted repositories;
- extracted facade partials contain no SQL or row mapping;
- the owning repository files exist and contain their declared table access;
- repair/migration routines are absent from every active Data-layer source,
  not only files named `SpikeDatabase*`.

Behavioral tests must compare facade and repository reads, exercise explicit
writes on disposable database copies and retain the byte-level read-only
startup proof. The committed database hash must not change during a pure
repository extraction.

## 8. Next extraction order

Later phases should continue with coherent slices rather than a bulk rename:

1. tree lifecycle dispatch and reference/Usage queries;
2. field and record-class persistence;
3. Theme and resource repositories;
4. Component/Module class and Variant repositories;
5. structured collections and Module Instances;
6. Preview payload and resolver data services.

The first part of item 6 is now established by contract 51:
`DesignPreviewPayloadDataSource` is the sole database/context dependency of
`DesignPreviewPayloadFactory`. Further resolver-data extractions must preserve
that typed boundary and proceed as separately validated slices.

Contract 52 establishes the next part: `ModuleInstanceTimelineDataSource`
supplies exact current timeline inputs while `ModuleInstanceTimeline` retains
all duration and owner-frame formulas without a database dependency.

Contract 53 establishes the Actor Preview slice: `ActorPreviewDataSource`
supplies exact current Actor context and raw preview values while the Actor
input/avatar factories retain mode, Palette, media and presentation semantics.

Contract 54 establishes the Production Shot context slice:
`ProductionShotContextDataSource` supplies the explicit owner Actor, Device,
Theme and mode route while `ProductionShotContextService` retains validity,
error and navigation policy.

Contract 55 establishes the Runtime Input options slice:
`RuntimeInputOptionsDataSource` supplies exact Actor, Palette and complete
Component preset options while generic factories retain `ValueKind` and
declared dynamic-list interpretation.

Contract 56 establishes the Preview visual-context slice:
`PreviewVisualContextDataSource` supplies exact Device/Theme options, Project
media root and common resolved Device metrics while the controller retains
session selection/orientation and the web Preview remains database-independent.

Contract 57 establishes the Production Preview session slice:
`ProductionPreviewSessionDataSource` supplies exact Shot/Screen/Variant values,
ordered Screen ids reuse `ModuleInstanceTimelineDataSource`, and the Preview
controller no longer retains a database handle.

Contract 58 establishes the isolated Component Preview input slice:
`ComponentPreviewInputDataSource` supplies exact Project fps, complete
Component Variant configs and effective runtime contracts while the Test Values
session and action interpreter retain transient input and action semantics.

Contract 59 establishes the Module Instance animation document slice:
`ModuleInstanceAnimationDocumentStore` composes the common timeline source and
delegates complete animation v2 writes while the editor retains target,
owner-frame and interaction semantics without a database handle.

Contract 60 establishes the Runtime Input owner document slice:
`RuntimeInputOwnerDocumentStore` supplies exact Component/Module/Screen owner
documents and delegates explicit isolated Design Preview writes while Runtime
Input, collection, Override and forwarding semantics remain in the editor.

Contract 61 establishes the Runtime Input instance document slice:
`RuntimeInputInstanceDocumentStore` delegates explicit stable scalar,
collection and complete animation writes while the editor retains ids,
mappings, interaction and document preparation without a database handle.

Contract 67 removes the remaining bespoke Status/Navigation item persistence
path. Both fixed item collections now use the generic Component Variant field
write, preserve complete item objects and reject invalid owner documents before
SQL. No index-based item facade API remains.

Each slice must leave the app usable, keep the facade API stable until its
callers are intentionally migrated, and add the relevant parity tests before
the next owner is moved.

## 9. Forbidden shortcuts

- adding new table SQL to an extracted `SpikeDatabase` partial;
- passing repository objects into `MainWindow` or an editor control;
- letting a repository build tree/UI models;
- using a repository read as a repair opportunity;
- changing table shape or parity data as an incidental part of extraction;
- duplicating connection factories, parameter binding or write gates;
- catching invalid current JSON and returning a plausible empty value;
- splitting every partial at once without a validated vertical slice.
