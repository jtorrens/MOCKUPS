# Animation System Architecture and Roadmap Handoff

Date: 2026-07-13

Status: preparation handoff. The animation thread must begin with an audit and a
canonical contract proposal. This document does not authorize immediate timeline
UI implementation.

## Objective

Design and implement one deterministic animation system for the desktop editor,
Production preview and final rendering. It must cover:

- per-frame changes to Module Instance runtime parameters;
- text deletion and rewriting between ordinary text keyframes;
- finite audio and video playback windows;
- reusable visual motion driven by Theme motion tokens;
- automatic Screen and Shot duration calculation;
- Design Test Values actions and Production Shot/Screen navigation;
- HTML priority, HTML every-frame and raster every-frame preview routes;
- deterministic export where any requested frame can be resolved independently.

Do not start by building a visual timeline. First close the persisted schema,
typed interpolation rules, frame ownership and pure resolver behavior. The editor
must manipulate the canonical model; it must not become a second animation
engine.

## Baseline coordination

The completed UX/UI phase is published on `main` at commit `b1fc05eb`
(`feat(desktop-editor): apply semantic icon system`). The animation thread must:

1. run `git pull` and confirm it starts at `b1fc05eb` or a later explicitly
   approved commit;
2. confirm a clean tracked working tree in its own thread/worktree;
3. restart the desktop application to clear resident layouts, WebViews and
   preview processes;
4. record the actual base commit in its first architecture document.

The shared root still contains later local files/changes that are not part of
this handoff. Do not stage, restore, rewrite or interpret them as animation work.
Preserve `artifacts/` and `data/window-state.json` unless the user explicitly
directs otherwise.

## Required reading

Read completely before proposing or changing the contract:

- `AGENTS.md`;
- `docs/architecture/editor_shell_non_negotiables.md`;
- `docs/architecture/editor_modernization_roadmap.md`;
- `docs/architecture/24_desktop_preview_component_architecture.md`;
- `docs/architecture/25_component_migration_status.md`;
- `docs/architecture/26_desktop_preview_pipeline.md`;
- `docs/architecture/26_shot_module_instance_contract.md`;
- `docs/architecture/27_time_units_contract.md`;
- `docs/architecture/27_design_production_ux_separation.md`;
- `docs/architecture/28_static_preview_update_experience.md`;
- `docs/architecture/08_visual_tokens_layout_contract.md`;
- `docs/architecture/15_target_system_architecture.md`, especially the animation
  and text-keyframe section;
- `docs/architecture/component_behavior/open_items.md`;
- `docs/exchange/tasks/0020_app_screen_token_simplification.md`, especially the
  `animation_json` ownership sections;
- `docs/exchange/codex_handoffs/2026-07-12_resident_static_preview_and_shot_playback_handoff.md`;
- the final UX/UI audit and implementation handoff, so new animation UI follows
  the accepted shared primitives rather than reintroducing custom chrome.

Inspect these implementation sources before deciding anything:

- `spikes/desktop-editor-shell/Common/RuntimeTimeline.cs`;
- `spikes/desktop-editor-shell/EditorShell/ModuleInstanceTimeline.cs`;
- `spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs`;
- `spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs`;
- `spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs`;
- `src/desktop-preview/previewMotionHelpers.ts`;
- `src/desktop-preview/previewTextRevealHelpers.ts`;
- `src/desktop-preview/textBoxComponentResolver.ts`;
- `src/desktop-preview/textBoxComponentRenderable.ts`;
- Conversation, Bubble, Media, Audio, Keyboard and Text Input Bar contracts,
  resolvers and renderables;
- `archive/react-legacy/src/domain/schemas/animation.ts`;
- the animation helpers around `interpolateTextByTail`,
  `animatedValueForAnimationTrack` and `animatedChatMessage` in
  `archive/react-legacy/src/domain/resolvers/resolveChatScreen.ts`.

The React implementation is evidence and a useful algorithmic reference, not
automatically the final schema.

## Non-negotiable architecture

