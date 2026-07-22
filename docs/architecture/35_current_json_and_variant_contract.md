# Current JSON and Variant Contract

Status: normative.

This document governs how current persisted JSON is parsed and how Component
and Module Variant envelopes are validated, read and written. It extends the
persistence rules in contract 33 and the manifest/routing rules in contract 34.

## 1. Objective

Current data has one accepted shape. A repository consumes that shape or fails
before changing it:

```text
persisted current JSON
→ strict declared-root parse
→ complete owner/Variant envelope validation
→ current repository operation
→ validated explicit write
```

A reader or editor is not a repair boundary. It must not make incomplete data
look current by supplying a root object, a Variant array, a Variant config or a
lock/protection flag.

## 2. Persisted JSON roots

Every SQLite JSON column declares its current root kind in the persistence
contract: object or array.

```text
object
  projects.metadata_json
  episodes.metadata_json
  shots.canvas_json
  shots.metadata_json
  apps.config_json
  apps.metadata_json
  modules.config_json
  modules.design_preview_json
  modules.metadata_json
  module_instances.transition_json
  module_instances.content_json
  module_instances.behavior_json
  module_instances.animation_json
  module_instances.metadata_json
  palette_colors.metadata_json
  devices.metrics_json
  actors.metadata_json
  production_fonts.metadata_json
  icon_themes.mapping_json
  icon_themes.metadata_json
  render_presets.codec_json
  render_presets.color_json
  render_presets.quality_json
  render_presets.export_json
  render_presets.metadata_json
  component_classes.config_json
  component_classes.design_preview_json
  component_classes.metadata_json
  themes.tokens_json
  themes.metadata_json
  editor_layouts.layout_json

array
  production_fonts.files_json
```

A column is added to this inventory in the same change that adds it to the
canonical schema. The executable startup inventory must remain identical.

A persisted document is invalid when it is:

- blank;
- malformed JSON;
- a valid JSON scalar with the wrong root kind;
- `null` where the column requires an object or array.

Startup validation rejects those documents read-only. Normal repositories,
payload preparation and Preview consumers use the same strict interpretation;
they do not catch parse errors and return `{}` or `[]`.

An optional property inside a valid owner document may still be absent when
that owner's current contract explicitly declares it optional. This contract
does not centralize component semantics or invent one global component-config
schema. Component/module owners remain responsible for their own required and
optional fields.

Owner-specific semantic contracts are equally strict once declared. Contract
67 requires complete Status Bar and Navigation Bar configs and fixed item
arrays for both class parity data and every Variant snapshot; a root-valid
object with missing item members is not accepted as current data.

Explicit construction is different from fallback. A creation/scaffolding
routine may build a declared new `JsonObject` or `JsonArray`. It may not parse
missing persisted data as an empty container and then save the result.

## 3. Component Variant envelope

`component_classes.metadata_json.variants` is the current persisted Component
Variant array. `variants` is both the current storage vocabulary and the
product language.

The array is required, non-empty and contains objects only. Every entry has:

- a non-empty stable `id` unique within the Component class;
- a non-empty display `name`;
- an explicit boolean `protected`;
- an explicit boolean `locked`;
- an object `config` containing that Variant's complete owner snapshot.

Exactly one `default` id is required and it is protected. The class
`config_json` parity copy must equal the Default Variant config as required by
the existing architecture check.

Readers must not:

- skip a non-object or id-less array entry;
- infer `locked` or `protected` from the `default` id;
- replace a missing or empty Variant config with class config;
- create an implicit `variants` array while renaming, duplicating, deleting,
  locking or editing;
- select a Variant by name or position.

Creating or duplicating a Variant clones the complete selected Variant config
and supplies every envelope member explicitly. Renaming and locking preserve
the stable id and all complete references.

## 4. Module Variant envelope

`modules.metadata_json.variants` is required, non-empty and contains objects
only. Every entry has the same complete envelope:

- stable unique `id`;
- non-empty display `name`;
- explicit boolean `protected`;
- explicit boolean `locked`;
- object `config` containing the complete Module snapshot.

Exactly one `default` id is required and it is protected. A Module Instance
continues to store the complete `moduleId::variant::variantId` reference.
Readers reject malformed entries rather than filtering them before usage or
reference validation.

The protected default Module Variant may be renamed but never deleted. User
Variants may be created by cloning the active complete Variant, duplicated,
renamed and deleted only under the Usage/lock/protection rules established in
contract 34.

## 5. Write boundary

Every operation that accepts a JSON document validates its required root before
executing SQL. A current record is never partially updated after a parse or
Variant-envelope failure.

When a repository discovers an incomplete current envelope it throws with the
owner id and missing/invalid member. It does not call an `Ensure*`, normalize
the document or save a repaired version.

## 6. Validation and proof

Read-only startup validation and architecture enforcement cover:

- declared root kinds for every persisted JSON column;
- complete Component and Module Variant arrays;
- object-only entries and unique stable ids;
- explicit names, booleans and config objects;
- protected Default presence;
- complete Variant references;
- absence of catch-all object parsing, `EnsureVariantArray`, class-config
  fallback and id-derived lock inference in current paths.

Tests corrupt disposable database copies and prove both that opening them fails
and that the rejected file remains byte-for-byte unchanged. Repository tests
also prove that an incomplete envelope introduced after startup is rejected
without being repaired by a write operation.

## 7. Out of scope

This phase does not:

- change tables, columns or Variant reference spelling;
- split `SpikeDatabase` into repositories;
- centralize the semantic config schema of every component/module;
- change editor presentation, lifecycle actions, forwarding or Preview;
- migrate current parity data when it already satisfies this contract.

Future scaffolding must construct owner-specific complete configs directly and
validate them before promoting them into current data. It cannot depend on a
reader fallback added here.

## 8. Forbidden shortcuts

- catch any parse exception and return an empty object;
- parse blank persisted JSON as `{}` or `[]`;
- filter malformed Variant entries out of an array;
- synthesize a missing Variant array during an edit;
- infer a missing Variant flag from its id, name or position;
- use class config when a selected Variant has no config;
- persist a repair discovered while reading current data.
