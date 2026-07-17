# Runtime Input Options Data Boundary Contract

Status: normative.

This document governs the desktop data boundary used to populate dictionary
controls for Runtime Input values and declared dynamic option lists. It extends
contracts 23, 31, 34, 36 and 53 without changing `FieldDefinition`, `ValueKind`,
Runtime Input contracts or persisted payloads.

## 1. Objective

Runtime Input option lookup and dictionary construction have separate owners:

```text
validated current Actor, Palette and Component preset data
→ RuntimeInputOptionsDataSource
→ exact FieldOption lists or preset display name
→ RuntimeInputFieldDefinitionFactory / RuntimeInputDynamicOptions
→ FieldDefinition
→ registered dictionary control
```

The data source reads explicit current options. The factories decide how
declared Runtime Input metadata becomes a generic dictionary definition or a
dynamic option list.

## 2. Data-source ownership

`RuntimeInputOptionsDataSource` may supply only:

- Actor options for one explicit Project through `ActorPreviewDataSource`;
- Palette color-token options for one explicit Project;
- complete Component preset reference options for explicit component types and
  the declared `includeNone` policy;
- the current display name of one complete Component preset reference.

It must not inspect a field label or JSON key to choose an option source;
construct `FieldDefinition`; parse a structured collection; create controls;
execute SQL; repair references; shorten preset ids; or infer a component class,
Variant, Actor or Palette token from name, type spelling, position or index.

The source composes current facade/domain operations during the repository
transition. It is not a repository.

## 3. Field-definition ownership

`RuntimeInputFieldDefinitionFactory` retains the exhaustive mapping from the
declared `ValueKind` and input metadata to generic `FieldDefinition`:

- `RecordReference` with explicit `tableId: actors` requests Actor options;
- `ComponentPreset` with explicit `componentType` requests complete preset
  references and preserves `AllowEmptyComponentPreset`;
- `PaletteColorToken` requests exact Palette token options;
- every other kind uses only its declared options.

The factory also retains numeric bounds, pair labels, structured-collection
metadata, units, animation and behavior timing. It consumes only the typed
options data source and must not accept `SpikeDatabase`.

## 4. Dynamic-option ownership

`RuntimeInputDynamicOptions` reads only the collection and keys explicitly
declared by `OptionsSourceCollectionJsonKey`, `OptionsSourceValueJsonKey` and
`OptionsSourceLabelJsonKey`. A declared `presetId` label key may ask the data
source for the preset display name. This is metadata-driven behavior, not a
guess based on component class or field position.

Dynamic options remain a presentation of current structured values. They do
not create ids, alter ordering, forward Runtime Inputs, change selected state
or persist collection data.

## 5. Preserved boundaries

- Every editable Runtime scalar still follows
  `FieldDefinition → ValueKind → registered dictionary control → generic commit`.
- Stable Actor ids, Palette tokens and full Component Variant references remain
  the stored values; labels are presentation only.
- Forwarding and local Overrides remain explicit.
- New Component boundaries still select an explicit Default Variant.
- Test Values remain session-only outside a persisted Module Instance.
- Animation continues to use the same definitions and owner-relative tracks.
- Repositories, Preview bridge and renderer gain no Runtime Input UI knowledge.

## 6. Enforcement and tests

Architecture enforcement must verify:

- both Runtime Input option factories contain no `SpikeDatabase` dependency;
- `RuntimeInputOptionsDataSource` is their database boundary and contains no
  SQL;
- it reuses the Actor data boundary for Actor options;
- Runtime Inputs and Module Instance animation editors reuse one data-source
  instance per editor service;
- this contract is linked from `AGENTS.md` and the architecture index.

A disposable-database test must compare Actor, Palette and Component preset
options with their exact current facade values, resolve a declared dynamic
preset label and prove that the reads leave the database byte-for-byte
unchanged.

## 7. Out of scope

This phase does not change option labels, collection presentation, dictionary
controls, Runtime Input definitions, Test Values, payload preparation,
forwarding, Overrides, tables, parity data, assets or animation behavior.

## 8. Forbidden shortcuts

- selecting options from a field name, label suffix, component class or tree
  position;
- storing a display label instead of the stable id/token/reference;
- accepting a short preset id as current data;
- choosing the first Component or Variant when a reference is missing;
- building a raw ComboBox outside the dictionary registry;
- querying persistence from either option factory;
- moving Runtime Input or Component semantics into a repository, bridge or
  renderer.
