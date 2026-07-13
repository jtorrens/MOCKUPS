# Animation parameter timeline contract

Date: 2026-07-13
Status: **approved contract; phase 2 in progress**
Base: `main` at `a124622401363117139e4fa23ff77ed360794d5f` (after the UI baseline named by the handoff). `git pull` reported the branch up to date.

This document specifies the proposed canonical model before an implementation, data migration, resolver, duration-service replacement, or animation editor is started. It deliberately does not authorize a visual timeline.

## 1. Baseline and audit inventory

The running desktop application was restarted before this audit. Its build started successfully; NuGet also reports the pre-existing high-severity `SQLitePCLRaw.lib.e_sqlite3` advisory, which is unrelated to animation.

The shared worktree intentionally has unrelated local changes, including the desktop database, preview files, `artifacts/`, and `data/window-state.json`. They were not interpreted, modified, staged, or used as animation work.

### Persisted state

`module_instances.animation_json` is the sole owner of shot-specific parameter animation. `content_json` remains the base public runtime inputs and `behavior_json` remains instance behaviour. The committed desktop database has three module instances and every `animation_json` value is exactly:

```json
{"schemaVersion":1,"tracks":[]}
```

The current schema/default writer emits that same v1 empty object. There is no active writer/editor for parameter tracks.

### Readers and duration writers

| Location | Current responsibility | Audit result |
| --- | --- | --- |
| `Common/RuntimeTimeline.cs` | module duration | reads `tracks[].events[]` as `startFrame + durationFrames`; this is inconsistent with every parameter-keyframe contract |
| `EditorShell/ModuleInstanceTimeline.cs` | module/Shot duration | delegates to `RuntimeTimeline`; Shot is the sequential sum of slots |
| `DesignPreviewPayloadFactory.cs` | Production payload/frame ownership | selects the active slot from global Shot frame and provides its local frame plus raw `animation_json` |
| `EditorPreviewController.cs` | Production transport | owns the one global Shot cursor; Screen scope translates it to the active slot range |
| `ComponentInputsPanel.cs` | Design action playback | owns declarative, isolated action frames; it must not become a Production timeline reader |
| legacy `schemas/animation.ts` | evidence schema | uses `tracks[].keyframes[]`, `parameterId`, optional `itemId`, `frame`, `value`, optional `enabled` |
| legacy `resolveChatScreen.ts` | evidence resolver | sorts keyframes, gives exact frame priority, then uses the destination keyframe interpolation; text algorithm is useful but is not grapheme safe |

`RuntimeTimeline` also calculates declared collection/action durations and item-action endpoints. Those remain legitimate duration sources, but its animation-specific `events` reader is not canonical and must be removed in the later migration. The current fallback `duration_frames` is an existing stored minimum, not an independent animation clock.

### Existing resolved animation and media evidence

- `previewTextRevealHelpers.ts` already has an `Intl.Segmenter` grapheme route (with a conservative fallback) for ordinary Conversation write-on.
- `previewMotionHelpers.ts` resolves Theme motion from elapsed milliseconds; it is a generic helper and must remain free of module names.
- Text Box currently consumes `textAnimationElapsedMs` for its own visual effect. Conversation derives its keyboard/text-input state from frame data. Neither renderer may acquire a timer for parameter animation.
- Media and Audio contracts already express physical position/duration in seconds. They do not presently define a canonical finite parameter-playback value. That is supplied below as a contract-owned field type, not a renderer event.

The current preview routes remain: `HTML · Priority FPS`, `HTML · Every frame`, and `Raster · Every frame`. They must request the same resolved frame; route policy may affect presentation/preparation only.

## 2. Decision: keyframes, not generic events

`animation_json` v2 contains **parameter tracks with keyframes only**. `tracks[].events[]`, `startFrame`, and `durationFrames` are retired. They are not accepted by v2 readers and are not silently translated at read time.

An event is a useful product word for a finite module action, but it is not a second generic persisted track grammar. A finite action is represented by the owning contract's declared typed runtime field and its keyframed value. For example, a module can expose an animatable `mediaPlayback` field whose `on` value includes a finite `windowFrames`; it can later be set to `off`. The generic evaluator validates and holds the typed value; only the owning media or Conversation resolver interprets playback semantics.

