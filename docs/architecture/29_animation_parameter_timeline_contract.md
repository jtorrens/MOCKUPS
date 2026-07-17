# Animation parameter timeline contract

Date: 2026-07-13
Status: **approved contract; implementation in progress**
Base: `main` at `a124622401363117139e4fa23ff77ed360794d5f` (after the UI baseline named by the handoff). `git pull` reported the branch up to date.

This document records the canonical model and the generic owner-timeline refinements approved during implementation.
For the end-to-end application flow and the planned architecture/functionality/
UX audit, see
[Application Architecture, Functional Flows and UX Audit Handoff](32_application_architecture_functional_ux_handoff.md).

## 1. Baseline and audit inventory

The running desktop application was restarted before this audit. Its build started successfully; NuGet also reports the pre-existing high-severity `SQLitePCLRaw.lib.e_sqlite3` advisory, which is unrelated to animation.

The shared worktree intentionally has unrelated local changes, including the desktop database, preview files, `artifacts/`, and `data/window-state.json`. They were not interpreted, modified, staged, or used as animation work.

### Persisted state

`module_instances.animation_json` is the sole owner of shot-specific parameter animation. `content_json` remains the base public runtime inputs and `behavior_json` remains instance behaviour. At the historical baseline recorded by this section, the committed desktop database had three module instances and every `animation_json` value was exactly:

```json
{"schemaVersion":1,"tracks":[]}
```

The schema/default writer at that historical baseline emitted the same v1 empty object. Later sections and contracts 31/32 describe the implemented v2 writer/editor and supersede this inventory.

### Readers and duration writers

| Location | Current responsibility | Audit result |
| --- | --- | --- |
| `Common/RuntimeTimeline.cs` | module duration | reads `tracks[].events[]` as `startFrame + durationFrames`; this is inconsistent with every parameter-keyframe contract |
| `EditorShell/ModuleInstanceTimeline.cs` | module/Shot duration | delegates to `RuntimeTimeline`; Shot is the sequential sum of slots |
| `DesignPreviewPayloadFactory.cs` | Production payload/frame ownership | selects the active slot from global Shot frame and provides its local frame plus raw `animation_json` |
| `EditorPreviewController.cs` | Production transport | owns the one global Shot cursor shared by Preview and Animation; tree selection chooses full Shot or isolated Screen payload |
| `ComponentInputsPanel.cs` | Design action playback | owns declarative, isolated action frames; it must not become a Production timeline reader |
| legacy `schemas/animation.ts` | evidence schema | uses `tracks[].keyframes[]`, `parameterId`, optional `itemId`, `frame`, `value`, optional `enabled` |
| legacy `resolveChatScreen.ts` | evidence resolver | sorts keyframes, gives exact frame priority, then uses the destination keyframe interpolation; text algorithm is useful but is not grapheme safe |

`RuntimeTimeline` also calculates declared collection/action durations and item-action endpoints. Those remain legitimate duration sources, but its animation-specific `events` reader is not canonical and must be removed in the later migration. The current fallback `duration_frames` is an existing stored minimum, not an independent animation clock.

### Existing resolved animation and media evidence

- `previewTextRevealHelpers.ts` already has an `Intl.Segmenter` grapheme route (with a conservative fallback) for ordinary Conversation write-on.
- `previewMotionHelpers.ts` resolves Theme motion from elapsed milliseconds; it is a generic helper and must remain free of module names.
- The generic Transition bounds contract keeps the immutable root device Screen
  separate from the current parent frame. `Screen` always translates from the
  physical screen edge; `Parent` translates from the immediate assigned
  container. Nested component payloads may replace their local frame but must
  preserve the root Screen. Parents measure and place children before applying
  Transition, so visual motion bounds never become layout bounds.
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
  "retime": {
    "targetDurationFrames": 145,
    "targets": { "message-42": { "targetDurationFrames": 67 } }
  },
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

Required root properties are `schemaVersion: 2` and `tracks`. Optional `retime.targetDurationFrames` sets the Screen target duration; `retime.targets[targetId].targetDurationFrames` sets one collection owner's target duration. These are positive integer output durations, not user-facing multipliers. A track has a stable non-empty `id`, a stable `fieldId`, optional `targetId`, and `keyframes`. `targetId` identifies a stable collection item owned by the field's declaring contract; it is absent for a top-level field. Neither labels nor array indexes are addressing values. A label is editor-derived metadata and is not persisted in a track.