### Resolver ownership

Animation is resolved frame data:

```text
base runtime payload
  + animation tracks
  + requested local frame
  + effective FPS/time conversions
  -> owning module/component resolver
  -> fully resolved component state for that frame
  -> renderable atoms
  -> generic renderer
```

- The resolver owns the value/state at the requested frame.
- The renderer paints the resolved frame.
- The WebView must not run component animations, CSS timelines, countdowns or
  interpolation timers.
- Avalonia may coordinate the clock, preparation, caches and presentation, but
  must not calculate component-specific animation state.
- The central bridge and common preview helpers must not branch on a concrete
  component/module type.
- Component-specific interpretation remains inside the owning resolver.
- Export and preview must call the same resolver contract.

### Editor ownership

- `MainWindow` remains shell-only.
- Editable scalar keyframe values use `FieldDefinition`, `ValueKind` and the
  registered dictionary control for the target parameter type.
- A structured track/keyframe collection editor is allowed, but its scalar
  fields still use dictionary definitions/controls.
- No component-specific timeline logic belongs in the shell.
- The animation editor consumes generic animatable-field metadata published by
  module/component contracts.
- Calculated/internal inputs are not animatable unless the owning contract
  explicitly exposes a safe animation target.

### Persistence and migration

- `module_instances.animation_json` owns shot-specific parameter animation.
- `content_json` remains base shot content.
- `behavior_json` remains instance behavior/visibility.
- Theme/App/Module Theme Config remain the reusable visual design layers.
- `animation_presets` are conceptually separate reusable visual
  entrance/exit/motion presets; they are not parameter tracks.
- Schema changes require one explicit migration across seeds, examples, layouts
  and the committed desktop database. Do not retain aliases or dual readers.

## Canonical time domains

The current time-unit contract is authoritative:

| Domain | Unit | Notes |
| --- | --- | --- |
| Shot/Screen timeline, parameter keyframes, authored events and Conversation timing | frames | evaluated using effective Shot FPS |
| Physical audio/video position and source duration | seconds | matches media metadata/decoders |
| Reusable motion, fades, blink and continuous UI effects | milliseconds | independent of project FPS |

Conversions happen at ownership boundaries:

- timeline frame delta → elapsed seconds for media;
- timeline frame delta → elapsed milliseconds for Theme/component motion;
- physical media duration → finite end frame using effective FPS when calculating
  Screen duration.

Do not expose milliseconds or seconds merely because the scheduler internally
uses them. Editors display the canonical unit of the authored property.

## Existing temporal/navigation model to preserve

### Production

- The Production navigator owns one global Shot frame.
- Ordered Screen slots are sequential inside the Shot.
- The active Screen receives a local frame:

```text
local Screen frame = global Shot frame - Screen start frame
```

- `Shot` scope displays `0…Shot duration - 1` using global frames.
- `Screen` scope displays `0…active Screen duration - 1` and translates its
  local value back to the equivalent global Shot frame.
- Start, end and Play operate on the selected scope.
- Previous/next Screen crosses slot boundaries while retaining the user-facing
  term `Screen`, never `ModuleInstance`.
- Changing scope changes the displayed range, not temporal authority or the
  resolved frame.
- Reference video synchronization uses the global Shot frame.
- A stopped slider/frame/Screen change is a static resident update, not playback.
- Static navigation stops playback before selecting its requested frame.

Do not redesign or duplicate the established compact Production transport while
adding animation editing. The animation editor's playhead must bind to this same
authoritative cursor.

### Design

- Design preview uses Test Values and declarative local actions.
- Design action frames are local to that isolated action/module fixture.
- Design must not simulate a Production Shot or read Production navigation.
- Production never applies transient Test Values or Design action clocks.
- Both sources may feed the same generic resolved-frame sequence/presentation
  infrastructure, but their context providers remain separate.

### Playback ownership

- Only one playback owner may be active in a preview.
- Starting Shot/Screen play stops an active Test Values action.
- Starting a Test Values action stops Production play.
- Navigation, context, route or active Screen changes cancel stale preparation.
- On stop/cancel, the selected static frame returns through the resident update
  path.

