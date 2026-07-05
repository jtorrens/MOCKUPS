# Editor modernization roadmap

This document defines the low-friction path for moving the desktop editor spike toward a more data-driven, composable architecture.

It complements `editor_shell_non_negotiables.md`. The non-negotiables define what must not be broken. This roadmap defines the order in which to remove current architectural pressure.

## Direction

The target architecture is:

```text
editor layout metadata
  -> field catalog
  -> FieldDefinition
  -> ValueKind
  -> dictionary control registry
  -> shared commit coordinator
  -> field value service
  -> repository/database
  -> resolver
  -> preview payload/frame model
```

The editor shell should organize and delegate. It should not know individual field storage rules, value-specific controls, or record-specific visual behavior.

Generic routines must be extracted before they spread. Shared algorithms belong in common/editor-shell services, not inside whichever editor, bridge, renderer, importer, or repository first needed them.

Before adding a private helper for parsing, normalization, paths, colors, numeric conversion, SVG processing, token mapping, metrics, or other cross-cutting behavior, check `spikes/desktop-editor-shell/Common` and `docs/architecture/21_desktop_editor_base_routines_audit.md`. If an analogous routine exists, reuse or extend it in common rather than creating another local variant.

## Phase 1: make rules explicit

Keep the operational rules in:

- `AGENTS.md`
- `docs/architecture/editor_shell_non_negotiables.md`
- this roadmap

Every architecture cleanup should preserve a usable editor after each small step. Do not start with broad database or UI rewrites.

## Phase 2: extract field catalogs

Move field definitions out of `MainWindow`.

Start with low-risk record classes:

- project fields;
- episode fields;
- palette fields.

Then continue with:

- device fields;
- actor fields;
- theme fields;
- font fields;
- status/navigation bar scalar fields;
- component class fields.

Each field descriptor should declare:

- stable field id;
- label;
- `ValueKind`;
- default value;
- options, if any;
- editability;
- storage target or JSON path;
- semantic hints such as pair labels or token family;
- future validation and animation metadata.

Do not let layout JSON remain the only metadata. Layout may order fields, but field catalogs define what fields mean.

## Phase 3: extract field value access

Move field read/write logic out of `MainWindow`.

Introduce a shared service with responsibilities like:

```text
GetFieldValue(node, fieldId)
CurrentStoredValue(node, fieldId)
ToStorageValue(node, fieldId, draftValue)
CommitFieldValue(node, fieldId, storageValue)
```

`MainWindow` may wire commits, but it must delegate storage rules.

## Phase 4: extract editor extras

Move record-specific editor behavior out of `MainWindow`.

Examples:

- actor avatar preview;
- palette navigation swatch and used marker;
- theme color pair labels;
- navigation subtitles by record type;
- collection editor dispatch.

Use small extension/decorator classes instead of adding more `node.Kind` branches to the shell.

## Phase 5: introduce dictionary control registry

Make `DictionaryFieldControl` a row host, not the factory for every editor type.

Target shape:

```text
DictionaryFieldControl
  label
  restore/default state
  changed marker
  value editor slot

DictionaryControlRegistry
  ValueKind -> IDictionaryValueEditor
```

Each value editor owns its internal layout, validation, commit gesture, picker trigger, and display invariants.

## Phase 6: refine ValueKind

Split broad kinds into semantic kinds before adding local exceptions.

Examples:

- `number.integer`;
- `number.decimal`;
- `pair.xy`;
- `pair.widthHeight`;
- `pair.lightDarkColor`;
- `token.paletteColor`;
- `token.themeColor`;
- `token.themeRadius`;
- `token.icon`;
- `token.iconList`;
- `path.directory`;
- `path.imageFile`.

Avoid inferring behavior from field id strings. If a pair needs `X/Y`, `W/H`, or `Light/Dark`, that belongs in metadata or the value kind.

## Phase 7: resolve before preview

Create an explicit resolver pipeline:

```text
editable data
  -> resolved data
  -> frame-specific data
  -> preview payload
```

The preview should consume resolved data. It should not know editor forms, draft controls, inheritance rules, or component override rules.

## Phase 8: split repositories after field extraction

Do not start by splitting `SpikeDatabase`. First remove field and shell coupling.

Once field access is delegated, split database responsibilities into focused services/repositories:

- tree/project repository;
- field repository;
- theme repository;
- component class repository;
- collection repositories;
- preview payload/resolver service.

## Guardrails

Reject new changes that add:

- field definitions directly inside `MainWindow`;
- field persistence directly inside `MainWindow`;
- `TextBox`, `ComboBox`, `ToggleSwitch`, numeric inputs, color pickers, font pickers, or icon pickers for scalar values outside the dictionary route;
- record-specific navigation rendering directly inside `MainWindow`;
- field behavior inferred from string suffixes when metadata can declare it;
- preview logic that reads editor controls or form state;
- another value control path parallel to `ValueKind`.
- reusable SVG, theme-token, color, JSON-path, numeric parsing, import mapping, or device metric routines inside a single module instead of a common/shared class.

Allowed custom editor chrome:

- cards;
- section headers;
- tree rows;
- toolbar buttons;
- add/delete/reorder collection row chrome;
- preview shell;
- modal frame.

Even inside collection rows, scalar fields must use `FieldDefinition` and dictionary controls.

## Size alarms

These are not formatting goals. They are early warning limits.

- `MainWindow.axaml.cs` should trend toward 600-800 lines.
- `DictionaryFieldControl.cs` should trend toward 250-350 lines.
- `SpikeDatabase.cs` should not grow during editor cleanup work; it should only shrink or be split.

If a change makes one of these files materially larger, extract first.

## Temporary exceptions

Temporary exceptions must be explicit and searchable:

```text
TODO(editor-architecture): explain why this exists and which phase removes it.
```

Do not add silent compatibility fallbacks or one-off local fixes for editor-specific behavior.
