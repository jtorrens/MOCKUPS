# Animation and time

Status: normative.

## One frame clock

Preview uses one absolute Shot playhead internally. Editors project that clock
into the selected Screen's local authoring scale.

```text
Shot frame
→ Screen origin
→ owner appearance origin
→ owner-local field frame
```

Frame conversion belongs to the common timeline. Editors, payload factories,
resolvers and renderers do not reproduce the formulas.

## Temporal ownership

Every temporal entity follows one rule:

- appearance, disappearance, activation and selection are authored in the
  local time of its parent;
- the entity's own fields and keyframes are authored relative to its first
  appearance;
- moving or reordering an entity recalculates effective frames without
  rewriting its stored local keyframes;
- re-entry restarts parent-owned Enter/Exit Motion but does not restart the
  entity's internal timeline;
- stable ids, never indices, bind owners and tracks.

This applies recursively to Shots, Screens, stack slots, States, structured
collections and nested Components.

## Persisted tracks

Parameter animation is persisted only as version 2 tracks identified by stable
`fieldId` and `targetId`. A track is relative to its declared owner.

The common owner timeline derives:

- effective origin;
- completion dependencies;
- finite action duration;
- non-sequencing fields;
- absolute Preview frame projection;
- retime projection.

An editor never stores absolute Shot frames in a child-owned keyframe.

## Duration policies

A Module declares one Screen duration policy:

- `calculated`: finite actions and collections determine Screen extent;
- `explicit`: the Module Instance frame count is authoritative.

An explicit policy declares a positive default and is edited only on the
Screen instance. Child keyframes and composition cannot extend it silently.

The authoring `+` horizon is session-only for both policies. It is not duration
and is never persisted.

## Behavioral timing

Reusable action duration uses dictionary `BehaviorTiming`:

- fixed mode resolves authored frames;
- natural mode resolves semantic units × the Module-owned base rate × a
  `theme.motion.naturalPace.*` multiplier.

The owning resolver determines deterministic internal cadence inside the final
duration. Bridge and renderer receive only the resolved state for the requested
frame.

Contract-declared finite and base durations use the shared reference-duration
lane. Retime is disabled when `targetDurationFrames` is absent.

## Keyframe interaction

Keyframes are selected and dragged through the shared timeline interaction.
Drag converts pointer movement into the selected Screen-local authoring scale
and commits a valid owner-local frame.

During drag:

- the keyframe keeps its stable track and owner;
- preview playhead updates through the common frame projection;
- bounds come from the owner timeline and current duration policy;
- cancellation restores the uncommitted position.

No drag path identifies a keyframe by visual position or collection index.

## Frame-by-frame Preview

Animation is resolved frame data. For every requested frame:

1. the timeline computes exact local frames;
2. each Module and Component owner resolves its state;
3. renderables emit generic resolved primitives;
4. the renderer paints that frame.

The web layer does not run timers, CSS animations, countdowns or
Component-specific interpolation.
