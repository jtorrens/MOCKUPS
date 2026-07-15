# Static desktop preview update experience

Status: resident static-preview phase implemented and manually validated on
2026-07-12. The shared resolved-frame playback abstraction remains proposed.

## Current implementation baseline

The proposal must be implemented from `main` at or after commit `0b26b7df`.
That baseline already contains:

- production Shot preview resolved through ordered ModuleInstance slots;
- global Shot frame navigation and global-to-local module-frame translation;
- production context chrome that identifies and selects active module instances;
- independent persisted Design and Production context-history stacks;
- a user-facing `Shot / Screen` navigation scope where Screen means the active
  ModuleInstance without exposing that internal term;
- stable Screen identity in Runtime Values and playback cache keys, independent
  of display name, plus the resolved local frame in production payload context;
- a grouped transport surface with separate Shot/Screen zones and editor-theme
  accent colors;
- automatic Screen-local scope when a Screen is selected or traversed, while a
  later explicit user choice of Shot scope remains authoritative;
- Device and Theme removed from production controls because Shot/Actor own them;
- preview mode inheritance with explicit module light/dark override;
- a first Shot Play integration with the existing HTML/raster preparation route;
- the final Conversation Bubble geometry, including placement-aware actor-name
  width, compact delivery status rows and inline status placement.

The existing Shot Play integration is a correctness bridge, not the final
playback abstraction. It supplies Shot payloads to the current preparation
pipeline through a pending-frame override. Phase 7 must replace that temporary
coupling with the shared resolved-frame-sequence provider described below.

The first resident static-update phase is implemented in the desktop preview.
Compatible stopped updates retain the WebView document, synchronize the
identified production-font style, and commit through the generic DOM patch.
Render or asset-commit failures preserve the last valid body, and completed
updates are discarded when a newer state is already pending. Full document
loads remain limited to initial, empty and shell-incompatible states.

Production and Design have explicit input boundaries. Design may apply
session-only Test Values and declarative actions. Production resolves only
declared runtime references and preserves the Shot-global to Screen-local frame
calculated by navigation; it never passes through the Design input session.

Resident patch completion is retained by patch id independently from diagnostic
event draining. A log reader therefore cannot consume the confirmation awaited
by presentation. This is required to prevent a stopped patch from delaying the
playback clock and making a Screen appear to start part-way through.

Raster Shot playback uses the same prepared frame map as Test Values playback,
but explicitly activates the native bitmap surface while the clock is running.
The native WebView must be hidden during that interval because its platform
airspace otherwise paints above Avalonia and conceals correctly advancing
bitmaps. Manual validation reached 89/91 presented frames at 24.8 FPS; visual
playback, slider navigation and frame-step navigation were correct.

The shared resolved-frame playback sequence and single playback ownership are
still pending. Do not rewrite
the production navigator, Conversation resolver or Bubble geometry while
implementing this proposal unless a demonstrated preview-boundary defect
requires a separately reviewed change.

## Purpose

Editing a stopped preview should preserve visual continuity. A normal field,
variant, Test Values or asset update must not cover or blank the complete
preview merely because a new resolved state is being prepared.

This document separates static editing updates from playback preparation and
proposes a resident-WebView update model compatible with HTML priority, HTML
every-frame and raster playback.

## Observed behavior

The opaque interruption perceived during static editing is normally not the
Avalonia playback scrim. It is primarily the visible effect of replacing the
complete WebView document:

```text
field/value change
  -> render a new HTML body
  -> preview.webview.update route=full-load
  -> NavigateToString(...)
  -> previous document disappears
  -> WebView background becomes visible
  -> new document loads
```

Recent measurements show static full loads around 140-250 ms. Several
consecutive updates make that interval look like an opaque scrim or flash even
though no playback process is active.

The explicit opaque preparation scrim remains appropriate for operations that
intentionally block before playback, such as rasterization or complete
every-frame preloading. It should not be the normal static-editing experience.

