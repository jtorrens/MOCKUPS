# Editor Presentation Context Data Boundary Contract

Status: normative.

This document governs the small persisted-data reads used by shared editor
presentation services after field access has already been delegated. It extends
contracts 21, 36, 40, 42 and 45 without changing fields, filesystem behavior,
tree structure or persistence.

## 1. Objective

Generic editor presentation and current record lookup have separate owners:

```text
validated current Project, Theme and Production Font data
→ EditorPresentationContextDataSource
→ exact media root, Theme navigation values or font files document
→ EditorPathBrowser / EditorFieldPostCommitEffects
→ picker context or in-memory tree subtitle refresh
```

The data source supplies raw current values. Consumers retain selection,
filesystem, field-id and presentation behavior.

## 2. Data-source ownership

`EditorPresentationContextDataSource` may supply only:

- the exact media root stored by one explicit Project id;
- the exact Theme family and Icon Theme, Status Bar and Navigation Bar Variant
  references stored by one explicit Theme id;
- the exact current Production Font files field for one explicit font id.

It composes current facade/domain operations during the repository transition.
It is read-only and is not a repository.

The source must not find a Project from the tree; inspect a selected node;
resolve filesystem paths; open a picker; count files or Theme references;
construct a subtitle; react to a field id; mutate a tree node; create controls;
execute SQL; repair data; resolve Preview state; or infer context from names,
types, labels, positions or indices.

## 3. Consumer ownership

`EditorPathBrowser` retains:

- the current selected-node callback and explicit Project-ancestor traversal;
- file/folder picker options and media filters;
- Project-relative path conversion and local filesystem checks.

It asks the source only for the selected Project's stored media root.

`EditorFieldPostCommitEffects` retains:

- explicit field-id and node-kind routing after a successful generic commit;
- in-memory title, subtitle, color and navigation-tree presentation updates;
- counting the supplied Theme references and Production Font file lines;
- deciding which Preview/options/navigation callback must run.

Both services may receive `SpikeDatabase` only as a construction parameter for
the typed source. Neither may retain a database field or call database methods
directly.

## 4. Preserved boundaries

- Field definitions, values and commits remain in their existing dictionary
  and field-value services.
- The Project media root remains persisted data; path normalization remains in
  `ProjectPathService` and picker presentation.
- Theme references remain exact stable ids/full Component Variant references.
- Production Font files remain the required current stored document.
- Post-commit effects mutate only presentation state after persistence has
  succeeded; they create no alternate write path.
- Preview resolution, bridge and renderer behavior do not change.

## 5. Enforcement and tests

Architecture enforcement must verify:

- this contract is linked from `AGENTS.md` and the architecture index;
- both consumers compose `EditorPresentationContextDataSource`;
- neither consumer retains or calls `SpikeDatabase`;
- the source contains no SQL, Avalonia, filesystem, picker, field-definition,
  tree mutation, Preview, resolver or renderer logic.

A disposable-database test must compare Project media root, Theme navigation
values and Production Font files with their exact current facade values and
prove the reads leave the database byte-for-byte unchanged. Existing field and
navigation tests remain authoritative for presentation behavior.

## 6. Out of scope

This phase does not redesign pickers, media roots, Theme cards, Production Font
import, tree subtitles, dictionary fields, navigation or Preview. It changes no
tables, JSON, assets, parity data, animation, Render Mode or export behavior.

## 7. Forbidden shortcuts

- reading Project, Theme or Production Font records directly from either
  consumer;
- moving path resolution or filesystem access into the data source;
- persisting a derived tree subtitle or file/reference count;
- selecting a Project or Theme by label, name or position;
- adding an alternate field write from a post-commit presentation callback;
- moving presentation or Preview behavior into persistence.
