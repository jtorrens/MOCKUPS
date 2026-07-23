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
- immediate Preview transition feedback and coalescing of stale scheduled
  selection refreshes;
- reading the explicit Module Variant `appearanceMode` from the supplied
  complete config;
- diagnostics that distinguish Shot-absolute, root Screen and current-boundary
  local frames.

The controller may receive `SpikeDatabase` as a construction/composition
parameter while the staged shell creates its typed services, but it must not
retain a database field or perform direct database reads.

Playback preparation is also transient controller-owned session state. Pressing
Play must present `Preparing playback` and yield one render opportunity before
enumerating or resolving the frame sequence. This first state is owned by the
native Preview-host loading surface and must not depend on the resident HTML
document or a queued WebView script. The controller resolves frames incrementally
through the same complete Production payload boundary and yields at background
dispatcher priority between frames so rendering and input remain responsive. A
new selection, data commit or second Play/Stop action cancels stale preparation;
cancellation must never produce a partial playable sequence.

Every visible preparation surface that offers `Esc to stop`, in isolated Design
Preview as well as Production Preview, owns a current cancellable operation and
passes its token through HTML preparation, asset preloading and raster
preparation. Starting a replacement operation cancels the previous one, and
only the current operation may clear the loading surface. The first Production
render yield is inside the same guarded cleanup scope as frame preparation, so
an immediate cancel cannot escape cleanup or leave Play/Busy state behind.
`Esc` is the shared Preview transport stop: while a preparation is visible it
cancels that operation, and while isolated Design or Production playback is
active it stops playback. When neither is active it remains unhandled for the
rest of the editor. The Preview controller owns this window-level routed action
so it remains reliable when focus moves between native editor controls and the
resident Preview surface; `MainWindow` does not implement the behavior.

The controller may retain one prepared Shot/Screen playback sequence and the
renderer cache-capacity reservation that supports it for the current
application session. Reuse is allowed only when an exact cryptographic
fingerprint still matches. That fingerprint includes the selected stable node,
requested frame range, Preview presentation context and complete resolved
payload documents for every participating stable Screen. A mismatch rebuilds
the sequence through normal payload resolution. The cache is never persisted,
never becomes payload truth and never permits a renderer to resolve omitted
data.

Selection feedback is part of this transient orchestration. When a selected
Production Shot or Screen has an explicit Preview route, the shell delegates
the transition immediately to the controller before rebuilding editor content.
The controller asks the resident Preview pane to present its loading state and
schedules only the latest requested Preview refresh at background dispatcher
priority. This gives the shell a render opportunity before payload preparation
without moving payload resolution into the UI, weakening strict failures or
changing the resolved result. The pane owns the loading presentation; the
shell must not manipulate WebView state directly.

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
- Prepared playback reuse never changes frame ownership, timing or resolved
  output and never bypasses complete Production payload resolution.

## 6. Enforcement and tests

Architecture enforcement must verify:

- `ProductionPreviewSessionDataSource` is the declared database boundary and
  contains no SQL;
- `EditorPreviewController` retains no database field and performs no direct
  database method call;
- the controller gets ordered Screen ids only from
  `ModuleInstanceTimelineDataSource`;
- playback presents its loading state before frame enumeration, resolves
  incrementally with cancellation and keys session-only reuse by an exact
  cryptographic fingerprint;
- immediate cancellation is cleanup-safe, isolated Design preparation uses the
  same real cancellation semantics and only the current operation may dismiss
  the loading surface;
- routed `Esc` stops the current Design or Production preparation/playback and
  remains unhandled when Preview transport is idle;
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
- reusing playback frames by name, type, position, approximate timestamps or a
  partial payload comparison;
- persisting prepared playback frames, fingerprints or cache reservations;
- retaining a general database handle in the Preview controller;
- adding playback timers or component behavior to the bridge or renderer.
