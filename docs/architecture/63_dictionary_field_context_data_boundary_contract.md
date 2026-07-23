# Dictionary Field Context Data Boundary Contract

Status: normative.

This document governs persisted context reads used by the shared desktop
dictionary-control service. It extends contracts 23, 34, 36, 40, 43, 55, 58
and 60 without changing `FieldDefinition`, `ValueKind`, Runtime Input contracts,
Theme documents, Component Variants or editor presentation.

## 1. Objective

Dictionary composition and persisted context lookup have separate owners:

```text
validated current Theme, Icon Theme, Palette and Component Variant data
→ DictionaryFieldContextDataSource
→ exact ids, tokens, options, contracts and asset paths
→ EditorDictionaryFieldServices
→ DictionaryFieldServices delegates
→ registered dictionary controls
```

The data source is the general database boundary for shared dictionary context.
The editor service retains UI composition, dialogs, field-value callbacks,
Behavior Timing presentation and embedded Override navigation.

## 2. Data-source ownership

`DictionaryFieldContextDataSource` may supply only:

- the effective Icon Theme id for an explicit editor node and selected Design
  Theme context;
- exact current Theme tokens, including the explicit Shot-owned Theme route for
  a Module Instance;
- the exact asset path for one explicit Icon Theme id and stored token file;
- Palette token options for one explicit Project;
- full Component Variant reference options for one explicit Project and
  declared component type selector;
- current Component Variant Runtime Input bindings, values and structured
  collection definitions;
- exact Project, component type, record class and complete config documents for
  one full Component Variant reference.

It composes current facade/domain operations and the existing Preview Theme
selection boundary during the repository transition. It is read-only and is
not a repository.

The source must not create controls; open dialogs; construct `FieldDefinition`;
choose a Project, Theme, Component Class or Variant; shorten a Variant
reference; choose Default for an existing boundary; interpret UI labels; apply
forwarding or Overrides; execute SQL; access SVG contents; repair documents; or
infer anything from names, types, positions or indices.

An empty Theme token object remains permitted only for isolated Design context
with no selected/effective Theme, matching the existing UI state. A Production
Module Instance must resolve its exact Shot → Actor → Theme context or fail.

## 3. Editor-service ownership

`EditorDictionaryFieldServices` retains:

- the selected node and Project ancestor UI context;
- current selected Design Theme callback;
- generic picker/dialog delegates;
- dictionary field-value and Runtime Test Value callbacks;
- Behavior Timing resolution through declared metadata and supplied Theme
  tokens;
- explicit embedded Component Override context construction;
- mapping the typed source methods to `DictionaryFieldServices` delegates.

It may receive `SpikeDatabase` only as a construction parameter for the typed
data source. It must not retain a general database field or call database
methods directly.

`SvgIconPreview` remains a presentation helper. For the dictionary route it
receives a resolved asset-path callback; it must not make the dictionary
service query persistence or interpret Icon Theme records.

## 4. Preserved contracts

- Every editable scalar still follows
  `FieldDefinition → ValueKind → registered dictionary control → generic commit`.
- Stable ids, Palette tokens and full Component Variant references remain the
  stored values; labels remain presentation only.
- Variant selection, forwarding and local Overrides remain explicit.
- Crossing a new Component boundary still selects its explicit Default
  Variant; current references never fall back to Default.
- Design Test Values remain session-only and Production Runtime Values remain
  persisted instance payload.
- Theme and Component documents are strict current JSON and are never repaired
  while read.
- primitive and pair controls consume the exact shared `ValueKind` parser;
  malformed pair members, boolean/icon-list documents and out-of-range
  Alpha/Hue values are not converted into plausible control state.
- Integer/Decimal controls validate assigned current values and declared ranges;
  invalid or incomplete interactive text remains a draft and is never committed
  as zero or a clamped boundary value.
- pair labels are explicit `FieldDefinition`/Runtime contract metadata and are
  never inferred from field ids, JSON keys, kinds, hierarchy or position;
  every generic projection through `ComponentInputBindingDefinition` preserves
  those labels unchanged before constructing the nested dictionary field.
- Preview resolution still completes before the generic bridge and renderer.

## 5. Enforcement and tests

Architecture enforcement must verify:

- this contract is linked from `AGENTS.md` and the architecture index;
- `EditorDictionaryFieldServices` retains no database field and makes no direct
  database call;
- it composes one `DictionaryFieldContextDataSource`;
- the data source contains no SQL, Avalonia control, dictionary definition,
  dialog, forwarding, Override or renderer logic;
- dictionary Icon Theme presentation receives only a resolved token asset-path
  callback.

A disposable-database test must compare Theme and Icon Theme context, Palette
and Component Variant options, bindings, Runtime values, collections, selection
documents and one token asset path with the exact current facade values. All
reads must leave the database byte-for-byte unchanged.

## 6. Out of scope

This phase does not redesign dictionary controls, pickers, Runtime Inputs,
structured collections, Behavior Timing, Overrides, forwarding, Themes, Icon
Themes or Component Variants. It does not change tables, JSON, assets, parity
data, Preview payloads, animation, Render Mode or export.

## 7. Forbidden shortcuts

- querying `SpikeDatabase` directly from `EditorDictionaryFieldServices`;
- selecting Theme or Variant context from a label, name or tree position;
- resolving a short Component variant id;
- supplying a token filename from the token id instead of its stored file;
- reading or repairing SVG/token documents inside the data source;
- returning empty Production Theme context;
- moving dictionary or component semantics into persistence, bridge or
  renderer code.
