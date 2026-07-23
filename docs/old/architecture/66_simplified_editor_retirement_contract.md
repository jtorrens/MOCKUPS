# Simplified Editor Retirement Contract

Status: normative.

This document retires the experimental Simplified Editor described by contract
30. It extends contracts 33, 35, 36 and 45 without changing domain fields,
component/module contracts, dictionary controls, persisted runtime values or
Preview.

## 1. Decision

The desktop application has one editor presentation: the complete
metadata-driven editor.

```text
current editor layout cards
→ FieldDefinition
→ ValueKind
→ registered dictionary control
→ shared commit path
→ current persistence owner
```

There is no Simplified/Complete mode, projection, promotion control, captured
embedded default or alternate editor card composition.

## 2. Current editor interaction

The editor continues to provide:

- the authored cards and groups declared by the record class layout;
- shared card, internal-navigation and responsive presentation;
- direct, embedded and structured-collection dictionary controls;
- explicit inherited values, local Overrides and Runtime forwarding;
- session-only card expansion, internal selection, navigation width and scroll;
- the existing Preview and animation routes.

Opening or selecting an editor constructs those surfaces from current data. It
must not persist editor-layout changes, capture child metadata or write another
record as a side effect.

## 3. Retired interaction and code

The following are retired current behavior:

- the Simplified/Complete selector;
- field and collection promotion checkboxes;
- the captured-default provenance lock;
- `EditorSimplifiedProjectionState` and every related model/control;
- projection state stored in `DictionaryFieldServices`;
- automatic traversal of embedded layouts;
- automatic `SaveEditorLayout` calls while an editor is opened or rebuilt.

`EditorLayoutCardFactory` composes only ordinary complete cards. Structured
collections return their normal decorated dictionary controls without an
additional projection wrapper.

## 4. Editor layout persistence

`editor_layouts.layout_json` remains a required current JSON object. Its current
top-level contract contains exactly one `cards` array. The retired top-level
`simplified` property and its `capturedSlots` data are invalid current data.

The committed database migration for this phase accepted exactly the one known
previous document:

- owner: `component.keypad`;
- previous state: one top-level object-valued `simplified` property;
- operation: remove only that property;
- preserved state: the complete `cards` array and every other table/row/column.

The migration ran against a copy, validated the output, compared every other
table and every non-Keypad layout, verified Keypad card equality, then promoted
the validated result. Its temporary routine was removed in the same delivery.
The historical schema-v1 candidate databases remain historical evidence and
are not current seed authorities.

`EditorLayoutRepository` may retain its explicit Save operation for reviewed
development/layout-authoring workflows. Normal editor controllers and controls
must not call it. A current layout read rejects a missing `cards` array or any
additional top-level property instead of ignoring or repairing it.

## 5. Startup and migration boundary

Startup validation is read-only and rejects:

- a layout without a top-level `cards` array;
- any additional top-level layout property;
- retired `simplified` or `capturedSlots` metadata;
- persisted derived `VisibleGroups`, `VisibleFields` or `Entries` properties.

Normal startup, repository reads and editor construction never remove or
translate retired metadata. Any future editor-layout contract change requires a
new explicit migration of the committed current database.

## 6. Session state

Contract 45 remains authoritative for working-point continuity. Session state
retains only the current editor's stable card expansion, internal navigation,
navigation width and clamped scroll. No presentation-mode key is created or
restored.

A fresh application session still begins with every card closed. Nothing from
this retirement moves session presentation into domain data or
`data/window-state.json`.

## 7. Preserved architecture boundaries

- Every editable scalar still uses `FieldDefinition`, `ValueKind` and the
  registered dictionary control.
- Embedded Components still reference complete Variants and use explicit local
  Overrides.
- Runtime forwarding remains explicit and unchanged.
- Stable ids, Variant references and owner-relative keyframes are unchanged.
- Resolver, renderable, bridge, renderer and Preview payload behavior are
  unchanged.
- `MainWindow` remains shell-only.
- No component name, type, hierarchy depth or position selects an alternate
  presentation.

## 8. Enforcement and tests

Architecture enforcement must verify:

- this contract is linked from `AGENTS.md` and the active architecture index;
- the retired projection source file and C# types do not exist;
- active editor code contains no Simplified mode, promotion or capture route;
- current parity layouts contain only the top-level `cards` array;
- startup validation explicitly enforces the cards-only contract;
- ordinary editor code has no `SaveEditorLayout` call.

Behavioral tests must prove that retired or incomplete layout roots fail
without modifying their disposable database, layout serialization emits only
authored card metadata and normal startup remains byte-for-byte read-only.

## 9. Out of scope

This retirement does not redesign cards, tabs, breadcrumbs, compact layout,
Variants, Overrides, Runtime Inputs, animation, Production navigation, Render
Mode or component/module scaffolding. Future progressive disclosure must be
designed as an explicit workflow and must not restore this parallel projection
implicitly.

## 10. Forbidden shortcuts

- silently ignoring or deleting `simplified` during normal reads;
- retaining dormant projection types for possible future use;
- replacing the selector with another name-based basic/advanced switch;
- writing editor layouts while opening, selecting or rebuilding an editor;
- moving frequently used field copies into component/module runtime data;
- treating historical schema-v1 database files as active migration inputs;
- adding a fallback empty cards array for an incomplete current layout.
