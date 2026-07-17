# Module Instance Persistence Contract

Status: normative.

This document governs the Module Instance (Screen) persistence slice of the
staged desktop repository extraction. It extends contracts 26, 29, 31, 33, 34,
35, 36, 40, 41 and 44 without changing production authoring, Runtime Inputs,
Variants, animation, duration policy, payload preparation or Preview.

## 1. Persistence ownership

Module Instance rows follow one persistence route:

```text
module_instances current rows
→ ModuleInstanceRepository
→ SpikeDatabase compatibility facade and domain coordinators
→ tree / editor / payload / timeline caller
```

`ModuleInstanceRepository` owns exact and ordered row reads, strict current
JSON root validation, prepared complete-document writes, positive stored
duration writes, sibling order swaps, explicit insert/duplicate/Rename/delete
row persistence and exact full Module Variant reference counts.

The repository returns `ModuleInstanceRecord` persistence records. It does not
return tree nodes, editor controls, payloads, resolved frames or renderables.

## 2. Current row and document contract

Every record preserves its stable `id`, explicit `shot_id`, `app_id` and
`module_id`, authored name/notes, sibling `sort_order`, current stored
`duration_frames` and the five required current object documents:

- `transition_json`;
- `content_json`;
- `behavior_json`;
- `animation_json`;
- `metadata_json`.

Repository reads reject blank, malformed or wrong-root documents. Prepared
writes validate every replacement before the first SQL mutation. They never
replace invalid data with `{}`, translate retired fields, supply a missing
Variant reference, repair an animation track or infer identity from a name,
type, order or position.

Creation may construct the declared current initial documents explicitly.
Duplication copies the selected complete current row into a new generated
stable id. Display-name disambiguation never replaces stable identity.

## 3. Domain decisions stay outside persistence

The following remain in their current owners and may only hand the repository
an already prepared current record/document/value:

- selection and validation of the complete
  `moduleId::variant::variantId` reference;
- explicit Default Variant selection when a new Module boundary is crossed;
- Runtime Input definitions, persisted instance values and explicit
  forwarding;
- structured runtime collections, stable item ids, slots, States,
  Replace/Overlay and Reflow;
- local Overrides and embedded concrete Component Variants;
- v2 `fieldId`/`targetId` tracks, owner-relative keyframes and collection-track
  reconciliation;
- calculated versus explicit Screen duration policy, natural timing,
  completion dependencies and Shot-duration synchronization;
- Shot owner Actor/Theme context and token interpretation;
- payload preparation, Shot-to-Screen frame translation, resolver dispatch,
  renderable construction, bridge translation and generic rendering.

The repository validates document roots and positive stored duration. It does
not calculate duration, create animation origins, remove orphan tracks, merge
Runtime defaults or resolve a Theme.

## 4. Coordinated operations

`SpikeDatabase` remains the compatibility facade. It may validate a requested
user action, prepare complete JSON, ask the repository to persist it and then
invoke the existing cross-domain synchronization required by that action.

Variant changes prepare metadata, Runtime content and animation together
before one repository update. Collection duplication/deletion prepares content
and animation together before one update. Timeline synchronization reads typed
Module Instance records, applies the contract-owned calculation outside the
repository, and persists only the resulting positive duration.

Tree projection may combine repository records with Module definition records
to display a Module label. The label is presentation only and never becomes a
stored or routing identity.

Whole-database validation, exact reference Usage and Production Theme context
remain aggregate read-only services. Their declared cross-domain inventory
queries do not authorize competing Module Instance writes or fallback readers.

## 5. Lifecycle

Screens are Production instances, so their existing explicit lifecycle remains
available: add from an exact Module and complete Variant reference, duplicate,
Rename, reorder and delete after Usage permits it. These actions preserve all
stable owner and target ids inside the copied/current documents.

This contract does not add App or Module definition lifecycle actions. Those
definitions remain development/scaffolding-owned under contracts 34 and 44.

## 6. Validation

Automated enforcement verifies:

- `IModuleInstanceRepository` and `ModuleInstanceRepository` are explicit;
- `SpikeDatabase` constructs and delegates through the repository;
- Module Instance, Module Variant, Runtime contract and tree orchestration
  retain no direct `module_instances` CRUD SQL;
- facade and repository reads agree for complete current rows;
- content, animation, coordinated Variant documents, duration, order,
  duplicate, Rename and delete round-trip on a disposable database copy;
- malformed replacement roots fail before mutation;
- Runtime, animation, Theme, tree and renderer concerns do not enter the
  repository;
- startup remains byte-for-byte read-only and the committed parity database is
  unchanged by this extraction.

## 7. Forbidden shortcuts

- passing the repository into `MainWindow` or an editor control;
- moving Runtime forwarding, Variant selection or collection semantics into
  persistence;
- calculating duration or keyframe origins in the repository;
- accepting legacy animation or short Variant references;
- deriving a target, owner, Module, Variant or Screen from names or positions;
- issuing competing `module_instances` writes from facade partials;
- synchronizing Shot duration, resolving Theme tokens or building Preview
  payloads inside the repository;
- changing schema, seeds or parity data as an incidental extraction step.