## Parameter animation model already agreed

The persisted model should remain readable and sparse:

```text
animation_json
  schemaVersion
  tracks[]
    stable track id
    target parameter/field id
    optional stable collection item target id
    keyframes[]
      frame
      value
      interpolation
      optional enabled/metadata
```

Before writing schema v2 or an editor, audit and decide exact canonical names:

- `fieldId` versus legacy `parameterId`;
- `targetId` versus legacy `itemId`;
- whether interpolation is stored on the destination keyframe (the React
  behavior) or represented explicitly as a segment rule;
- duplicate-frame policy;
- disabled-keyframe behavior;
- stable ordering and stable ids;
- validation of a keyframe value against its target `FieldDefinition`.

### Known schema discrepancy — must be resolved first

The documented and React model uses `tracks[].keyframes[]` with `frame` and
`value`. The active `RuntimeTimeline.LastAnimationEndFrame(...)` currently reads
`tracks[].events[]` with `startFrame` and `durationFrames`. The committed sample
database currently contains only empty `tracks`, so the mismatch is latent.

The new thread must determine whether events are a separate finite-action
concept or an unfinished shape. It must then define and migrate one canonical
schema. Do not support both through fallbacks. Duration calculation must
understand the final schema explicitly.

## Typed interpolation rules

Start with a deliberately small matrix:

| Value type | Initial interpolation support |
| --- | --- |
| string/text | `hold`, derived `writeOn` |
| boolean, enum, reference, media source, delivery state | `hold` |
| integer/number | `hold`, `linear`, one approved eased interpolation |
| structured collection/object | no whole-object interpolation; animate declared child fields |
| colors, geometry and transforms | defer until explicitly declared by an owning contract |

Rules:

- Before the first keyframe, the base value from `content_json`/`behavior_json`
  remains effective unless the canonical contract explicitly chooses another
  rule.
- At an exact keyframe, the stored keyframe value is effective.
- After the final keyframe, the final value holds.
- Non-interpolable types never receive implicit coercion.
- Easing is deterministic and shared; it is not a CSS easing evaluated by the
  browser.
- Animation targets use stable runtime field and collection-item ids, never
  array indices or UI labels.

The first architecture deliverable must state interval ownership and boundary
semantics precisely, including frame inclusivity, so preview, duration and export
cannot disagree by one frame.

## Text keyframe algorithm

Text animation is a high-priority first-class feature, not a sequence of
generated character keyframes.

For consecutive text keyframes:

```text
frame 0  -> "Hola Jorge"
frame 36 -> "Hola Teresa"
```

the resolver:

1. splits both values into grapheme clusters, not UTF-16 code units;
2. finds their longest common grapheme prefix;
3. counts deletions from the old suffix;
4. counts additions in the new suffix;
5. divides the interval proportionally across all deletion and addition
   operations;
6. removes the final grapheme for each deletion step;
7. appends the next grapheme for each addition step;
8. returns the exact target at the destination keyframe.

Example:

```text
Hola Jorge -> Hola Teresa
common prefix: "Hola "
delete: "Jorge"
write:  "Teresa"
```

This supports ordinary edits, complete rewrites, emoji, accents and combined
Unicode sequences. Use the existing production shaping/grapheme utilities where
possible instead of `Array.from` if that would split a grapheme incorrectly.

The resolver may also emit generic diagnostics needed by owned children:

- visible/effective text;
- whether the interval is deleting or typing;
- changed grapheme for the current frame;
- normalized pressed-key semantic (`backspace`, character, shift/numeric/emoji
  mode as required);
- cursor/typing progress metadata.

Conversation can use that resolved state to keep Keyboard and Text Input Bar
synchronized. Text Box, Label, Bubble and renderer remain unaware of how the
text transition was derived.

`hold` remains available for an instantaneous text change. The normal system
message rule remains module-owned: centered and no write-on where the module
contract requires that behavior.

