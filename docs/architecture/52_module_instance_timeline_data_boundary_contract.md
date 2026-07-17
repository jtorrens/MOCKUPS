# Module Instance Timeline Data Boundary Contract

Status: normative.

This document governs the data boundary used by the common Module Instance and
Shot timeline. It extends contracts 29, 31, 34, 36, 41, 47, 48 and 51 without
changing any temporal formula or persisted animation document.

## 1. Objective

Timeline calculation has an explicit read boundary:

```text
validated current Module Instance and ordered Shot slots
→ ModuleInstanceTimelineDataSource
→ current timeline source documents and stable slot ids
→ ModuleInstanceTimeline
→ duration, Screen origin and keyframe projections
→ editor and Preview consumers
```

The data source supplies exact current inputs. The timeline owns every temporal
calculation.

## 2. Data-source ownership

`ModuleInstanceTimelineDataSource` may read only:

- the complete current Module Instance row needed by the timeline;
- its effective Module Variant runtime contract;
- its effective runtime preview values;
- its effective Theme tokens;
- the ordered stable Module Instance ids belonging to a Shot.

It returns one immutable source record and stable id lists. It does not parse
animation semantics, calculate durations, translate frames, edit payloads,
query SQL directly or repair missing current data.

The source composes existing facade/domain operations during the repository
transition. It is not a repository and must contain no table SQL.

## 3. Timeline ownership

`ModuleInstanceTimeline` retains:

- explicit versus calculated Screen duration policy;
- finite action and collection duration evaluation;
- Theme-dependent natural timing;
- ordered Shot duration as the sum of Screen durations;
- Screen start-frame projection inside its Shot;
- owner-relative keyframe projection to Screen-local frames;
- Shot keyframe projection through stable ordered Screen ids.

The timeline must not accept `SpikeDatabase`, open connections, write data or
infer owners from indices, names, types or positions. Collection order is read
only to accumulate explicitly owned Screen extents; stable ids bind every
Screen and track.

## 4. Preserved temporal invariants

- Persisted v2 tracks retain `fieldId`, stable `targetId` and owner-local
  keyframes.
- A Screen presents its local authoring scale while Preview keeps one absolute
  Shot playhead.
- Entity first appearance determines its internal origin and re-entry does not
  restart that internal timeline.
- Parent-owned Enter/Exit Motion follows the parent local timeline.
- Explicit Screen duration remains authoritative and cannot be extended
  silently.
- Calculated duration remains contract-owned.
- Retime-off, reference-duration lanes and session-only authoring horizon keep
  their current behavior.
- Moving or reordering an owner changes effective projection without rewriting
  stored local keyframes.

## 5. Consumers

Preview transport, animation editors, record field presentation and payload
data preparation create or receive one reusable timeline data source for their
working service lifetime. They must not bypass it with direct database reads
when invoking `ModuleInstanceTimeline`.

Production Preview additionally follows contract 57: the controller obtains
ordered stable Screen ids from this same data source while its separate session
data source supplies only Shot fps, owning Shot ids and selected Variant config.

Mutation remains in the existing animation/document owners. This read boundary
does not authorize the timeline data source to persist a duration, keyframe or
derived value.

## 6. Enforcement and tests

Architecture enforcement must verify:

- `ModuleInstanceTimeline` has no `SpikeDatabase` dependency;
- its data source is the sole timeline database boundary and has no SQL;
- Preview, animation and field consumers call the timeline through that source;
- the payload data source composes the same timeline source for Shot slots;
- this contract is linked from `AGENTS.md` and the architecture index.

Animation tests must continue to cover explicit/calculated duration, owner
origins, stable target ids, drag snapping, keyframe moves, retime, re-entry,
sequencing and non-extending fields. Strict database validation and unchanged
database hash remain required.

## 7. Out of scope

This phase does not change the animation editor interaction, fix or redesign
keyframe dragging, add new timing metadata, alter persisted durations or finish
Render Mode. It only removes database access from the timeline calculation
owner.

## 8. Forbidden shortcuts

- exposing the database through the timeline source record;
- moving duration or origin formulas into the data source;
- caching derived duration as persisted truth during a read;
- replacing stable-id slot order with array-index ownership;
- accepting malformed current JSON as an empty timeline;
- creating a second timeline formula in Preview, an editor or a renderer.