This prevents two incompatible schedulers and makes the duration service able to ask field contracts for finite endpoints. Reusable Theme motion presets are separate from parameter tracks and are not stored here.

## 3. Canonical `animation_json` v2

```json
{
  "schemaVersion": 2,
  "tracks": [
    {
      "id": "track-message-42-text",
      "fieldId": "text",
      "targetId": "message-42",
      "keyframes": [
        { "id": "kf-message-42-text-000", "frame": 0, "value": "Hola Jorge", "interpolation": "hold", "enabled": true },
        { "id": "kf-message-42-text-036", "frame": 36, "value": "Hola Teresa", "interpolation": "writeOn", "enabled": true }
      ]
    }
  ]
}
```

Required root properties are `schemaVersion: 2` and `tracks`. A track has a stable non-empty `id`, a stable `fieldId`, optional `targetId`, and `keyframes`. `targetId` identifies a stable collection item owned by the field's declaring contract; it is absent for a top-level field. Neither labels nor array indexes are addressing values. A label is editor-derived metadata and is not persisted in a track.

Each keyframe has a stable non-empty `id`, a non-negative integer `frame`, a typed `value`, one `interpolation`, and `enabled` (default `true` when writing v2). Writers always serialize tracks and keyframes by ascending `frame`, then stable `id`; readers must validate that order rather than quietly reorder it.

There is at most one track for a `(fieldId, targetId)` pair and at most one keyframe at a frame in that track. Duplicate target pairs, duplicate ids, duplicate frames, invalid ids, negative/fractional frames, unknown properties that affect semantics, and invalid values are validation errors. Disabled keyframes remain persisted and visible, but have no effect on resolution or duration. A track containing no enabled keyframes has no temporal effect.

`parameterId` and `itemId` are v1 migration input only. The v2 reader accepts only `fieldId` and `targetId`; there are no aliases or fallback parsing paths.

### Target and type validation

The owning module/component contract publishes generic `FieldDefinition` metadata for every possible target:

```text
fieldId, valueKind, animatable, allowedInterpolations,
targetScope, collectionTargetRule, unit, optional finite-duration evaluator
```

Validation resolves `fieldId` against that metadata, checks that the target is allowed, verifies a collection `targetId` exists and is stable, and validates the keyframe value with the same `ValueKind` validation used by the dictionary editor. Calculated/internal fields are not targets unless the owning contract explicitly publishes them as safe. The editor never constructs its own target catalogue or value controls.

The initial interpolation matrix is intentionally small:

| Value kind | Allowed interpolation |
| --- | --- |
| text/string | `hold`, `writeOn` |
| boolean, enum, reference, delivery status, media source | `hold` |
| integer/number | `hold`, `linear`, `easeInOut` |
| object/collection | none as a whole; declared scalar children only |
| color, geometry, transform | deferred until an owning contract declares it |

`writeOn` is a derived text segment rule, not a sequence of persisted character events. `easeInOut` is the single initial numeric easing and is deterministic (smoothstep `p²(3−2p)`); it is never delegated to CSS.

## 4. Frames, intervals, and value resolution

All authored parameter frames are Screen-local integer frames. A Screen with `durationFrames = D` resolves exactly `[0, D)`, i.e. `0` through `D - 1`. Shot slots are sequential, so the Production navigator owns `shotFrame` and passes `localFrame = shotFrame - screenStartFrame` to the active resolver.

For an enabled keyframe pair `A` at `a` and destination `B` at `b`, where `a < b`, the segment is `[a, b)`. `B.value` is effective exactly at `b`; the segment therefore never has to approximate its target. `B.interpolation` owns the segment from `A` to `B`:

- before the first enabled keyframe, the base `content_json`/`behavior_json` value remains effective;
- at a keyframe its stored value wins exactly;
- `hold` holds `A.value` throughout `[a, b)`;
- numeric `linear` and `easeInOut` use `p = (frame - a) / (b - a)` for frames strictly inside the interval;
- text `writeOn` uses the algorithm below for frames strictly inside the interval;
- after the final enabled keyframe, its value holds.