## Desired behavior

The WebView document should remain resident while the user edits a stopped
preview:

```text
resident WebView shell
  -> keep last valid preview visible
  -> resolve/render next static state in background
  -> register required assets and fonts
  -> patch or replace the preview body in place
  -> commit the new state atomically
```

The previous valid frame remains visible until the next valid frame is ready.
Avalonia coordinates the update but never paints or recreates the production
visual result.

## Update classification

## Design and Production input boundary

Design preview may apply transient Test Values and declarative action clocks.
Every declared action is presented by the same mixed Test Values control: a
label, or a target-state selector for option actions, followed by compact Play
and Restore buttons. Play always starts at action frame zero and leaves the
resolved final frame visible. Restore returns only that action and its declared
target inputs to the snapshot captured before its first Play; Reset Test Values
continues to clear the complete Design fixture. The currently effective option
remains visible in a target selector but is disabled as a destination.
Action placement follows ownership without promotion: root actions live at the
root Test Values level, collection-item actions live inside that item, and an
embedded component action lives inside the embedded component-input section of
its owning item. Multiple actions at the same ownership level use one horizontal
wrapping row and move to the next line only when their combined intrinsic width
does not fit. Stable collection id, item id and nested input path form the
action scope, so equal child action ids never share Play or Restore state.
Production preview must use only persisted Shot/Screen runtime state and the
global-Shot-to-local-Screen frame selected by production navigation.

Production payloads therefore pass through a generic reference-only runtime
resolver. That resolver may materialize declared record references, including
collection item references, but must not write scalar inputs, action state or
action time fields. In particular, a Design `conversationFrame` value must
never overwrite the local frame already resolved for a Production Screen.
It removes transient Test Values and declarative Design actions from the
Production envelope after using the input declarations to resolve references.

The boundary applies equally to stopped frames and playback preparation.
Diagnostics record Shot frame, Screen id, Screen start, Screen duration and the
local frame before and after reference resolution.

### Static body updates

These should normally use an in-place body patch and never blank the preview:

- dictionary field edits;
- Test Values changes;
- component configuration and variant changes;
- text, colors, spacing and sizes;
- collection item edits;
- image and media-source changes;
- renderable child additions, removals or reordering;
- marker activation;
- reference overlay and split controls.

Matching renderable trees may use DOM morph. Structural differences may use the
existing decode-gated layer replacement. In both cases the old state remains
visible until the new state can commit.

Reference view mode, swipe, opacity and angle remain resident overlay state and
should not regenerate component HTML.

### Preview-shell updates

The following may initially retain full document loads because they change the
outer viewport or device shell:

- device selection;
- orientation;
- canonical-frame mode;
- complete viewport geometry;
- preview scale strategy;
- transition between incompatible preview surface types.

A later phase may move these to resident CSS variables and JavaScript state,
but that is independent of eliminating full loads for normal field editing.

### Playback preparation

An opaque, blocking scrim remains valid for explicit playback preparation:

- raster-frame generation;
- complete HTML every-frame preparation;
- asset decoding required before guaranteed playback;
- other cancellable operations whose result must be complete before playback.

The scrim must be tied to active declarative playback state. Static refreshes
must never inherit a stale playback overlay.

## Production Shot navigation

Production navigation introduces two distinct update classes that must not be
confused merely because both change the requested frame.

### Stopped navigation is a static update

The following operations select a single resolved Shot frame and therefore use
the resident static-update path:

- dragging or clicking the Shot frame slider;
- previous/next frame;
- first/last Shot frame;
- previous/next module-instance slot;
- selecting a module-instance slot from the production context control.

They must keep the last valid frame visible, must not start playback
preparation and must not show the opaque playback scrim. Crossing a
module-instance boundary may replace the complete preview body layer when the
renderable trees are structurally incompatible, but must not recreate the
WebView document when the outer preview shell is compatible.

