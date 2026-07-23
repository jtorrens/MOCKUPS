# Module Instance Animation Document Boundary Contract

Status: normative.

This document governs the desktop boundary between persisted Screen animation
documents and the Module Instance animation editor. It extends contracts 23,
29, 31, 36, 47 and 52 without changing animation v2, temporal ownership,
duration policy or editor interaction.

## 1. Objective

Persistence, timeline calculation and animation authoring have separate owners:

```text
validated current Module Instance and selected Module Variant documents
→ ModuleInstanceTimelineDataSource + ModuleInstanceAnimationDocumentStore
→ exact current strings
→ ModuleInstanceAnimationEditor
→ owner-relative authoring through ModuleInstanceAnimationDocument
→ explicit prepared animation JSON write
→ ModuleInstanceRepository through the compatibility facade
```

The store is the editor's narrow document boundary. The shared timeline data
source remains the only input to duration and frame-origin calculation.

## 2. Document-store ownership

`ModuleInstanceAnimationDocumentStore` may:

- load the complete current selected Module Variant config;
- reuse `ModuleInstanceTimelineDataSource` to load the current animation,
  Runtime Input preview, Theme tokens and effective runtime contract documents;
- accept one complete prepared animation v2 JSON document for an explicit
  Module Instance id;
- delegate that explicit write through the current facade to the owning Module
  Instance repository;
- return the exact persisted current animation document after the write.

It must not parse tracks; infer fields or targets; calculate duration, origins
or frame projections; manufacture a missing document; repair malformed JSON;
create UI; select a Screen or Variant; execute SQL; or persist derived duration.

The store is a staged domain/document boundary, not a repository. SQL, row
mapping, validation at the repository write boundary and write synchronization
remain owned as declared by contract 47.

## 3. Animation-editor ownership

`ModuleInstanceAnimationEditor` retains:

- parsing the strict current animation document for authoring;
- discovering animatable targets from declared Runtime Input metadata;
- stable `fieldId`/`targetId` track selection;
- the Screen-local authoring scale projected from the absolute Shot playhead;
- keyframe activation, values, interpolation, removal and move intent;
- session-only selection and provisional authoring horizon;
- Retime and reference-duration presentation;
- delegating Play/Pause to the shared Preview playback owner;
- producing one complete animation v2 document before an explicit save.

The editor may receive `SpikeDatabase` only as a construction/composition
parameter while it creates the shared timeline source and document store. It
must not retain a database field or call database methods directly.

`ModuleInstanceAnimationDocumentContract` is the single structural owner of
current `animation_json` v2. Startup validation, prepared writes and the editor
document must all consume it. It requires object entries, stable non-empty
track/keyframe ids, explicit interpolation and enabled state, unique
`fieldId`/`targetId` targets, positive retime durations, mandatory enabled KF0
and persisted keyframes in ascending owner-local frame order. It does not own
field discovery, `ValueKind` semantics, frame origins, duration formulas or
Preview resolution.

## 4. Temporal ownership remains common

`ModuleInstanceTimeline`, `RuntimeAnimationFrameOrigin` and declared runtime
contract metadata remain the only authorities for:

- Screen start and effective duration;
- explicit versus calculated duration policy;
- owner first appearance and owner-local frame zero;
- field completion dependencies and reference duration;
- stable target projection into the Screen authoring scale;
- retime projection without rewriting authored keyframes.

The common owner timeline consumes optional contract members only by declared
absence. When `collections`, `inputs`, `actions`, collection `fields` or
`itemActions` are present, they are arrays of objects; a present Runtime
collection is an array of stable object items with non-empty ids. Timeline
metadata objects and their field-id lists keep their exact roots and entries.
Projected items declaring `itemRuntimeContractJsonKey` keep that required
nested object and any present nested input/action list remains an array of
objects. The desktop calculator must reject a wrong root or malformed entry
instead of filtering it into an empty timeline.

