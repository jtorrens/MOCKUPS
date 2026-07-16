# Lock Screen

Status: active module on the module resolver -> renderable route.

Source of truth: `src/desktop-preview/lockScreenModuleContract.ts`,
`lockScreenModuleResolver.ts`, `lockScreenModuleRenderable.ts` and
`spikes/desktop-editor-shell/Data/SpikeDatabase.LockScreenModule.cs`.

The Module Variant -> effective Module Instance contract and Stack State
animation relationship is defined in
[Structural Stacks, Slots, States and Module Instances](../31_structural_stacks_slots_and_module_instances.md).

## Composition

Lock Screen owns four ordered visual layers:

1. the selected Actor wallpaper;
2. one concrete Component Stack Variant;
3. the selected Status Bar Variant when visible;
4. the selected Navigation Bar Variant when visible.

The Stack is the content container for future Lock Screen components. Status
Bar, Navigation Bar and Stack are explicit embedded slots containing
`presetId` plus local `overrides`.

Component Stack keeps its own public runtime contract. At the Lock Screen
composition boundary, `stackInputs` binds that contract as module Variant data:
the Stack scalar inputs and ordered `items` collection are authored in the
normal Lock Screen card. Any child runtime field may be promoted explicitly to
the Lock Screen runtime through Runtime Input Forwarding. Lock Screen does not
copy a component-specific field catalog or require a new Stack Variant for each
composition.

The Lock Screen module class owns schema and resolver identity. Each Lock Screen
Module Variant owns one complete composition snapshot: Stack slots/States,
selected child Variants, Placements, Motions and Forward declarations. A Shot
Module Instance selects that Variant explicitly through
`moduleId::variant::variantId`; Actor, Device and Theme never choose it
implicitly.

## Runtime inputs

- `actor`: selects the Actor whose wallpaper contract supplies the background;
- `showStatusBar`: independently includes/excludes Status Bar;
- `showNavigationBar`: independently includes/excludes Navigation Bar;
- dynamically forwarded Stack or nested item fields chosen by the module
  designer.

A Label placed directly in the Stack receives the module instance's explicit
owner-local frame and project FPS through the generic preview payload. Its
count-up/count-down text is therefore resolved deterministically before Stack
measurement and placement; Lock Screen, the bridge and the renderer add no
clock behavior.

`sizingMode`, `startGapToken`, `endGapToken` and `items` remain runtime inputs of
Component Stack itself. They are not automatically runtime inputs of Lock
Screen. The parent binds them as Variant values unless the designer activates
Forward. Design Test Values and module instances consume only the resulting
effective Lock Screen runtime contract.

If a Stack slot State action is forwarded by that contract, Test Values and the
Module Instance show it at the slot's nested level. Production persists its
animation as an `active` v2 track targeted by the stable State id. Other slots
keep their own selected States and continue participating in flow.

`actor` is not an ambient Actor propagated into Stack descendants. It owns the
Lock Screen context and wallpaper. An Actor input declared by an Audio, Avatar,
message or future notification remains that item's Variant value unless the
designer explicitly forwards it. Equal input names or record types never create
an automatic binding.

## Layout

Wallpaper covers the whole screen. Visible bars keep their normal screen-edge
placement. Lock Screen calculates the remaining box between them and supplies
that box to Component Stack through the generic child preview-frame helper.
Consequently `fill` means the available content region, not the whole device
behind the bars. When either bar is hidden, Stack receives the released space.

Bars render after Stack so system chrome stays above any child visual overflow.
The module root clips the complete composition to the device screen.

## Wallpaper contract

Wallpaper belongs to Actor, not to Lock Screen or the System app. The preview
resolves the current Actor record before rendering and passes its complete
wallpaper contract (`kind`, `opacity`, Light/Dark images and Light/Dark fallback
colors). Persisted resolved Actor objects are removed during normalization so
stale partial previews cannot shadow the authoritative Actor record.

If an image path for the active appearance mode is empty, wallpaper rendering
uses that mode's Actor background color. Missing required current-model fields
remain errors; Lock Screen adds no opacity, image or color fallback.

## Preview boundary

Lock Screen resolver owns visibility and concrete Variant references. The module
renderable owns layer order and the available Stack frame. Component Stack owns
its child collection and deterministic placement. Each selected child resolver
receives the requested frame state before preview. The registry only routes,
and the generic renderer receives final groups, boxes, images and surfaces.

Before the Lock Screen resolver runs, the generic forwarding pass resolves all
promoted values recursively inside `stackInputs`, item overrides and deeper
component bindings. It consumes the transient `$forwardedInputs` metadata so
Stack and child resolvers receive only final per-frame values. Structured array
inputs and record references keep their declared types throughout this pass.
