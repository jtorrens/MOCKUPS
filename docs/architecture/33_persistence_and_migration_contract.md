# Persistence and Migration Contract

Status: normative.

This document governs the desktop SQLite database, its canonical schema, seed
data, the committed parity database, startup behavior, validation, explicit
migrations and SQLite runtime dependencies. If an older persistence note or
historical migration conflicts with this contract, this contract wins.

## 1. Objective

The application must operate on one explicit current data model. Opening the
application is a consumer operation, not a migration opportunity. Historical
data shapes may be understood only by a temporary, explicit migration tool and
must not survive in normal repositories, editors, resolvers, bridges or
renderers.

The target route is:

```text
canonical schema + current record-creation contracts + committed current DB
→ explicit validated clone or temporary one-shot migration
→ strict read-only validation
→ repositories and editors
→ resolvers
→ fully resolved preview
```

## 2. Ownership

- `SpikeDatabase.Schema.cs` owns the current SQL schema: tables, columns,
  constraints, indexes, defaults and `PRAGMA user_version`.
- Repository creation commands and component/module default factories own the
  current shape of records created by an explicit user action. They are not
  startup seeds and do not repair existing records.
- `data/desktop-editor-spike.sqlite` is the committed parity artifact used by
  the desktop application and the current canonical project database. It must
  exactly satisfy the current schema and contract validators; it is not a cache
  or a migration log.
- `CurrentDatabaseMaintenance` owns read-only validation and explicit validated
  copying of an already-current database. It does not understand retired data.
- A phase-specific temporary tool owns a declared one-shot migration and is
  removed in the same delivery that promotes its output.
- Repositories own current-model persistence only. They do not repair or infer
  retired data.
- Component and module contracts own their JSON payload shapes. Persistence
  stores those shapes but must not invent their semantics.

## 3. Existing-database startup

Opening an existing database is strictly read-only until current-contract
validation succeeds. Construction of `SpikeDatabase`, application startup and
standalone validation must not change any byte in the database file.

Normal startup may:

- open an existing database in SQLite read-only mode;
- inspect its schema version, tables, columns, constraints, indexes and
  defaults;
- parse and validate current JSON contracts;
- validate foreign keys, stable ids and complete references;
- fail with a precise message that points to the explicit maintenance workflow.

Normal startup must not:

- create or alter schema in an existing file;
- seed missing rows;
- run `Ensure*`, `Normalize*`, `Retire*`, repair or synchronization routines;
- rewrite JSON for formatting or default insertion;
- recalculate persisted durations or keyframes;
- translate legacy ids, short preset ids, aliases or retired property names;
- silently coerce an invalid current value into an accepted one.

The acceptance proof is byte-level, not merely logical: opening and validating
the same committed database repeatedly must leave its SHA-256 unchanged.

## 4. Creation is explicit

A missing or empty database must not cause normal application startup to create
and seed a new persistent database implicitly. Database provisioning is an
explicit maintenance operation with an explicit source and destination.

The current provisioning command is a validated clone:

```text
npm run desktop:db:create -- --source <current.sqlite> --output <new.sqlite>
```

It first validates the source read-only, copies it byte-for-byte without
overwriting an existing destination, and validates the output before reporting
success. It is intentionally not a partial seed generator.

Any future fresh-project/template generator must produce the current schema and
all required current contracts directly. It must not create an old schema and
then replay historical normalizers. Its output must pass the same strict
validator used for the committed database before it can replace a parity
artifact or be opened by the app.

## 5. Migration is explicit and temporary

Every persisted contract change follows one self-contained workflow:

1. Document the before/after shape and its owner.
2. Update the canonical current schema, contract and seed source.
3. Add a dedicated one-shot migration that accepts only the declared previous
   shape and writes only to an explicit destination or disposable copy.
4. Run the migration against a copy of the committed database.
5. Strictly validate schema, JSON, references, assets and row ownership.
6. Replace the committed parity database in the same change when validation
   succeeds.
7. Remove the temporary migration routine and all legacy readers in that same
   delivery.
8. Prove that normal startup is byte-for-byte read-only.

