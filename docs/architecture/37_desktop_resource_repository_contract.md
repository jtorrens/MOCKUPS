# Desktop Resource Repository Contract

Status: normative.

This document governs the Palette, Device and Actor persistence slice of the
desktop repository split. It extends contracts 33, 35 and 36. It does not
change the resource model, field vocabulary, tree presentation or Preview.

## 1. Objective

Resource persistence and resource interpretation are separate boundaries:

```text
Palette / Device / Actor current rows
→ focused repository
→ persisted settings and records
→ common/domain interpretation
→ field service, resolver or Preview caller
```

The repository stores and retrieves the current declared contract. It does not
decide how a Device becomes a Preview frame or how Actor wallpaper/avatar data
is visually resolved.

## 2. Repository ownership

The extracted owners are:

- `PaletteRepository`: `palette_colors` row mapping, settings, explicit field
  writes, token/value maps and lifecycle persistence;
- `DeviceRepository`: `devices` row mapping, settings, metrics-document writes,
  options and lifecycle persistence;
- `ActorRepository`: `actors` row mapping, settings, metadata-document writes,
  options and lifecycle persistence.

Each repository uses `SqliteProjectContext` and `SqliteCommandExecutor` from
contract 36. It accepts an already-open connection only when `SpikeDatabase`
is coordinating one tree operation.

For these tables, `SpikeDatabase` partials and `Tree` must not retain:

- SQL statements;
- row-reader materialization;
- current-record creation JSON;
- duplicate/delete/rename table writes;
- table-specific metadata updates.

The facade retains its existing public methods and delegates so editor callers
do not gain repository knowledge.

## 3. Persisted contracts

Resource settings and tree records are top-level persistence DTOs. Stable ids
remain the identity of every resource; token, display name, manufacturer,
model, short name and other labels are editable data, never identity.

Palette token/value maps preserve exact token keys. They must not infer token
families from spelling. Actor Device/Theme references remain explicit stored
ids. No repository may select a Theme, Device, Palette token, wallpaper or
avatar from a name, type, index or tree position.

Device `metrics_json` and Actor `metadata_json` are required current JSON
objects. Repository writes use the existing explicit field-to-path mapping and
reject unknown field ids. They never synthesize a missing persisted document.

## 4. Domain interpretation stays outside persistence

The following do not belong to a repository:

- converting Device metrics to `DevicePreviewMetrics`;
- Preview canvas/screen geometry calculations;
- resolving Actor wallpaper, avatar, palette or mode behavior;
- field-control construction or editor grouping;
- Theme, token, asset or component resolution.

`DeviceMetricRules`, `JsonPath` and the existing field-value services remain
the shared/domain route. During the compatibility-facade stage,
`SpikeDatabase.GetDevicePreviewMetrics`, `GetDeviceMetricFieldValue` and
`GetActorFieldValue` may compose repository settings with those shared rules,
but they contain no table access.

## 5. Explicit lifecycle

Add, imported add, duplicate, rename and delete remain explicit user actions.
The tree coordinates the action and constructs its `ProjectTreeNode`; the
repository owns the corresponding table write and returns a persistence record.

Creation factories construct the complete current JSON documents directly:

- a new Device receives the declared current metrics object;
- an imported Device receives the metrics object created from its explicit
  import draft;
- a new Actor receives the declared current avatar/wallpaper metadata object;
- a new Palette color receives explicit stable id, token, color and metadata.

Duplication copies the selected stable row into a new generated id. Rename
changes only the declared label/token field. Delete occurs only after the
cross-domain Usage boundary has allowed it.

## 6. Usage is not schema guessing

The current broad Usage fallback scans text columns with substring matching and
classifies some sources from display strings. That behavior is not a valid
future contract and must not be moved into these repositories or treated as
resource ownership.

The replacement Usage system must use explicit reference declarations and
stable ids. Relational references may come from declared foreign keys; JSON
references require owner-declared paths/contracts. It must not infer a
reference from a matching name, text-column type, substring, source label or
table position. Replacing that cross-domain behavior is a separate reviewed
phase because it changes deletion evidence and navigation targets.

## 7. Enforcement and tests

Architecture enforcement verifies:

- the three repository contracts and implementations exist;
- `SpikeDatabase` constructs and delegates to them;
- their facade partials contain no SQL/row readers;
- `SpikeDatabase.Tree.cs` contains no direct SQL for the three owned tables;
- the repositories do not import `MainWindow` or editor controls;
- the broad Usage scanner is not copied into a resource repository.

Disposable-database tests cover facade/repository read parity, explicit field
writes, create/duplicate/rename/delete routing and rejection of invalid current
JSON without a partial write. The committed database and assets remain
byte-for-byte unchanged by this extraction.

Actor Preview reads additionally follow contract 53. The typed Preview data
source composes current facade/domain reads; Runtime Actor and inline avatar
factories own interpretation and must not bypass that source or move visual
rules into `ActorRepository`.

Preview Device/Theme option and metrics reads additionally follow contract 56.
`PreviewVisualContextDataSource` supplies the shell with current options,
Project media root and common resolved metrics; `DeviceRepository` does not
gain Preview geometry or selector policy.

## 8. Forbidden shortcuts

- resolving Device or Actor visuals inside a repository;
- returning UI controls or tree nodes from a repository;
- treating Palette token text as record identity;
- inferring Actor defaults from matching field names or record types;
- moving broad `LIKE`/text-column Usage scanning into a repository;
- adding a second metrics/avatar/wallpaper JSON path implementation;
- changing seeded resources or committed parity data during extraction;
- adding compatibility defaults for incomplete current JSON.