The first keyframe's interpolation is stored for a uniform schema but has no preceding segment and is ignored. The final keyframe is a state at one frame, therefore its ordinary duration endpoint is `frame + 1`. This same exclusive endpoint convention applies to every temporal source.

## 5. Grapheme-safe text rewrite

For a `writeOn` segment, the resolver splits the source and destination into grapheme clusters using the shared production shaping/grapheme utility (the existing `Intl.Segmenter` route is suitable when it remains the shared utility), not UTF-16 units. It finds the longest common prefix, then makes `N` operations: all removals from the old suffix followed by all appends from the new suffix.

At each interior frame `f`, use `step = floor(N * (f-a)/(b-a))`, constrained to `0..N`. The source is returned at `a`, the exact destination is returned at `b`; thus an interval too short to visibly show every operation remains deterministic without creating extra frames. A resolver also may emit generic edit diagnostics: visible text, edit direction, changed grapheme, normalized pressed-key meaning, and cursor/typing progress. Conversation alone consumes those diagnostics to synchronize Keyboard and Text Input Bar.

This replaces the legacy `Array.from` / rounded-tail evidence algorithm, which is not sufficient for combined graphemes and does not define the boundary policy above.

## 6. Finite audio/video playback

Media source position is seconds; activation and window boundaries are frames. An owning contract may declare an animatable finite-playback field with this typed value shape:

```json
{ "state": "on", "mode": "playOnce", "windowFrames": 75, "sourceOffsetSeconds": 0 }
```

`state` is `on` or `off`; `mode` is `playOnce` or `loop`; `windowFrames` is a positive integer whenever state is `on`; `sourceOffsetSeconds` is a non-negative physical offset. This value belongs to the owner field's `ValueKind`, not to a generic renderer schema. A later `off` keyframe ends the window early. An `on` keyframe starts `[frame, frame + windowFrames)`; a following `on` replaces the preceding state at its exact frame.

For a requested frame, the resolver calculates `elapsedSeconds = (localFrame - startFrame) / effectiveFps` and then applies the offset. `playOnce` is active only until the earlier of its authored exclusive window and physical source duration; `loop` wraps physical source position but still stops at its finite authored window. A missing/unknown media duration is a validation/preparation diagnostic, not permission for an unbounded loop. Image attachments have no playback field/window.

If a future editor permits a media window authored in seconds, it converts once at the owner boundary using `ceil(seconds * effectiveFps)` to a positive `windowFrames`; the persisted parameter animation remains frame based.

## 7. Canonical duration service

For a Screen/module slot, every source reports an **exclusive end frame**. The single duration service returns:

```text
max(1,
    explicitStoredMinimum,
    declaredModuleSequenceEnd,
    ConversationSequenceEnd,
    lastEnabledKeyframe.frame + 1,
    each contract-owned finite-playback end,
    each contract-owned finite visual-motion end)
```

Concurrent sources are maximized, never summed. A Screen ends at that maximum; the last valid frame is `durationFrames - 1`. A finite media end is clamped by source duration after converting the source duration with the same `ceil(s * fps)` convention. A loop can therefore never create infinite duration.

Conversation timing remains Screen-local and sequential: each message start is derived from preceding delay/write-on/hold fields. Parameter tracks remain Screen-local even when targeted at a message; the Conversation resolver derives message-local elapsed time from that message's calculated appearance frame.

For the current cut-only model, Shot duration is the sum of ordered Screen durations. The navigator, resolver selection, slider ranges, playback cache, and export all call this service; no control owns a private duration formula.

## 8. Context, resolver, and presentation boundaries

Production owns one global Shot cursor and one playback owner. Its Screen scope only changes the displayed range; it never creates an independent clock. Design actions are isolated fixtures: their action frame and Test Values do not read Shot navigation and are never persisted into Production animation.

```text
ResolvedFrameRequest {
  source: designAction | productionShot,
  shotId?, screenId?, globalShotFrame?, localScreenFrame,
  effectiveFps, device/theme/mode, runtimeSignature, animationSignature
}

resolve(request) -> fully resolved owner state -> standard renderable atoms
```

