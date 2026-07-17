# Production Font Persistence Contract

Status: normative.

This document governs the Production Font slice of the staged desktop
repository extraction. It extends contracts 33, 35, 36 and 39 without changing
the persisted model, importer interaction, tree presentation or Preview.

## 1. Ownership boundary

Production Font persistence follows one explicit route:

```text
production_fonts current rows
→ ProductionFontRepository
→ SpikeDatabase compatibility facade
→ field service, tree or Preview payload caller
```

`ProductionFontRepository` owns all SQL and row materialization for
`production_fonts`, including exact lookup, coordinated tree reads, editable
field writes, imported-row upsert, rename and delete. It uses only the shared
`SqliteProjectContext` and `SqliteCommandExecutor` from contract 36.

`SpikeDatabase` keeps the existing public API while callers are migrated. Its
Production Font partial and tree orchestration must not retain table SQL or
reader mapping.

## 2. Persisted current records

Every row preserves its stable `id`, explicit `project_id`, family label,
category, source directory, required array `files_json` and required object
`metadata_json`. Reads reject malformed or wrong-root JSON. Writes validate a
complete files array before the first database mutation and never replace an
invalid document with `[]`, `{}` or a generated fallback.

An imported row is selected for update only by the existing exact
`project_id + family_name` persistence key used by the importer. A new row gets
a generated stable id and an explicit current metadata object. The repository
does not derive family, category, source paths, weights or styles from names or
file positions; it stores the already prepared import values it receives.

## 3. Filesystem and interpretation stay outside persistence

The repository does not read, copy, rename or delete font files. Those actions
remain in the explicit Production Font import/lifecycle workflow, which
coordinates them with the repository through the facade.

The following also remain outside repository ownership:

- discovering related font files;
- interpreting family, style and weight for the current importer;
- resolving the Project media root and safe asset path;
- constructing `ProductionFontFace` Preview values;
- formatting file summaries, tree labels or field controls;
- selecting fonts for a Theme creation policy.

This separation prevents SQLite persistence from becoming an asset importer or
a Preview resolver.

## 4. Lifecycle and Usage

Import, rename and delete are explicit user actions. The facade coordinates
asset work, the typed Usage guard and tree-node presentation; the repository
owns only the corresponding row write. Delete remains prohibited while the
exact reference projection from contract 38 reports a Usage edge.

No repository may infer Usage from substrings, labels, filenames, categories or
directory names. Stable ids are the only reference identity.

## 5. Validation

Automated enforcement verifies:

- the repository interface and implementation are explicit;
- `SpikeDatabase` constructs and delegates to the repository;
- Production Font facade and tree code contain no `production_fonts` SQL;
- filesystem and Preview types do not move into the repository;
- malformed imported `files_json` fails without a partial write;
- facade and repository reads and lifecycle writes agree on a disposable copy;
- opening and testing the committed database leave it byte-for-byte unchanged.

## 6. Forbidden shortcuts

- accessing `production_fonts` directly from the tree or editor shell;
- moving file copying, path safety or font-face interpretation into the
  repository;
- inferring a stable reference from family name, category, filename or order;
- accepting blank, malformed or wrong-root current JSON;
- repairing a font record while reading it;
- changing schema, parity rows or assets as an incidental extraction step;
- using startup or Preview resolution as an import or migration path.
