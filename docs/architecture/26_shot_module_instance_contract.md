# Shot Module Instance Contract

## Ownership

The runtime composition hierarchy is deliberately flat:

```text
Project
  -> Episode
    -> Shot
      -> ordered ModuleInstance slots
```

A `ModuleInstance` is the one concrete runtime unit for a module in a shot.
There is no separate Screen Instance layer. A shot is the phone action being
recorded; its ordered module slots are the visual states that occur during that
action.

## Context inheritance

`ModuleInstance` owns only its module reference, persisted runtime content,
behavior, animation data, duration and transition declaration.

It does not own actor, device, theme, mode or device state. Those are resolved
from the shot and its owner actor. FPS is inherited from the project, with a
future nullable override at shot level.

## Slot timeline

Slots are ordered by `sort_order`. The initial transition contract is:

```json
{ "type": "cut" }
```

For cuts, slot durations are sequential and the shot timeline is their summed
duration. The shot editor will expose add, delete, reorder and navigation for
these slots. The first editor phase allows only Conversation module instances.

## Future transitions

The React implementation in `src/render/timeline/screenTimeline.ts` derives
timing from duration and transition declarations, then `resolveShot.ts`
resolves active entries for a requested frame. We retain that useful model but
apply it directly to module slots:

- transitions declare type and duration in frames;
- non-cut transitions overlap the outgoing and incoming slots;
- the shot resolver asks both participating module resolvers for the requested
  frame and emits their resolved nodes;
- the generic renderer receives only the resulting atoms and transition values.

This remains a future timeline phase. The present model stores `transition_json`
so no schema redesign is needed when cut-only slots gain crossfade, slide or
other transitions.
