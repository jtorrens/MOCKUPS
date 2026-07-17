# Production Preview Session Data Boundary Contract

Status: normative.

This document governs the remaining desktop data reads used by Production
Preview navigation, transport and playback. It extends contracts 26, 31, 36,
41, 51, 52 and 54 without changing Shot, Module Instance, Variant or animation
persistence.

## 1. Objective

Production Preview data lookup, timeline calculation and UI orchestration have
separate owners:

```text
validated current Shot and Module Instance data
→ ProductionPreviewSessionDataSource + ModuleInstanceTimelineDataSource
→ exact Shot fps, owning Shot id, Variant config and ordered stable Screen ids
→ EditorPreviewController
→ Production context, transport, playback and Preview requests
```

The data sources supply exact current values. `ModuleInstanceTimeline` owns
duration and owner-frame calculations. The controller owns transient Preview
navigation and playback state.

## 2. Session data-source ownership

`ProductionPreviewSessionDataSource` may supply only:

- the exact owning Shot id of an explicit Module Instance id;
- the exact effective frame rate of an explicit Shot id;
- the complete current selected Module Variant config JSON for an explicit
  Module Instance id.

It must not parse `appearanceMode`; choose an active Screen; calculate a Screen
origin or duration; accumulate Shot frames; advance a playhead; create UI;
execute SQL; repair current documents; or infer an owner from Module, App,
Variant, name, type, position or index.

The service name describes its Preview-controller lifetime. It does not cache,
persist or own session state and remains a read-only facade/domain composition
boundary.

## 3. Timeline-source reuse

Ordered stable Screen ids come from `ModuleInstanceTimelineDataSource`.
`EditorPreviewController` must not call `GetShotModuleInstanceSlots` directly
or introduce a second slot-order data source.

`ModuleInstanceTimeline` remains the only owner of:

- effective Screen and Shot durations;
- Screen start-frame projection;
- owner-relative and Shot-absolute keyframe projection.

The controller may use those results to choose the visible active Screen and
move between Screen boundaries. It must not reproduce duration formulas.

## 4. Controller ownership

`EditorPreviewController` retains:

- Production Shot/Screen context selection and history presentation;
- one absolute Shot playhead and Screen-local navigation projection;
- previous/next Screen and keyframe transport;
- playback preparation, elapsed-time progression and Play/Stop UI;
- reading the explicit Module Variant `appearanceMode` from the supplied
  complete config;
- diagnostics that compare payload-local and Shot-absolute frames.

The controller may receive `SpikeDatabase` as a construction/composition
parameter while the staged shell creates its typed services, but it must not
retain a database field or perform direct database reads.

## 5. Preserved temporal and payload boundaries

- Stable ids, never indices, bind Screens, owners and keyframe tracks.
- Persisted keyframes remain relative to their stable owner.
- Preview retains one absolute Shot playhead and converts to Screen-local time
  before resolution.
- Explicit/calculated duration policy remains contract-owned.
- Runtime forwarding, local Overrides and complete Variant references remain
  unchanged.
- Variant `appearanceMode` affects presentation only through its explicit
  current config.
- Payload resolution completes before the generic Preview renderer.
- Playback and navigation reads never rewrite duration, payload or animation.

## 6. Enforcement and tests

Architecture enforcement must verify:

- `ProductionPreviewSessionDataSource` is the declared database boundary and
  contains no SQL;
- `EditorPreviewController` retains no database field and performs no direct
  database method call;
- the controller gets ordered Screen ids only from
  `ModuleInstanceTimelineDataSource`;
- this contract is linked from `AGENTS.md` and the architecture index.

A disposable-database test must compare the owning Shot id, fps, complete
Variant config and ordered stable Screen ids with the exact current facade
values and prove that all reads leave the database byte-for-byte unchanged.
Existing timeline and Preview tests continue to cover duration, keyframe and
render behavior.

## 7. Out of scope

This phase does not redesign transport, fix keyframe dragging, change playback
cadence, add Render Mode/export, alter Variant appearance behavior, change
tables, parity data, assets, payloads, durations or animation documents.

## 8. Forbidden shortcuts

- ordering Screens by name, type, Module or array index instead of stored stable
  slot order;
- deriving fps from Project or renderer state when the Shot declares it;
- reading a class default instead of the selected complete Module Variant;
- calculating duration or owner origins in the data source;
- caching a derived duration as persisted truth;
- retaining a general database handle in the Preview controller;
- adding playback timers or component behavior to the bridge or renderer.