Each keyframe has a stable non-empty `id`, a non-negative integer `frame`, a typed `value`, one `interpolation`, and `enabled` (default `true` when writing v2). Writers always serialize tracks and keyframes by ascending `frame`, then stable `id`; readers must validate that order rather than quietly reorder it.

There is at most one track for a `(fieldId, targetId)` pair and at most one keyframe at a frame in that track. Duplicate target pairs, duplicate ids, duplicate frames, invalid ids, negative/fractional frames, unknown properties that affect semantics, and invalid values are validation errors. Disabled keyframes remain persisted and visible, but have no effect on resolution or duration. A track containing no enabled keyframes has no temporal effect.

`parameterId` and `itemId` are v1 migration input only. The v2 reader accepts only `fieldId` and `targetId`; there are no aliases or fallback parsing paths.

### Target and type validation

The owning module/component contract publishes generic `FieldDefinition` metadata for every possible target:

```text
fieldId, valueKind, animatable, allowedInterpolations,
targetScope, animationTimeline, collectionTargetRule, unit, optional finite-duration evaluator
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

`animationTimeline` is owner-authored contract metadata, never a user-editable setting. Every top-level field is owned by its Screen; every collection field is owned by the stable item selected by `targetId`. A field origin is either `{ kind: "ownerStart" }` or `{ kind: "fieldCompletion", fieldId, offsetFrames }`. A dependency may reference only another field in the same owner and the dependency graph must be acyclic. Collections separately declare their serial pre-duration and post-duration fields. This keeps collection sequencing, field dependency, animation evaluation and rendering as distinct responsibilities.

A collection target id is addressing, not proof of temporal sequencing.
Collections with spatially concurrent owners declare
`animationTimeline.sequenceItems: false`; all item owner origins then begin at
the Screen origin. Component Stack slots use this mode, while Conversation
messages remain serial. The editor and timeline must read this metadata rather
than infer sequencing from collection order or `targetId` presence.

Every entity follows one temporal ownership rule. Its appearance, disappearance,
activation and selection are authored in the local time of its parent. Its own
fields and keyframes are authored in the entity's local time, whose origin is
its first appearance. Moving or reordering the entity recalculates the effective
parent/Screen frame without rewriting those stored local frames. Re-entry starts
new Enter/Exit Motion in parent time but does not restart the entity's internal
timeline. Stable ids, never collection indices, bind both the entity and its
tracks.

A non-serial child collection whose owner is activated by a stable selector may
declare `animationTimeline.ownerOrigin.kind: "firstMatchingValue"`. The contract
names the source collection, source target id, selector field/value and the child
match value. The common owner timeline then uses the earliest matching selector
keyframe. Later re-entries do not replace that origin. An entity without an
authored first appearance remains at provisional owner-local zero for authoring;
it is not rendered until its parent activation selects it.

`animationTimeline.extendsOwnerDuration` defaults to `true`. When `false`, the field does not move the end used to start the next serial collection item, but its absolute keyframes still extend the owning Screen and remain part of the owner's visual span. Conversation delivery/status fields use this mode: a read receipt can appear after later messages have started. Runtime activation controls distinguish these non-sequencing tracks with a circle; the Animation panel continues to use the shared diamond vocabulary.

Enabling animation for a field creates its track with an enabled keyframe at
owner-local frame `0`, containing the effective base value at that origin and
the field's default interpolation. This origin keyframe is mandatory, enabled
and cannot be deleted. A Screen editor draws it on a Screen-local slider at the
effective Screen frame obtained from that owner origin. The shared production
playhead remains absolute internally, but selected Screen controls display
`shotFrame - screenStartFrame`; a selected Shot displays the complete Shot scale.
Disabling the field animation at its Runtime control removes the complete track
after confirmation. At any frame without an exact keyframe, the editor shows a
hollow diamond; the filled diamond is reserved for an exact keyframe.

## 4. Frames, intervals, and value resolution

Authored parameter frames are non-negative integer frames relative to their field origin. A Screen field with `ownerStart` uses Screen-local frames. A Message `text` field uses its Message owner start, which is calculated after that Message's own delay. A delivery field can instead use text completion as its origin. Changing delay, insertion, reordering, write-on duration or an upstream text track recalculates absolute positions without rewriting stored keyframes. A Screen with `durationFrames = D` resolves exactly `[0, D)`, i.e. `0` through `D - 1`. Shot slots are sequential, so the Production navigator owns `shotFrame` and passes `localFrame = shotFrame - screenStartFrame` to the active resolver.

Retime scales owner-local timing to a requested integer-frame output duration. The implementation derives the factor, maps every action/keyframe through it, and rounds effective actions to integer frames. Authored keyframe frames are never rewritten. For collection owners, the natural visual span includes non-sequencing fields, while the shorter sequencing extent determines when the next item begins; both are scaled by the same owner factor. Screen retime is then applied to the complete resolved Screen span.

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

For a calculated Screen/module slot, every source reports an **exclusive end
frame**. The single duration service returns:

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

The root contract may instead declare:

```json
{
  "animationTimeline": {
    "durationPolicy": "explicit",
    "defaultDurationFrames": 240
  }
}
```

`durationPolicy` defaults structurally to `calculated`. `explicit` requires a
positive default and makes the concrete Module Instance's stored
`duration_frames` authoritative. Animation and finite actions are still resolved
frame by frame but cannot grow or shrink that Screen. A keyframe beyond the
explicit range remains authored and is shown as out of range in Animation; it
does not become visible until the user increases Duration. Lock Screen is the
first explicit-duration module; Conversation remains calculated.

Conversation timing remains Screen-local and serial. For message `i`, `start(i) = sequenceEnd(i-1) + delay(i)`. Text completion is `writeOn(i)` when its active track contains only mandatory KF0, otherwise the last enabled text keyframe. Delivery/status, Playing and Full screen are relative to that text completion. Text, finite Playing and Full screen extend the serial item extent; delivery/status fields do not. Consequently a status may resolve after another Message has begun, while still extending the Screen's visual span. `sequenceEnd(i) = start(i) + sequencingBodyExtent(i) + postWriteOnHold(i)`. A true media keyframe contributes only its finite authored window or until a replacing keyframe, whichever comes first. Disabled keyframes do not contribute.

For the current cut-only model, Shot duration is the sum of ordered Screen durations. The navigator, resolver selection, slider ranges, playback cache, and export all call this service; no control owns a private duration formula. The `+` authoring horizon remains session-only and never edits an explicit Duration.

### Fixed and natural behavior timing

Reusable time-based interactions use the dictionary `BehaviorTiming` value kind. Its persisted value is explicit and keeps both alternatives so switching modes does not discard authored intent:

```json
{
  "mode": "natural",
  "fixedFrames": 30,
  "paceToken": "theme.motion.naturalPace.normal"
}
```

`fixed` resolves directly to the positive integer `fixedFrames`. `natural` resolves a final integer duration from contract metadata, never from editor-specific logic:

```text
round(semanticUnitCount * baseFramesPerUnit * naturalPaceMultiplier)
```

The declaring module owns `semanticUnitCount`, `baseFramesPerUnit`, and the deterministic distribution of its interaction inside the resolved duration. The generic owner timeline consumes only the resulting frame duration. The Theme owns five numeric pace multipliers: `verySlow = 2`, `slow = 1.5`, `normal = 1`, `fast = 0.8`, and `veryFast = 0.6`. Values greater than one deliberately make an interaction longer. These are Theme motion tokens, not animation tracks or renderer timers.

The dictionary keeps `Duration (frames)` visible in both modes. It is editable in Fixed mode and read-only in Natural mode, where it displays the live derived value from the same shared resolver used by the owner timeline. The calculated number is not persisted and changes when its semantic source, module rate, selected pace token, or effective Theme changes.

Conversation Write On is the first consumer. Its semantic unit is a grapheme, its base rate is 7 frames per grapheme, and its module resolver produces a deterministic, monotonic typing plan with small contextual variations and no corrections. It always reaches the complete text at the resolved final frame. Password uses the same generic value kind with a component-owned rate of 4 frames per digit and a deterministic keypad sequence, without adding a special case to the dictionary, bridge or renderer.

Generic Design Preview actions may name a `BehaviorTiming` runtime input as
their finite duration source. The action host calls the same shared resolver
used by owner timelines; it does not inspect the owning component or reproduce
its unit/rate formula. The component still owns all state distribution inside
the resulting duration. Password applies that resolved duration to PIN,
fingerprint, face-recognition and draw-pattern sequences; every child receives
only its final state/progress for the requested frame.

The same action contract is resolved in Production without a second playback
mechanism. A keyframe track addresses the promoted play field by its stable
forwarded `fieldId`; forwarding retains an explicit stable-id-to-child-json-key
map until the embedded runtime values have been resolved. The common owner
timeline evaluates that track in the concrete collection item's local time,
derives elapsed action time from the activating source keyframe, applies the
declared finite duration and completion behavior, and only then invokes the
child resolver. Field labels and JSON storage keys are never animation
identities, and the renderer never advances the action clock.

Recursive component composition preserves the root effective runtime contract
separately from each child's local runtime values. Replacing the child payload
must therefore never replace the owner timeline contract: a KF0 inside a State
still resolves at that State's first appearance, not at Screen frame zero. This
rule applies uniformly to every embedded component and collection depth.

Finite Design Preview actions also declare a generic completion behavior.
`reset` switches the action off after its final frame and is declared explicitly
for momentary interactions. `holdFinal` retains the action input and its final
time value after presentation; replay starts again at frame zero and Reset Test
Values restores defaults. Password uses `holdFinal` so its correct or incorrect
result remains visible. This policy belongs to the action host and contains no
component-specific branch.

An action may additionally declare a generic target input. `toggle` actions
invert a boolean baseline; `option` actions select one explicit destination.
The action host snapshots the target, trigger, time and activation fields before
the first run, applies the destination before resolving frame zero, and owns
per-action Restore. A `targetFromJsonKey` may expose the captured source state
to the owning resolver for bidirectional Reflow. The component resolver still
owns every intermediate frame; neither the editor, bridge nor renderer infers
component transitions.

## 8. Context, resolver, and presentation boundaries

Production owns one global Shot cursor and one playback owner. The selected tree
node is its sole context: Shot renders the complete sequence and Module Instance
renders that isolated Screen. Shot selection presents the complete Shot scale;
Module Instance selection presents `0..Screen duration - 1`. Both read/write the
same absolute cursor internally, and the Screen boundary translates between its
local display frame and `screenStartFrame + localFrame`. Preview navigation never
changes tree selection, and no scope combo or Production context lock may create
a second context. Design actions are isolated fixtures: their action frame and
Test Values do not read Shot navigation and are never persisted into Production
animation.

```text
ResolvedFrameRequest {
  source: designAction | productionShot,
  shotId?, screenId?, globalShotFrame?, localScreenFrame,
  effectiveFps, device/theme/mode, runtimeSignature, animationSignature
}