## Conversation sequencing already established

Conversation message timing is frame-based and message-specific:

- `delayAfterPreviousFrames`;
- `writeOnDurationFrames`;
- `postWriteOnHoldFrames`.

Message start is derived sequentially from prior message timing. System messages
are centered and ignore write-on. Incoming messages may reveal instantly, use
write-on or show the typing indicator. Outgoing message behavior may show
Keyboard/Text Input Bar while the message is written and reveal the Bubble
during or after write-on according to its runtime contract.

Keyboard and Text Input Bar visibility/timing are module behavior derived from
the active outgoing interval, not independent renderer animations. Delivery
status/checks are runtime values and appear only when their resolved state says
so. Attachments appear at the module-defined reveal point; they must not leak
ahead of write-on/typing-indicator timing.

Message viewport scrolling uses the module's selected Motion variant and Theme
timing. The requested frame determines the offset/progress; no browser timer
runs the scroll.

Parameter keyframes may later modify a message's text/status after its initial
sequence. The architecture phase must define whether their frames are relative
to the Screen or message appearance. The established direction is that persisted
track frames remain Screen-local; the owning resolver can derive message-local
elapsed time from the message's calculated appearance frame. Do not store
ambiguous mixed coordinate systems.

## Audio and video temporal model

Audio/video are finite state windows on the frame timeline, while source
position remains seconds.

Initial model to validate:

- a track/keyframe or finite event turns playback on at a Screen-local frame;
- mode is `playOnce` or `loop`;
- the authored playback window is finite;
- source position at a requested frame is derived from elapsed timeline frames
  and FPS, then expressed in seconds;
- `playOnce` clamps/ends at the earlier of the finite window and available source
  duration;
- `loop` wraps source position but still ends at its finite authored window;
- a later explicit `off` hold keyframe may end playback earlier;
- no loop is allowed to extend Screen duration indefinitely;
- image attachments have no playback window;
- cold video decoding/buffering remains an upstream preparation issue, never a
  renderer-side clock.

The architecture phase must decide the persisted representation without mixing
time domains. A recommended direction is frame-based activation/end boundaries
plus physical `currentTimeSeconds`/`durationSeconds` values. If a user-authored
media window is expressed in seconds, duration calculation converts it once
using effective FPS with a documented ceiling/inclusive rule.

Design Test Values media actions play from the action trigger until their finite
window is exhausted. Production uses the same resolver at the selected local
frame; it does not persist Design button/action state.

## Reusable visual motion

Reusable visual motion and parameter animation remain distinct but composable:

- a parameter track changes what a value is;
- a Motion variant defines how an owned visual enters/moves/fades/scales;
- Theme Motion supplies duration, delay, easing and intensity in milliseconds;
- the parent timeline supplies trigger state and elapsed milliseconds derived
  from frame/FPS;
- the component resolver emits the fully resolved frame.

Do not keyframe raw Theme tokens at Module Instance level. If a module exposes an
animatable semantic property such as opacity or position in the future, it must
be an explicitly declared runtime animation target with a typed contract.

## Duration model

Screen duration is the maximum end frame of all relevant concurrent temporal
sources, never an unbounded loop and never the sum of parallel tracks:

```text
max(
  declared module action/sequence duration,
  last Conversation message delay + write-on + hold,
  last parameter keyframe/segment end,
  last finite media playback end,
  last finite visual-motion event end,
  explicit stored minimum/fallback where still required
)
```

Shot duration is currently the sequential sum of its ordered Screen durations.
Any later overlapping/layered Shot model is a separate architectural decision.

Requirements:

- duration updates immediately when messages, timing, tracks, keyframes or media
  windows change;
- editor, Production navigator, slider ranges, play scope, cache preparation and
  export use the same duration service;
- deleting the final event may shrink duration deterministically;
- an exact boundary has one documented inclusive/exclusive rule;
- no UI control maintains a private duration calculation;
- `RuntimeTimeline` must be migrated from its current mixed action/event logic to
  the canonical temporal model without compatibility fallbacks.