The navigator owns a global Shot frame. The active module resolver receives
only its local frame:

```text
Shot frame
  -> ordered active ModuleInstance slot
  -> local module frame = Shot frame - slot start frame
  -> resolved module renderable
  -> resident static commit
```

Reference video synchronization continues to use the global Shot frame, not
the active module's local frame.

The production navigator may display either scope without changing this
internal authority:

- `Shot` displays `0…Shot duration - 1` and exposes the global frame;
- `Screen` displays `0…active Screen duration - 1` while translating its local
  value to the corresponding global Shot frame;
- start/end and Play operate on the selected scope;
- previous/next Screen changes the active ModuleInstance but remains labeled
  `Screen` in user-facing UI.

### Shot Play uses the selected playback route

The main Shot Play control must use the same playback infrastructure and route
selection as declarative Test Values actions:

- `HTML · Priority FPS` may discard obsolete frames;
- `HTML · Every frame` presents every prepared frame;
- `Raster · Every frame` prepares and presents the raster sequence;
- preparation progress, cancellation, asset registration, font prewarming and
  cache reservations remain shared behavior.

The difference is only the frame-sequence provider. A declarative component or
module action provides frames from one runtime action; Shot Play provides the
ordered resolved payload sequence from the current global frame through the
Shot end. The renderer and WebView must not need to know which provider created
the sequence.

Shot playback may cross any number of module-instance boundaries. Cache and
stale-work identity must therefore distinguish at least the Shot, active module
instance, global Shot frame, local module frame, resolved device/theme/mode and
the normal render signatures. A boundary between modules is not by itself a
reason to fall back to a full WebView navigation.

Only one playback owner may be active in a preview. Starting Shot Play stops an
active Test Values action; starting a Test Values action stops Shot Play. Static
navigator commands stop playback before selecting their requested frame.

### Production context ownership

Production preview chrome is not part of the rendered module payload:

- Device and Theme are inherited from the Shot and its Actor and are not
  editable production-preview controls;
- preview mode follows the preview selector unless the active module explicitly
  forces light or dark;
- the context title identifies the active ModuleInstance;
- the context list contains ordered ModuleInstance slots from the Shot, never
  embedded component presets;
- changing active instance or forced appearance updates editor chrome and the
  resident preview atomically, without exposing a mismatched intermediate
  state.

## Fonts and head styles

General static body patching also requires correct `<head>` synchronization.
The rendered output currently separates `fontStyleHtml` from the body. A safe
static commit sequence is:

```text
render next state
  -> compact and register missing assets
  -> update production @font-face style when its signature changed
  -> prepare the new body
  -> morph or decode-gated replace
  -> confirm commit
```

The body must not commit against missing or stale production-font rules. No
host-system font fallback may be introduced to hide a synchronization failure.

## Error behavior

A failed static render should not immediately destroy a valid preview.

Preferred behavior:

- retain the last successfully committed state;
- report the error through Messages;
- optionally show a small non-blocking `Preview outdated` indicator;
- replace the resident state only after a later render succeeds.

An empty-payload selection may still present the normal empty placeholder.

## Coalescing and stale work

Rapid edits should follow latest-state-wins semantics:

```text
render A starts
  -> edit B arrives
  -> edit C arrives
  -> A may finish but must not commit if C is pending
  -> render and commit the latest required state
```

The current queue already stores at most one pending update, but a completed
intermediate render may still be presented before the pending state. A
pre-commit stale-update check would prevent that visual flash and avoid
unnecessary WebView work.

Playback keeps its existing no-backlog rule: late animation frames may be
discarded and must never be replayed after their intended time.

## Non-blocking static progress

Most static patches should complete without showing progress UI. If an update
exceeds approximately 120-150 ms, the application may show a small resident
indicator:

- spinner or activity dot in a preview corner;
- optional `Updating preview...` label;
- no opaque background;
- no input blocking;
- previous valid preview remains visible.