resolve(request) -> fully resolved owner state -> standard renderable atoms
```

The resolver evaluates typed animation and module semantics at the requested frame. Conversation applies its field/target mapping and message-relative origins in its owning frame resolver before its renderable is called. Component renderables emit standard atoms. Common helpers may convert frame deltas to milliseconds and tokens to final values. The bridge and HTML/raster renderer paint only those resolved atoms; none may branch on Conversation, Bubble, Media, Audio, Keyboard, or a field name, or run a timer.

Runtime size or position changes of one logical element retain stable renderable
ids and use Theme Reflow by default. The owner resolves complete source and
destination trees, then recursively interpolates matching boxes for each
requested frame; parent layout and owned child geometry therefore move on the
same clock. A contract may explicitly choose another transition or `none`.
Static Variant/Test Value edits without a runtime source frame update directly
and do not invent a transition. The renderer never performs this interpolation.

Module instances select an explicit full Module Variant reference. Timeline
discovery, initial keyframes and duration consume the effective contract formed
from that Variant config and the module's shared runtime declarations. Changing
Variant removes runtime values and tracks whose fields or stable targets no
longer exist. The timeline never falls back to class config or infers a Variant
from Actor, Device or Theme.

Component Stack State changes use the same v2 contract. `runtimeStateId` is the
`fieldId`; the slot's stable id is `targetId`, and keyframe values are stable
State ids. Slots are independent owners of their visible sets, so a track for
one slot never selects an index in another slot. At a change frame, outgoing
Exit Motion, incoming Enter Motion and any
container Reflow share one action origin; total transition time is their maximum
finite duration, not a serial sum. Entering child-local time starts at zero,
outgoing child content holds its final internal state, and the resolver emits
only the complete requested-frame result. Design Test Values option actions are
transient fixtures for this same resolution contract, not persisted animation.

The structural ownership and Module Instance mapping are specified in
[Structural Stacks, Slots, States and Module Instances](31_structural_stacks_slots_and_module_instances.md).

The sequence provider accepts either Design action frames or Production frames, then shares preparation and presentation. Starting either source cancels the other; navigation, context, route, active Screen, FPS, asset/font, runtime or animation changes cancel stale preparation. Cache identity includes source, Shot id, Screen id, global and local frame, effective FPS, device/theme/mode, runtime and animation signatures, asset/font signatures, route, and geometry.

## 9. Migration

The seed, fixtures and committed desktop database migrate in one explicit step to v2. `RuntimeTimeline` delegates only to the generic owner timeline; the retired event/legacy duration implementation is removed rather than retained behind a fallback. Persisted v1 data after migration is invalid. Database and parity assets ship in the same implementation commit.

The audited committed data is empty, so step 2 presently has no event or keyframe payload requiring semantic conversion. Any non-empty local/user data encountered at implementation time must be explicitly reported and migrated; it must not be silently reinterpreted.

## 10. Editor scope after approval

The Production Animation surface is bound bidirectionally to the authoritative
Preview Shot playhead. Moving or playing either transport updates the other;
neither owns an alternate cursor. A Runtime Value with an active track is
read-only on the Runtime Values surface and displays the value resolved at the
current Shot playhead after translating it through its Screen and owner origin.
The displayed value is presentation-only: it never overwrites the instance
payload, and keyframe editing remains in Animation. Creating, editing or
removing a keyframe persists the v2 track, refreshes the resolved current frame
and rebuilds only that local Animation surface. It must not navigate or
reconstruct the containing editor, so its open cards, selected internal
tab/track, scroll offset, current frame and session-only horizon remain stable.
Screen-owned tracks live in an `Animation`
subcard below the fields in Runtime Values `General`. Collection tracks normally live inside their owning item. A collection may instead declare the generic `collectionFooter` animation presentation; its direct item tracks are then aggregated in one `Animation` card below the complete collection, while nested runtime contracts retain their own local cards. Each panel presents the containing Screen's local frame scale and translates every selected stable target at its own owner boundary through the generic owner timeline; persisted keyframes remain authored in owner-local frames and therefore survive delay changes, insertion, reordering and retime unchanged. The authoring horizon starts at the complete Screen duration and always includes the selected duration reference. A `+` beside the slider extends its right edge by ten session-only frames at a time; the provisional limit is shown muted in parentheses. Under a calculated duration contract, an authored keyframe may contribute to the next calculated extent. Under an explicit duration contract, it remains marked out of range until the user increases the Screen Duration. The list contains only active properties, while all active properties remain visible when another is selected.

Retime is an explicit persisted switch represented by the presence of `targetDurationFrames`. Off means no retime override and hides the target-duration input. Turning it on initializes the target from the generic natural/reference duration and reveals the editable frame target; turning it off removes the override. The editor never presents an effective keyframe-shortened span as the natural duration of a contract-declared behavior.

The compact transport and mini-timeline use the shared keyframe glyphs and one Preview playback owner. The selected property always occupies an 18 px lane. When its contract declares a base or finite action duration, a pale blue reference band fills `[fieldOrigin, fieldOrigin + referenceDuration)` behind the keyframes; a property without a duration provider keeps the same empty lane. The band is contract-derived and generic: Fixed/Natural Write On and a declared finite Playback Duration use the same mechanism. It remains visible when two or more keyframes override the base completion, so shortening or extending the reference is directly legible. The exact nonzero keyframe diamond removes that keyframe; a hollow diamond creates one; mandatory KF0 cannot be removed or dragged.

Every other mini-timeline diamond is draggable on the containing Screen authoring scale. Ordinary drag snaps to five-Screen-frame increments; `Alt` drag snaps to every Screen frame. Existing keyframes provide a stronger soft detent even when they are outside that grid. During drag the shared Preview cursor follows the candidate frame, but the database is written only once on a valid release. `Escape`, capture loss, an owner-local frame below one, or a frame already occupied in the same track restores the original position without a write. The editor converts the accepted Screen position back through the target's owner timeline and persists that owner-local integer frame; keyframe id, value, interpolation and enabled state remain unchanged. Thus drag remains stable across entity delay, reorder and retime and does not introduce a second origin formula.

The synchronous UI feedback boundary that keeps the captured marker stable
while the shared Preview follows is specified by
[Animation Keyframe Drag Interaction](62_animation_keyframe_drag_interaction_contract.md).

Slider dragging uses a shared soft magnetic detent at keyframes without blocking continued movement. Preview aggregates enabled keyframes from every header and collection owner into Screen/Shot coordinates, disables unavailable directions and marks exact-keyframe parking with an amber Play/Pause border. Runtime field activation uses diamonds for sequencing tracks and circles for `extendsOwnerDuration: false` tracks; this distinction is not repeated inside Animation.

It does not introduce a canvas/dope sheet, custom `MainWindow` behavior, component-specific controls, an independent playhead, or a private duration calculation. Empty/invalid/orphaned states are explicit. Curves, color/geometry animation, cross-Screen transitions, arbitrary scripted events, and unbounded/live media are deferred.

## 11. Test and performance acceptance matrix

| Area | Required checks |
| --- | --- |
| schema | v2 acceptance; reject v1 aliases, duplicate targets/frames/ids, invalid values, orphaned target ids |
| boundaries | base-before-first; exact keyframe; every interpolation interior frame; final hold; duration `D` resolves only `0..D-1` |
| numeric/text | hold/linear/easeInOut; deletion/rewrite; same value; emoji, combining mark, newline, interval of one frame |
| media | play-once/loop, finite end, early off, source shorter than window, unknown source duration, no infinite loop |
| duration | grow/shrink after edits, concurrent max not sum, Screen-to-Shot sum, screen boundary navigation; Fixed/Natural timing and pace multiplication |
| ownership | Design Test Values never affect Production; one playback owner; cancelled/stale work cannot commit |

### Automated animation suite

`npm run animation:test` is the focused autonomous suite and `npm test` includes it. It does not read or modify `data/desktop-editor-spike.sqlite`.

- TypeScript frame tests cover missing/disabled tracks, exact `[start, end)` boundaries, final hold, destination-owned hold/linear/ease-in-out/write-on interpolation, Unicode grapheme rewrite, exact `fieldId`/`targetId` isolation, Screen fields, post-delay message-relative origins, delay changes without keyframe rewrites, insertion/reordering, chain extension by keyframes and finite media, non-animable actor/direction, delivery/full-screen fields, finite media playing, Natural Pace multiplication, and deterministic monotonic natural Write On cadence.
- The .NET contract runner covers strict v2 document shape, activation with a frame-zero keyframe, target persistence and round-trip, ordered upsert/removal/move, protected KF0 and occupied destinations, Screen-grid/precise drag snapping, Screen and target origins, animated/media-driven target chaining, duration endpoints and composition, finite media action duration, duplicate target/frame rejection, negative-frame rejection, initial-keyframe normalization, explicit rejection of legacy events, the initially authorized animatable vocabulary, shared playback state notifications, and grapheme-based `BehaviorTiming` resolution through Theme pace tokens.
- `npm run check:architecture` covers editor placement, active-track-only lists, target-scoped session state, dictionary interpolation, diamond-first standard transport order, Preview keyframe aggregation and scope filtering, delegation to the single Preview playback owner, absence of an editor timer, resolver ownership, and renderer/bridge boundaries.

Visual density, alignment, truncation and responsive layout remain a short human review in the running application; they are not treated as semantic correctness tests.
| routes | identical resolved frames for HTML priority, HTML every-frame and raster every-frame; no renderer timer |
| architecture | `npm run check:architecture` prevents concrete names/imports in bridge, common helpers, renderer |

Performance targets retain the static-preview contract: at 25 fps, median end-to-end frame preparation no more than 25 ms, p95 no more than 40 ms, no late-frame replay, and cache work cancellable by the identity above. Resolver evaluation must be pure: any requested frame can be resolved independently.

## 12. Approved decisions

The implemented baseline is keyframes-only v2, strict `fieldId`/`targetId`, destination-owned interpolation, half-open intervals, mandatory KF0, grapheme-safe `writeOn`, finite audio/video, one derived Screen duration, generic owner/field-completion origins, non-sequencing late fields, integer target-duration retime, and dictionary-backed Fixed/Natural behavior timing. Natural cadence remains module-owned; future interaction types remain contract additions rather than editor, bridge, or renderer exceptions.
