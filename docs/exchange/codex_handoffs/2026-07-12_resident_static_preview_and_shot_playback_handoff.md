# Resident static preview and Shot playback handoff — 2026-07-12

## Objective

Implement the resident static-preview update model described in
`docs/architecture/28_static_preview_update_experience.md`, then consolidate
Shot Play and declarative Test Values actions behind one generic resolved-frame
playback pipeline.

Start from `main` at commit `7724fc1b` or later. Pull before editing and restart
the desktop application so no stale preview process or editor layout remains in
memory.

## Baseline already complete

Do not reimplement these features:

- Shot preview selects the active ordered ModuleInstance and resolves its local
  frame from the global Shot frame.
- The main navigator has first/last Shot frame, previous/next frame,
  previous/next ModuleInstance, Play and a global frame slider.
- Production context identifies the active ModuleInstance and lists Shot slots.
- Device and Theme derive from Shot/Actor and are hidden in production setup.
- Mode follows preview mode unless the active module forces light or dark.
- Shot Play already prepares a sequence through HTML/raster as an interim
  correctness path.
- Conversation Bubble status spacing, inline delivery placement, actor-name
  minimum width, typing centering and external-avatar geometry are complete.

Relevant baseline commits:

- `e7c0e66e` — production Shot preview navigation and first HTML/raster bridge;
- `d108117c` — resident-preview architecture proposal;
- `1302cc30` — Bubble transient geometry correction;
- `7724fc1b` — placement-aware Bubble width and compact/inline delivery status.

## Required first phase: resident static updates

Normal stopped edits must keep the last valid preview visible. Implement in
this order:

1. Classify shell compatibility independently from animation-only updates.
2. Route compatible static changes through the resident generic DOM patch.
3. Synchronize the identified production-font style before committing a body.
4. Preserve the last valid state when rendering or asset preparation fails.
5. Suppress a completed static update when a newer pending state supersedes it.
6. Keep full WebView loads only for initial/empty/incompatible shell changes.

Static Shot navigation is included: slider, frame buttons, slot buttons and
slot selection must patch or decode-gated-replace the resident body without an
opaque playback scrim. Crossing a ModuleInstance boundary does not by itself
authorize a WebView navigation.

Reference mode, swipe, opacity and angle remain resident overlay state. A
reference video follows the global Shot frame, not the module-local frame.

## Required second phase: shared playback sequence

The current `EditorPreviewController` uses
`_pendingPlaybackFramesOverride` to feed Shot payloads into action-oriented
preparation. Replace that bridge with a generic frame-sequence contract:

```text
resolved frame-sequence provider
  -> shared HTML/raster preparation
  -> shared assets/fonts/cache reservation/progress/cancellation
  -> selected presentation policy
```

Providers differ, presentation does not:

- a declarative Test Values action produces its resolved action frames;
- Shot Play produces resolved global Shot frames from the selected frame to the
  Shot end, crossing ModuleInstance boundaries as necessary.

Preserve route semantics:

- HTML Priority FPS may discard obsolete frames;
- HTML Every Frame must present every prepared frame;
- Raster Every Frame must prepare and present the raster sequence.

Only one playback owner may run. Starting Shot Play stops Test Values playback;
starting a Test Values action stops Shot Play. Any stopped navigation command
stops playback before committing the requested frame. Route, Shot, slot or
resolved-context changes cancel stale preparation.

## Boundaries that must remain intact

- Component and module resolvers own requested-frame state.
- The bridge translates only generic resolved atoms.
- The web renderer paints resolved nodes and contains no component knowledge,
  token inheritance, timers or animation logic.
- Avalonia coordinates assets, fonts and atomic presentation but never paints
  production visuals.
- No component-name branch may be added to central preview files.
- Stable renderable ids and the generic asset registry remain the DOM identity
  mechanism.
- Do not introduce compatibility fallbacks. Migrate persisted contracts and
  remove retired identifiers in the same change.
- `MainWindow.axaml.cs` remains shell-only.

## Diagnostics and acceptance

Measure a representative stopped edit session before and after phase 1:

- static `full-load` versus resident patch count;
- median and p95 render/commit time;
- structural layer replacements;
- font-style signature changes;
- asset registration or decode failures;
- renders discarded because a newer update was pending.

Verify manually:

- dictionary and Test Values edits never blank a valid stopped preview;
- a failed render preserves the last valid visual state;
- rapid text edits do not present obsolete intermediate bodies;
- markers and reference controls do not navigate the document;
- Shot slider/frame/slot navigation remains resident;
- active context title and forced mode change atomically with the preview;
- Shot Play works in all three HTML/raster modes and crosses instance boundaries;
- Test Values Play still preserves its current route semantics;
- no late-frame backlog appears.

Run before every commit that closes a phase:

```text
npm test
npm run desktop:schema-v1:validate
git diff --check
```

`npm test` includes the mandatory architecture check. Extend it with durable
rules for resident static commits, production-font synchronization, last-good
state retention, stale-update suppression, shared frame-sequence preparation
and single playback ownership.

## Commit guidance

Prefer independently reviewable commits:

1. resident static body patch and font synchronization;
2. last-good state and stale-update suppression;
3. generic resolved-frame-sequence abstraction;
4. Shot/Test Values playback ownership and cancellation;
5. documentation, enforcement and measured results.

Do not mix Conversation or component visual changes into these commits. If a
visual regression is discovered, document it and fix it separately after
confirming the preview layer is not merely presenting stale output.