`RuntimeOwnerTimeline` applies the same envelope contract after the prepared
payload crosses into web Preview. It preserves only declared structural
absence and rejects present malformed contract arrays, Runtime collections,
stable items, embedded/projected Runtime objects and timeline field-id lists.
This is parity validation for the common temporal owner, not component logic in
the bridge or renderer.

The desktop common timeline also accepts an explicit empty transient animation
object for an owner with no authored tracks. Once `tracks` or `retime` is
present, their calculation envelope is strict: tracks/keyframes are object
arrays, track field ids are stable strings, and target ids are either stable
strings or the explicit empty Screen-owner sentinel. Keyframe frames are
non-negative integers, optional enabled state is boolean, and all authored
root/target retime durations are positive integers. This consumption guard
complements, but does not replace, the complete persisted v2 document contract
above. The web owner timeline applies the same transient-animation guard before
resolving frames.

Owner-authored timeline metadata is a closed temporal vocabulary. Collections
may declare serial sequencing, explicit `sequenceItems`, pre/post duration field
ids and the complete `firstMatchingValue` owner-origin object. Fields may
declare `ownerStart` or complete `fieldCompletion` origin, an explicit boolean
`extendsOwnerDuration`, and a completion object with a real base-duration field,
the supported `lastEnabledKeyframe` override and a minimum of at least two.
Offsets and Runtime duration values are non-negative numeric frames. Missing
referenced fields or values are invalid; the desktop calculator never replaces
them with frame zero.

The store must not reproduce any of these formulas. A save persists authored
animation only; it must not persist a calculated Screen extent or absolute Shot
frame.

## 5. Preserved contracts

- Tracks persist only v2 `fieldId`/stable `targetId` keyframes.
- Keyframes remain relative to their owner.
- Reordering an owner changes projection without rewriting its keyframes.
- Re-entry does not restart the entity's internal timeline.
- Explicit Screen duration remains instance-owned and authoritative.
- The selected Module Variant remains a complete explicit reference.
- Runtime Input forwarding and local Overrides remain explicit.
- Preview resolution completes before the generic bridge and renderer.
- The renderer runs no animation timer or component-specific interpolation.

## 6. Enforcement and tests

Architecture enforcement must verify:

- `ModuleInstanceAnimationDocumentStore` owns the editor's database dependency,
  contains no SQL and reuses `ModuleInstanceTimelineDataSource`;
- `ModuleInstanceAnimationEditor` retains no database field and performs no
  direct database call;
- the editor still uses the shared timeline and owner-frame utilities;
- the shared desktop owner timeline rejects present wrong-root or filtered
  Runtime contract collections, items, fields, inputs and actions;
- the web owner timeline enforces the same envelopes before resolving a frame;
- the desktop timeline validates every present transient track/keyframe/retime
  calculation envelope without filtering malformed entries;
- the web timeline mirrors that transient animation envelope, including the
  explicit empty Screen target sentinel;
- the desktop timeline validates closed collection/field temporal metadata and
  exact referenced duration values before applying its formulas;
- the store delegates only a complete animation document write;
- startup, writes and the animation editor use the one common v2 document
  contract;
- this contract is linked from `AGENTS.md` and the architecture index.

A disposable-database test must compare every loaded document with the exact
current facade value, prove loads are byte-for-byte read-only, explicitly save
the same complete current animation document and verify the persisted result.
Existing animation tests remain authoritative for tracks, duration, origins,
retime and frame resolution.

Contract 61 composes this store for track activation and structured collection
target reconciliation initiated by the Runtime Inputs editor. It must still
receive and persist only one complete prepared animation v2 document.

## 7. Out of scope

This phase does not fix or redesign keyframe dragging, alter animation controls,
change timing metadata, add a second playback clock, change tables or JSON
shape, migrate data, add Render Mode/export or modify parity assets.

## 8. Forbidden shortcuts

- accepting partial tracks or a plausible empty animation document;
- binding tracks by label, type, owner position or collection index;
- storing absolute Shot frames in owner-local keyframes;
- recalculating or persisting Screen duration during a document read;
- bypassing the Module Instance repository with local SQL;
- moving duration formulas into the store or UI;
- adding animation behavior to the Preview bridge or renderer.
