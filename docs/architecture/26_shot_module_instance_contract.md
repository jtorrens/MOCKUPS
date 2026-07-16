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

Shot module slots are timeline entries. They are not Component Stack slots. A
Module Instance may internally resolve a Component Stack, but that structure is
owned by its selected Module Variant and effective runtime contract.

The detailed relationship is defined in
[Structural Stacks, Slots, States and Module Instances](31_structural_stacks_slots_and_module_instances.md).

## Context inheritance

`ModuleInstance` owns only its module reference, persisted runtime content,
behavior, animation data, duration and transition declaration.

`content_json` follows the public runtime-input contract declared by its
selected full Module Variant reference (`moduleId::variant::variantId`). The
module class contributes shared runtime declarations; the Variant contributes
concrete config and Forward declarations. The Module Design Test Values editor and the concrete
ModuleInstance editor therefore expose the same fields, collections, hierarchy
and actions. Calculated/parent-owned inputs are excluded from persistence.
`behavior_json` is reserved for module-internal instance state that is not a
public runtime input; Conversation currently retains only its head/tail timing
padding there.

It does not own actor, device, theme, mode or device state. Those are resolved
from the shot and its owner actor. FPS is inherited from the project, with a
future nullable override at shot level.

Changing Module Variant explicitly rebuilds the effective instance contract.
Runtime values not declared by the new contract and v2 animation tracks whose
field/target ids no longer exist are removed. There is no class-config fallback,
State-name matching or Actor/Device inference.

## Slot timeline

Slots are ordered by `sort_order`. The initial transition contract is:

```json
{ "type": "cut" }
```

For cuts, slot durations are sequential and the shot timeline is their summed
duration. The shot editor exposes add, delete, reorder and navigation for these
slots. Each Module Instance selects one registered module and one explicit
Variant of that same module.

A module declares the finite action that owns its duration with
`definesModuleDuration`. The shared evaluator reads that action's declared
collection and frame fields, compares its endpoint with authored animation
events, persists the module-instance duration, and synchronizes the Shot sum.
The Preview controls expose a Shot frame navigator and resolve the selected
frame through the same local-frame contract.

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

## Future on-set raster package intention

This section is a compatibility declaration, not an implementation phase or a
complete mobile specification.

In the future, an on-set mobile device may receive a pre-rendered raster package
for a Shot instead of the original payload and HTML/CSS renderer. The package
may contain both `light` and `dark` sequences so the operator can choose the
effective mode before playback. Play, pause and frame navigation are playback
state, not edits to Shot or ModuleInstance data.

No package format, transfer protocol, mobile UI, persistence model or delivery
schedule is defined yet. Current work must only avoid making that future route
impossible:

- frame resolution remains deterministic and based on the project frame;
- Shot/Module contracts do not depend on Avalonia, WebView invocation details,
  SQLite access during rendering, host-system fonts or live network assets;
- production text and emoji fonts and all media are explicit contract resources;
- a future package may carry shot-wide glyph usage and deterministic subsets of
  those production fonts; subsets are packaging products, never system-font
  fallbacks or per-frame font changes;
- HTML/CSS remains a deterministic production stage used to generate preview,
  raster frames and MOV output; it is not required on the on-set device;
- desktop DOM morphing and `mockups-asset:` interning remain adapter-local
  optimizations, not portable raster-package requirements;
- the effective light/dark mode is context supplied to resolution and is not a
  visual override stored by ModuleInstance.

The intended portable frame vocabulary is generic:

- `full`: a complete bitmap establishes a new base;
- `tiles`: only fixed-grid tiles affected by changed resolved nodes are stored;
- `hold`: no visual state changed and the preceding composited frame remains;
- every bitmap asset is content-addressed and may be reused by either mode;
- package manifests carry device resolution, FPS, mode, frame number and base
  dependencies explicitly.

Resolvers do not choose tiles. They provide stable node ids, final visual
values and deterministic bounds. A generic comparator after resolution detects
changed nodes, unions previous/current bounds, aligns dirty regions to tiles and
falls back to a full frame above a configured changed-area threshold.

Do not begin mobile runtime implementation from this declaration. A separate
normative contract should be written only when that product phase starts.