## Preview, caches and frame sequences

The resident static-preview architecture is the baseline:

- stopped frame changes retain the last valid frame until the next one commits;
- compatible changes use resident DOM patching;
- errors retain the last-good preview;
- latest-state-wins suppresses stale work;
- assets and production fonts must be ready before commit.

Animation work must complete the pending shared resolved-frame sequence from
`28_static_preview_update_experience.md`:

```text
Design declarative action frames --+
                                   +-> resolved-frame sequence provider
Production Shot/Screen frames -----+          |
                                              v
                            shared HTML/raster preparation
                                              |
                                              v
                              one presentation/playback owner
```

Routes retain their meaning:

- `HTML · Priority FPS` may discard obsolete late frames;
- `HTML · Every frame` presents every prepared resolved frame;
- `Raster · Every frame` prepares and presents the complete raster sequence.

Shot playback may cross Screen boundaries. Cache/stale-work identity must include
at least:

- Shot id;
- active Screen/Module Instance stable id;
- global Shot frame;
- local Screen frame;
- effective FPS;
- device, Theme, mode and orientation;
- runtime/animation signatures;
- asset/font signatures;
- render route and geometry signatures.

Animation must not make a boundary crossing fall back to full WebView navigation
when the outer shell remains compatible.

## Animation editor UX direction

Do not implement this surface until the contract/resolver/duration tests pass.

The first Production animation editor should be intentionally modest:

- available on the selected Screen/Module Instance using user-facing `Screen`
  terminology;
- bound to the existing Production playhead and selected Shot/Screen scope;
- track list grouped by runtime field and stable collection item;
- Add Track chooses only fields declared animatable by the owning contract;
- Add/Update Keyframe captures the current effective typed value at the current
  Screen-local frame;
- keyframe values edit through their target dictionary control;
- interpolation options are filtered by value type;
- move, duplicate, enable/disable and delete keyframes are explicit collection
  actions;
- base value editing remains in Runtime Values; keyframe editing remains in
  Animation;
- the UI clearly indicates whether the current value comes from base content, a
  held keyframe or an interpolated segment;
- duration/range changes update the existing navigation immediately;
- empty, invalid-target and orphaned-item states are explicit.

The first version need not be a full graphical curve editor. A track list plus
frame-addressed keyframe cards can validate the model before investing in a
dope-sheet/timeline canvas. Any graphical timeline later remains a view/editor
of the same generic tracks and authoritative playhead.

The accepted UX/UI work remains applicable:

- use shared cards and hierarchical rows;
- keep context/breadcrumb visible;
- distinguish temporary Design values from persisted Production keyframes;
- use contextual preview loading/error states;
- provide noun-scoped tooltips and accessible names;
- preserve Light/Dark, narrow widths and keyboard navigation.

## Required first deliverable

Before implementation, create a canonical architecture proposal, suggested path:

`docs/architecture/29_animation_parameter_timeline_contract.md`

It must contain:

- inventory of current schemas/readers/writers and discrepancies;
- final versioned `animation_json` JSON schema with examples;
- target-addressing and typed-value validation;
- exact interpolation and boundary semantics;
- text grapheme rewrite algorithm;
- media playback representation;
- Screen/Shot duration formula;
- Design/Production frame ownership;
- resolver and resolved-frame sequence interfaces;
- cache/cancellation identity;
- migration plan with no fallbacks;
- editor information architecture and deferred features;
- test matrix and performance budgets;
- open product decisions requiring user approval.

Do not change production code during this audit/contract phase. Present the
document for review first.

## Roadmap

### Phase 0 — Synchronize baseline

- Start from UI baseline `b1fc05eb` or its explicitly approved successor.
- Pull, restart and confirm the app/tests are green.
- Record current DB schema, seeded `animation_json` values and preview routes.

Deliverable: baseline note in the architecture proposal. No implementation.

### Phase 1 — Contract and legacy audit