The resolver evaluates typed animation and module semantics at the requested frame. Component renderables emit standard atoms. Common helpers may convert frame deltas to milliseconds and tokens to final values. The bridge and HTML/raster renderer paint only those resolved atoms; none may branch on Conversation, Bubble, Media, Audio, Keyboard, or a field name, or run a timer.

The sequence provider accepts either Design action frames or Production frames, then shares preparation and presentation. Starting either source cancels the other; navigation, context, route, active Screen, FPS, asset/font, runtime or animation changes cancel stale preparation. Cache identity includes source, Shot id, Screen id, global and local frame, effective FPS, device/theme/mode, runtime and animation signatures, asset/font signatures, route, and geometry.

## 9. Migration plan (later phase)

1. Add strict v2 schema/typed target validation and a database scan.
2. Migrate every seed, fixture, example, layout, and committed desktop database in one transaction: rename `parameterId` to `fieldId`, `itemId` to `targetId`, assign stable missing ids, and reject/explicitly transform any legacy `events` before the release.
3. Replace the default writer with v2 and replace `RuntimeTimeline`'s event reader with the canonical duration service.
4. Remove all v1 readers, aliases, and retired event fields. Persisted v1 data after migration is invalid, not a compatibility input.
5. Commit the changed desktop database and any parity assets/data in the same implementation commit.

The audited committed data is empty, so step 2 presently has no event or keyframe payload requiring semantic conversion. Any non-empty local/user data encountered at implementation time must be explicitly reported and migrated; it must not be silently reinterpreted.

## 10. Editor scope after approval

The first Production Animation surface is a generic Screen editor bound to the existing authoritative playhead. It lists declared tracks and keyframe cards, uses `FieldDefinition` plus registered dictionary controls for values, filters interpolation by the target metadata, and gives explicit add/update/move/clone/enable/delete actions. Runtime Values keep base data; Animation keeps overrides.

It does not introduce a canvas/dope sheet, custom `MainWindow` behavior, component-specific controls, an independent playhead, or a private duration calculation. Empty/invalid/orphaned states are explicit. Curves, color/geometry animation, cross-Screen transitions, arbitrary scripted events, and unbounded/live media are deferred.

## 11. Test and performance acceptance matrix

| Area | Required checks |
| --- | --- |
| schema | v2 acceptance; reject v1 aliases, duplicate targets/frames/ids, invalid values, orphaned target ids |
| boundaries | base-before-first; exact keyframe; every interpolation interior frame; final hold; duration `D` resolves only `0..D-1` |
| numeric/text | hold/linear/easeInOut; deletion/rewrite; same value; emoji, combining mark, newline, interval of one frame |
| media | play-once/loop, finite end, early off, source shorter than window, unknown source duration, no infinite loop |
| duration | grow/shrink after edits, concurrent max not sum, Screen-to-Shot sum, screen boundary navigation |
| ownership | Design Test Values never affect Production; one playback owner; cancelled/stale work cannot commit |
| routes | identical resolved frames for HTML priority, HTML every-frame and raster every-frame; no renderer timer |
| architecture | `npm run check:architecture` prevents concrete names/imports in bridge, common helpers, renderer |

Performance targets retain the static-preview contract: at 25 fps, median end-to-end frame preparation no more than 25 ms, p95 no more than 40 ms, no late-frame replay, and cache work cancellable by the identity above. Resolver evaluation must be pure: any requested frame can be resolved independently.

## 12. Product approvals required before phase 2

1. Approve v2 as keyframes-only and retire generic `events`.
2. Approve `fieldId`/`targetId`, destination-owned interpolation, strict unique frame policy, and the half-open `[start, end)` convention.
3. Approve the initial interpolation set, particularly the canonical `easeInOut` name and `writeOn` text semantics.
4. Approve finite playback as a contract-owned typed field with frame windows, rather than a global event model; confirm whether `sourceOffsetSeconds` is needed in the first release.
5. Confirm the status of `duration_frames`: it is proposed as an explicit stored minimum while the derived canonical duration is recalculated. If it should instead be removed as redundant, that requires a separate schema migration decision.
6. Confirm which initial runtime fields each owning module exposes as animatable. The architecture does not infer them from existing UI fields.
