# Animation Keyframe Drag Interaction Contract

Status: normative.

This document governs the desktop interaction boundary between the Production
Animation mini-timeline and the shared Preview playhead. It refines contracts
29, 45, 52 and 59 without changing animation v2, temporal ownership, duration
policy or persistence shape.

## 1. Objective

One drag gesture must keep one live marker surface until it finishes:

```text
pointer drag on one nonzero keyframe
→ Screen-frame candidate and declared snapping
→ shared absolute Shot Preview frame
→ resolved Preview feedback
→ one owner-local keyframe move on valid release
```

Moving the shared Preview cursor publishes a synchronous playback-state
notification. A notification caused by the active animation editor must update
the other Preview consumers, but must not make that same editor rebuild its
mini-timeline while the pointer is captured. External transport, playback and
Preview frame changes continue to refresh the editor normally.

## 2. Interaction ownership

`ModuleInstanceAnimationEditor` owns:

- the active pointer gesture and marker capture;
- the Screen-local candidate shown by the slider and frame label;
- delegation of the corresponding absolute Shot frame to the one Preview
  playhead;
- suppression of its own synchronous playback feedback while that local frame
  update is in progress;
- rebuilding its local animation surface after a completed external frame
  change or a persisted animation edit.

`TimelineFrameUpdateGate` is a generic, exception-safe re-entry boundary. It
only identifies the synchronous extent of a locally initiated frame update. It
must not retain a frame, debounce or delay feedback, own playback, interpret a
component, or suppress notifications for other consumers.

The Preview controller remains the sole absolute Shot playhead owner. The gate
does not add another cursor or alter `PreviewPlaybackState` publication.

## 3. Persisted move boundary

Dragging remains presentation-only until release. The editor writes exactly
one complete prepared animation v2 document only when all existing contract 29
conditions accept the destination. It converts the accepted Screen position
through the declared owner timeline and persists the resulting owner-local
integer frame.

The following remain unchanged:

- KF0 cannot move;
- ordinary drag snaps to five Screen frames and `Alt` enables one-frame
  precision;
- existing keyframes provide the stronger soft detent;
- occupied, invalid, capture-lost and cancelled destinations restore without a
  write;
- keyframe id, typed value, interpolation and enabled state are preserved;
- relative owner keyframes, stable target ids and explicit duration policy are
  authoritative.

## 4. Boundary constraints

- `MainWindow` only wires the shared playhead callbacks and contains no drag
  implementation.
- The Preview bridge and renderer receive resolved frame state only and contain
  no gesture, keyframe or timer logic.
- No database, JSON, migration, payload, resolver, forwarding or Variant
  contract changes are part of this correction.
- The editor must not disable all playback-state feedback during a gesture;
  Runtime Value controls and Preview presentation still receive the candidate
  frame.
- No behavior may be inferred from names, component types, labels, hierarchy
  positions or array indices.

## 5. Enforcement and acceptance

Architecture enforcement must verify:

- this contract is linked from `AGENTS.md` and the architecture index;
- the animation editor uses the shared frame-update gate around locally
  initiated Preview frame changes;
- its playback callback exits while that gate is active;
- the gate has no Avalonia, persistence, payload, resolver or renderer
  dependency;
- the Preview controller remains the shared playhead owner.

Automated tests must prove that a local frame notification does not re-enter
its originating surface, an external notification still refreshes it, and the
gate always reopens after failure. Existing animation tests remain authoritative
for snapping, owner projection and payload-preserving moves.

Manual acceptance in the running application must prove that a nonzero marker
visibly follows the pointer, Preview follows the candidate, release persists
once, `Alt` permits one-frame placement, cancellation restores, and KF0 remains
fixed.

## 6. Forbidden shortcuts

- rebuilding the mini-timeline from its own candidate-frame notification;
- writing every pointer move to SQLite;
- delaying Preview feedback until pointer release;
- adding a second animation playhead or an editor-owned timer;
- storing the Screen or Shot candidate as the keyframe frame;
- moving drag behavior into `MainWindow`, the bridge or the renderer.