The indicator should be delayed so fast updates do not create additional
flicker.

## Proposed implementation phases

Phases 1-5 below are complete. Phase 6 remains measurement-driven; phases 7-8
remain the next implementation boundary.

1. **General static body patch**
   - Define shell compatibility independently from animation-only updates.
   - Route compatible static changes through the existing generic patch path.

2. **Production font-style synchronization**
   - Give the resident document an identified production-font style element.
   - Update it before committing a body that requires a new font signature.

3. **Last-good-state error policy**
   - Retain the committed body when a static render fails.
   - Surface the error outside the production preview pixels.

4. **Stale render suppression**
   - Before committing, skip a completed update when a newer incompatible
     pending update supersedes it.

5. **Shell full-load reduction**
   - Keep full load only for genuinely incompatible shell changes.
   - Consider resident CSS/JavaScript updates for device geometry separately.

6. **Delayed non-blocking activity indicator**
   - Add only if measurements show useful feedback beyond the continuity
     improvements above.

7. **Shared resolved-frame playback sequence**
   - Extract preparation and presentation from the action-specific frame
     producer.
   - Feed both declarative actions and Shot Play through the same HTML/raster
     route, cache reservation, progress and cancellation behavior.
   - Preserve global Shot navigation state while resolving local module frames.

8. **Playback ownership and navigation cancellation**
   - Enforce one active playback owner per preview.
   - Cancel stale preparation when the Shot, active slot, route or resolved
     preview context changes.
   - Return to the selected static frame through the resident update path.

## Architecture constraints

- The resolver remains responsible for component state at the requested frame.
- The web renderer remains responsible for the complete production visual.
- Avalonia may coordinate rendering, assets, fonts and atomic presentation but
  must not recreate component visuals.
- The WebView bridge remains generic and must not branch on component type.
- Stable renderable ids and the generic asset registry remain the basis of DOM
  updates.
- Static edits and playback use the same resolved component contracts.
- No renderer-side timers, component-specific morph rules or host-font
  fallbacks are permitted.

## Acceptance criteria

For a stopped preview:

- ordinary field and Test Values edits do not blank the complete preview;
- the last valid state remains visible until the next state commits;
- new images are visible only after their assets are registered and decodable;
- a failed render preserves the last valid visual state;
- rapid edits do not visibly present obsolete intermediate states;
- reference controls remain immediate and independent of body rendering;
- markers appear without a document navigation;
- logs report `dom-patch` or an equivalent resident replacement for compatible
  static changes;
- full loads are limited to initial load, empty selection and incompatible
  shell changes;
- opaque scrims appear only for explicit playback preparation.

For playback:

- HTML priority, HTML every-frame and raster retain their current semantics;
- no late-frame backlog is introduced;
- preparation progress and cancellation remain available where required.
- Shot Play honors the selected HTML/raster route and may cross module-instance
  boundaries without a full document load;
- global Shot frame, active slot and local module frame remain synchronized;
- Shot/Screen scope changes preserve the equivalent global frame and only
  change the displayed range;
- frame/slot navigation stops playback and commits the requested static frame;
- Shot Play and Test Values playback cannot run concurrently.

## Recommended next measurement

Before implementation, classify a representative edit session from
`logs/desktop-preview-debug.log`:

- number of static `full-load` updates;
- number of static `dom-patch` updates;
- median and p95 render time;
- body structural replacement count;
- font-style signature changes;
- asset registration/decode failures;
- updates completed while a newer state was already pending.

For a representative production Shot, also record:

- stopped frame changes within one module instance;
- stopped frame changes crossing a module-instance boundary;
- Shot Play preparation and presentation for each HTML/raster route;
- cache hits and misses across module-instance boundaries;
- global-to-local frame translations;
- cancellations caused by slider, slot, route or context changes.

This measurement will show how much of the visible interruption is removed by
phase 1 alone and whether the delayed activity indicator is necessary.
