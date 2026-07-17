# Theme Persistence and Context Contract

Status: normative.

This document governs the Theme slice of the desktop repository split. It
extends contracts 32, 33, 35, 36 and 37 without changing the SQLite schema,
Theme token vocabulary, preview payload or editor presentation.

## 1. Objective

Theme persistence, Theme-document interpretation and Production context are
separate responsibilities:

```text
themes current rows
→ ThemeRepository
→ complete Theme settings / tokens_json
→ dictionary and Theme-token interpretation
→ payload / resolver

Module Instance id
→ explicit Shot production context
→ owner Actor
→ Actor default Theme
→ complete tokens_json
```

`SpikeDatabase` remains a compatibility facade for existing callers. It must
delegate Theme row access and Module Instance Theme-context lookup instead of
owning their SQL.

## 2. `ThemeRepository` ownership

`ThemeRepository` owns:

- `themes` row mapping;
- exact settings reads and coordinated tree queries;
- direct writes for family, Icon Theme and Status/Navigation Variant
  references;
- complete `tokens_json` writes;
- explicit create, duplicate, rename and delete persistence.

Stable Theme ids remain identity. Names and family values are editable data.
Status Bar and Navigation Bar values remain full concrete Component Variant
references; the repository never shortens or reconstructs them.

`SpikeDatabase.Themes.cs` and `SpikeDatabase.Tree.cs` must not contain Theme
table SQL or materialize Theme rows. Tree orchestration may gather explicitly
selected related values and ask the repository to perform one Theme lifecycle
write. Repositories return persistence records and never construct tree nodes,
cards, controls or dialogs.

## 3. Current Theme documents

`tokens_json` and `metadata_json` are required current JSON objects. Reads and
writes reject blank, malformed and wrong-root documents. A repository must not
supply `{}`, repair missing paths, translate retired tokens or normalize a
document merely because it was read.

Theme token field interpretation remains outside persistence. The dictionary
and common Theme catalogs continue to own:

- Light/Dark semantic color paths and alpha pairs;
- numeric token paths;
- motion timing and easing paths;
- typography, spacing, radii, shadow and icon-size field semantics;
- palette-token presentation and final palette lookup.

The repository stores the complete result of an explicit edit; it does not
resolve palette colors, fonts, assets, Device pixels or Preview atoms.

## 4. Explicit lifecycle

Theme creation constructs a complete current tokens document before insertion.
Duplication copies the selected Theme row into a new generated stable id.
Rename changes only the Theme name. Delete occurs only after the shared exact
Usage service has proved that no declared reference remains.

This extraction preserves the current Add Theme UI and its current creation
values. It does not make those values the target contract for future
scaffolding. Any redesign of Theme creation choices must be reviewed separately
and must not infer durable references from names, display order or tree
position.

## 5. Module Instance Theme context

Production Preview context is owned by the selected Shot. The target route is:

```text
Module Instance -> Shot -> owner Actor -> Actor default Theme
```

`ModuleInstanceThemeContextService` isolates this cross-domain lookup from the
Theme repository and the UI facade. It returns one complete current
`tokens_json` document or fails explicitly; it never returns a plausible empty
Theme document.

The committed parity data currently contains one Module Instance whose Shot has
no owner Actor. The previous implementation handled that incomplete context by
selecting the first project Theme ordered by name/id. This is a documented
temporary architecture exception, not a supported inference rule. It remains
isolated and marked with `TODO(editor-architecture)` only until an explicit data
migration assigns the intended owner Actor. New code must not copy, widen or
depend on this fallback.

Removing the exception requires one reviewed migration of seeds and the
committed parity database. Normal startup must not assign an Actor, choose a
Theme or repair the Shot.

## 6. Cross-domain queries

A Theme repository does not own queries whose primary purpose is a different
aggregate, such as recalculating Module Instance duration from its effective
runtime contract. Those queries move with their owning Module Instance or
timeline service in the corresponding repository phase. They must still use
exact stable references and strict current Theme documents.

No cross-domain query may become an alternative Theme write path.

## 7. Enforcement and tests

Architecture enforcement verifies:

- `IThemeRepository`, `ThemeRepository`,
  `IModuleInstanceThemeContextService` and its implementation remain explicit;
- `SpikeDatabase` constructs and delegates to both owners;
- the Theme facade and tree contain no Theme table lifecycle SQL;
- the repository owns Theme table access and does not import the UI shell;
- Module Instance Theme lookup cannot return `{}` for missing context;
- this contract is required by `AGENTS.md` and the architecture index.

Disposable-database tests compare facade/repository reads, exercise direct and
token writes, create/duplicate/rename/delete routing, reject invalid current
documents without a partial write, and prove missing Module Instance Theme
context fails explicitly. A pure extraction must leave the committed database
and assets byte-for-byte unchanged.

## 8. Forbidden shortcuts

- adding Theme SQL back to `SpikeDatabase.Themes.cs` or the tree;
- resolving palette hex, fonts, icons or Preview visuals inside the repository;
- reconstructing a Component Variant reference from class, name or position;
- returning `{}` when Theme context or `tokens_json` is missing;
- treating the temporary first-Theme fallback as normal Production behavior;
- assigning missing Shot Actors during startup or ordinary reads;
- changing seeds or the committed database as an incidental repository edit.