A permanent `--migrate-database` command that discovers and applies whatever
repairs happen to exist is forbidden. Migration routines must not be called by
constructors, normal startup, repository reads or validators.

## 6. Current-model strictness

After migration, all active code knows only the current contract.

The following are required:

- stable ids bind records, collection owners and animation tracks;
- component composition stores complete preset references as
  `componentClassId::preset::presetId`;
- module instances store explicit module Variant references;
- crossing a new component or module boundary selects an explicit default
  Variant, never an inferred one;
- runtime input forwarding and local Overrides remain explicit;
- animation uses v2 `fieldId`/`targetId` tracks with keyframes relative to their
  stable owner;
- JSON columns contain valid JSON of the current declared root kind;
- required references resolve without name-, type-, index- or position-based
  inference.

Short ids, retired property names, alternate JSON roots, aliases, hidden
defaults and catch-all parse fallbacks are invalid current data. If retained
data contains one, validation fails before an editor or resolver consumes it.

## 7. Schema and parity validation

Validation is read-only and must cover at least:

- exact canonical table set and `user_version`;
- column names, nullability, primary keys and declared defaults;
- required unique constraints, foreign keys and indexes;
- foreign-key integrity;
- JSON validity and expected current root shapes;
- full component preset and module Variant references;
- current animation schema and owner-relative track targets;
- referenced committed assets;
- canonical row ownership and required seed ids;
- absence of retired tables, columns, fields and identifiers.

Schema source and the committed database must agree physically, including SQL
defaults. Equivalent current rows do not excuse drift in `sqlite_master`.

Validation must never instantiate a write-enabled repair path. A command named
`validate` may report; it may not fix.

## 8. Authoring writes

Read-only startup does not make the application itself read-only. Once the
database has passed validation, explicit user edits may use write-enabled
repository operations.

Every write must be attributable to a user action or an explicit creation /
maintenance command. Derived values are persisted only when their contract
declares them authoritative. Opening a screen, changing selection, previewing a
frame or pressing Play must not rewrite payloads, durations or keyframes.

## 9. Dependency integrity

The SQLite provider and native SQLite bundle are part of the persistence
boundary. Their resolved versions must be visible in dependency checks, free of
known applicable high-severity advisories where a supported patched route
exists, and validated on every supported desktop runtime affected by a change.
Do not suppress a security warning or swap native providers without documenting
the runtime and packaging consequence.

The runtime validator rejects SQLite older than 3.50.2. At this phase the
desktop project resolves `Microsoft.Data.Sqlite` 10.0.10 and
`SQLitePCLRaw.bundle_e_sqlite3` 2.1.12; the loaded bundled engine reports
SQLite 3.53.3 on the validated Mac runtime.

## 10. Required checks for a persistence phase

Before the phase is complete:

```text
[ ] current schema source and committed DB physical schema match
[ ] committed DB passes strict read-only validation
[ ] two consecutive opens leave the database SHA-256 unchanged
[ ] invalid or legacy fixtures fail without being modified
[ ] JSON, full references, animation v2 and foreign keys validate
[ ] validated clone or migration output validates before replacement
[ ] temporary migration code and retired compatibility paths are removed
[ ] affected unit, architecture and desktop integration checks pass
[ ] the latest validated desktop build opens the committed database
[ ] parity database/assets are included in the same commit when changed
```

## 11. Forbidden shortcuts

- using application startup as a migration runner;
- treating an `Ensure*` routine as harmless validation;
- repairing a database because a validator noticed drift;
- keeping old and new JSON/property/id shapes readable indefinitely;
- inferring references from display names, types, list positions or hierarchy;
- changing only the committed database without changing its schema/seed owner;
- changing only schema/seed source without migrating the committed database;
- accepting logical row parity while SQL defaults or indexes differ;
- leaving a migration command in production after its one-shot delivery;
- allowing a repository, resolver, bridge or renderer to compensate for stale
  persistence.

## 12. Phase documentation rule

Each architecture cleanup phase that establishes a lasting boundary must add or
update a normative document and link it from `AGENTS.md` or this active
architecture index before implementation closes. The document records durable
ownership and invariants, not a temporary task diary. Subsequent phases extend
the contract instead of re-explaining exceptions in code.