- Trace all current animation readers/writers and duration calculations.
- Compare active desktop code, architecture docs and React legacy.
- Resolve `keyframes` versus `events`, field/target naming and interval semantics.
- Produce `29_animation_parameter_timeline_contract.md` for user review.

Gate: explicit approval of the canonical model.

### Phase 2 — Canonical schema, migration and validation

- Implement one versioned schema and typed validators.
- Migrate seeds, examples, layouts and committed DB in the same commit.
- Remove retired identifiers/readers; no aliases or fallback parsing.
- Add architecture/database checks that scan all persisted animation JSON.

Suggested commit: `refactor: establish canonical parameter animation schema`

### Phase 3 — Pure typed animation resolver

- Implement deterministic track lookup and frame evaluation.
- Add hold, numeric linear/ease and boundary behavior.
- Keep it repository/UI/renderer independent.
- Define generic diagnostics/provenance for editor and markers.

Suggested commit: `feat: resolve typed parameter animation by frame`

### Phase 4 — Text deletion/rewrite

- Implement grapheme-safe common-prefix deletion and rewrite.
- Integrate only through owning text/module resolvers.
- Synchronize Conversation Keyboard/Text Input Bar from resolved edit metadata.
- Test emoji, combining marks, manual newlines, complete deletion, same values,
  very short intervals and exact destination frames.

Suggested commit: `feat: resolve grapheme text keyframe edits`

### Phase 5 — Finite audio/video state

- Implement play-once/loop finite windows and optional early off state.
- Resolve physical source position in seconds from frame/FPS.
- Cover media reveal timing and cold-preparation behavior.
- Prove loops cannot create infinite module duration.

Suggested commit: `feat: resolve finite media playback windows`

### Phase 6 — Unified duration service

- Replace mixed/private duration calculations with one canonical service.
- Include message sequences, keyframes/segments, finite media and motion events.
- Update Screen and Shot ranges immediately after edits.
- Add grow/shrink and off-by-one tests.

Suggested commit: `refactor: unify screen and shot duration resolution`

### Phase 7 — Shared resolved-frame sequence and playback ownership

- Complete phases 7–8 of `28_static_preview_update_experience.md`.
- Feed Design actions and Production playback through one generic sequence,
  preparation and presentation layer.
- Enforce one playback owner and cancellation on navigation/context/route change.
- Preserve Shot-global/Screen-local synchronization across boundaries.

Suggested commit: `refactor: unify resolved frame playback sequences`

### Phase 8 — Production animation editor MVP

- Add metadata-driven tracks/keyframe collections to the selected Screen.
- Use dictionary controls for typed values.
- Bind to the existing authoritative playhead/navigation.
- Implement Add/Update/Move/Duplicate/Disable/Delete and interpolation selection.
- Recalculate duration and preview immediately.

Suggested commit: `feat: edit screen parameter keyframes`

### Phase 9 — Reusable visual motion integration

- Represent finite motion triggers without keyframing Theme tokens.
- Convert frame elapsed time to Theme motion milliseconds in the owning resolver.
- Validate entrance, scroll and component state motion frame by frame.

Suggested commit: `feat: integrate frame-driven component motion`

### Phase 10 — Timeline visualization and advanced navigation

- Only after MVP validation, add a dope-sheet/timeline visualization.
- Reuse the same tracks/playhead; do not create a parallel timeline state.
- Consider zoom, snapping, multi-track selection and keyframe markers.
- Preserve compact Frame/Screen/Shot transport and accessibility.

Gate: explicit UX review before graphical timeline implementation.

### Phase 11 — Export determinism and performance closure

- Compare arbitrary-frame resolution with sequential playback.
- Verify HTML/raster/export parity.
- Measure resolver cost, cache hit/miss, preparation memory and cancellation.
- Establish performance budgets at representative 24/25/30 FPS Shots.
- Validate no late-frame backlog and no stale frame presentation.

Suggested commit: `test: enforce deterministic animated frame parity`

### Phase 12 — Documentation and behavior sheets

- Update canonical architecture docs and component behavior references.
- Close animation open items.
- Document editor workflows, schema examples and extension rules.
- Record deferred features explicitly.

