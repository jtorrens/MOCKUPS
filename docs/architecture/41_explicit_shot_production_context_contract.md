# Explicit Shot Production Context Contract

Status: normative.

This document governs the explicit Production-context boundary for Shots and
Module Instances and records the approved cleanup of disposable parity Shots.
It extends contracts 26, 29, 31, 32, 33 and 40.

## 1. Production context ownership

One Module Instance obtains its external Production context only through its
owning Shot:

```text
Module Instance
→ Shot
→ explicit owner Actor
→ Actor default Device and Theme
→ Theme mode, fonts, icons and final Preview values
```

The App, Module, Variant, tree position, display name and project ordering do
not supply an implicit Actor or Theme. Design Preview Test Values remain a
separate session-only fixture and cannot repair Production context.

## 2. Valid creation and editing states

Creating a Shot requires an explicit existing Actor from the same Project. The
creation dialog starts without a selected value, never proposes the first Actor
and writes nothing until the user selects one. SQLite stores `owner_actor_id` as
a non-null, no-default foreign key with restricted Actor deletion.

The owner Actor may be changed later to any explicitly selected Actor from the
same Project. There is no `None` option and the write boundary rejects blank,
missing or cross-Project ids. Changing the Actor updates Device/Theme Production
context and recalculates contract-owned durations where required, while Module
Variant references, runtime payloads, Overrides and keyframes remain unchanged.

Before adding the first Module Instance, the selected Actor must also reference
an existing default Theme. The add operation fails before writing anything when
this route is incomplete.

While a Shot owns Module Instances:

- its owner Actor cannot be empty;
- changing its owner requires the new Actor to have an explicit Theme;
- the owning Actor's default Theme cannot be empty or unresolved;
- Theme resolution never falls back to another Theme in the Project.

These rules use stable ids and exact references. They do not choose values from
names, Actor type, Theme family, row order or tree position.

## 3. Current parity data

The committed canonical project intentionally retains only:

- `episode_001 / shot_001` (`Shot 01 · Opening chat`);
- its two authored Module Instances, Lock Screen and Conversation.

`shot_002`, `shot_003`, `shot_004` and `shot_005` were disposable audit/test
records and were explicitly removed with their dependent Module Instances.
Episodes remain project data even when they are empty.

The committed database is not a scratch fixture. Automated lifecycle and
invalid-context tests must work on disposable database copies and remove their
temporary records before completing.

## 4. Migration workflow

The cleanup is an explicit parity-data migration:

1. clone the validated committed database;
2. enable foreign keys and delete every Shot except the exact retained id and
   Episode ownership pair;
3. let the declared `module_instances.shot_id` cascade remove dependent test
   Screens;
4. rebuild `shots` so `owner_actor_id` has no empty default and references
   `actors(id)` with restricted deletion;
5. prove foreign-key integrity and exact retained counts;
6. validate the migrated copy with the current read-only validator;
7. promote it to the committed parity artifact;
8. remove the first-project-Theme fallback from every active duration/context
   reader;
9. retain no migration routine in startup or repositories.

There is no separate active seed generator: the validated committed database
is the canonical current project source and explicit provisioning clones it as
defined by contract 33. Historical archived seed code is not updated or
reactivated.

## 5. Validation and enforcement

Current-database validation rejects every Module Instance whose Shot cannot
resolve the complete exact `Shot → Actor → Theme` route. Architecture checks
also verify the current parity database contains only the approved Shot and
that every retained Module Instance belongs to it.

Disposable integration tests cover:

- Shot creation only after explicitly selecting an Actor;
- successful Actor replacement without rewriting payload or animation;
- rejection when clearing a Shot owner or Actor Theme while Screens exist;
- explicit missing-context failure without `{}` or ordered Theme fallback;
- byte-for-byte read-only startup after migration.

Desktop navigation and Preview reads additionally follow contract 54. The
typed data source supplies only the exact stored Shot/Actor/Device/Theme route;
`ProductionShotContextService` remains the owner of invalid-context messages
and Screen navigation availability.

## 6. Forbidden shortcuts

- selecting the first Theme in a Project;
- deriving Actor or Theme from App, Module, Variant, name, type or position;
- creating or persisting a Shot without an Actor;
- permitting an ownerless Shot to contain Module Instances;
- clearing a required Actor/Theme reference and relying on next startup to
  notice it;
- reintroducing deleted test Shots into the committed parity database;
- using normal startup, reads or Preview resolution as a migration path;
- preserving a compatibility fallback after the parity data is current.