Suggested commit: `docs: complete animation system contract`

## Verification matrix

At minimum cover:

### Schema and targeting

- empty animation;
- one/multiple tracks;
- repeated component type with independent stable item ids;
- orphaned/deleted item target;
- invalid target/type/interpolation;
- duplicate and unsorted keyframes;
- migrated database restart/idempotence.

### Boundary behavior

- before first, exact first, between, exact next and after last keyframe;
- one-frame and zero-span invalid intervals;
- Screen frame 0/end and Shot boundary crossing;
- 24, 25 and 30 FPS conversion;
- duration grows and shrinks after edits.

### Text

- append-only, delete-only and delete+rewrite;
- no common prefix and identical values;
- emoji sequences, flags, skin tones, combining accents and newlines;
- keyboard backspace/shift/numeric/emoji synchronization;
- hold versus write-on;
- system-message override behavior.

### Media

- image/no playback;
- audio/video play once;
- finite loop;
- early off;
- source shorter/longer than window;
- activation after final message;
- media end extends duration only when later than other events.

### Preview/navigation

- stopped frame step and slider;
- Shot and Screen scopes;
- play from nonzero current frame;
- cross-Screen playback;
- Design action versus Production playback mutual exclusion;
- HTML priority, HTML every-frame and raster every-frame;
- cancel on navigation, route, context and source edit;
- resident last-good frame on render/decode error.

### Architecture

- no component names/branches in bridge/common renderer;
- no renderer/CSS timers for production animation;
- no raw editable controls outside dictionary route;
- `MainWindow` shell-only;
- arbitrary frame equals the same frame reached sequentially;
- `npm run check:architecture`, focused tests, `npm test` and `git diff --check`.

## Performance and diagnostics

Add diagnostics before optimizing. Record:

- source mode, playback owner and route;
- Shot id, Screen id, global/local frame and FPS;
- animation signature and resolved target count;
- resolver time per track/module/frame;
- cache hit/miss and stale-work cancellation;
- asset/font/media preparation time;
- prepared/presented/dropped frames;
- requested/presented frame skew;
- memory reserved for raster/every-frame sequences.

Do not log full private message text or asset contents by default. Use ids,
counts, hashes/signatures and bounded diagnostics.

## Stop conditions and product decisions

Stop for user review if:

- the canonical schema cannot represent both parameter keyframes and finite
  media/motion windows without two clearly named concepts;
- a migration would require retaining compatibility readers;
- animatable fields cannot be declared generically through existing contracts;
- text animation would require renderer timers or generated persisted character
  keyframes;
- media looping would make duration unbounded;
- animation editing requires a private playhead separate from Production
  navigation;
- the shared resolved-frame provider would rewrite established Preview or
  Production transport semantics;
- a component-specific branch appears necessary in the bridge/renderer;
- the graphical timeline requires major UX decisions before the MVP model is
  proven.

Decisions the architecture proposal must explicitly surface:

1. final schema names and whether finite `events` coexist with parameter
   `keyframes` as distinct typed concepts;
2. interpolation ownership and inclusive/exclusive boundary rule;
3. media window authoring representation;
4. initial set of animatable value types;
5. policy for orphaned targets after collection item deletion;
6. whether keyframes may precede a message's calculated appearance frame;
7. first timeline editor form: keyframe cards only or cards plus compact ruler;
8. performance target and memory ceiling for every-frame preparation.

## Completion definition

The animation phase is complete only when:

- one canonical migrated schema exists with no fallbacks;
- arbitrary frames resolve deterministically without prior-frame state;
- text editing works by grapheme deletion/rewrite;
- media loops are finite and duration-safe;
- Screen/Shot duration, navigation, preview and export share one temporal truth;
- Design and Production remain separate context providers;
- HTML/raster/export output agree at sampled frames;
- the animation editor uses shared metadata/dictionary controls;
- architecture and full tests pass;
- each roadmap phase has an independently reviewable commit and explicit manual
  checks for the user.
