# Preview resolution and rendering

Status: normative.

## Complete route

Preview resolves one exact authored context:

```text
selected Design Variant or Production Screen
→ typed data sources
→ payload factory
→ explicit context and Runtime Input forwarding
→ exact manifest route
→ owner contract/resolver
→ owner renderable
→ common resolved primitives
→ generic bridge
→ generic web renderer
```

The complete resolution happens before painting. Preview is never a second
source of persisted truth.

## Typed data boundaries

Cross-domain reads use narrow, explicit boundaries:

- `DesignPreviewPayloadDataSource`
- `ModuleInstanceTimelineDataSource`
- `ActorPreviewDataSource`
- `ProductionShotContextDataSource`
- `RuntimeInputOptionsDataSource`
- `PreviewVisualContextDataSource`
- `ProductionPreviewSessionDataSource`
- `ComponentPreviewInputDataSource`
- `ModuleInstanceAnimationDocumentStore`
- `RuntimeInputOwnerDocumentStore`
- `RuntimeInputInstanceDocumentStore`
- `DictionaryFieldContextDataSource`
- `EmbeddedComponentDocumentStore`
- `EditorPresentationContextDataSource`

These boundaries supply current records or documents and contain no semantic
fallbacks. The payload factory is the only database-facing boundary for Design
Preview payload construction. Timeline services consume their focused data
source rather than the general database facade.

## Payload preparation

The payload boundary owns:

- exact Project and selected owner identity;
- Design fixture or persisted Production payload;
- full selected Variant references;
- effective Actor, Theme, Device, fonts, icons and wallpaper context;
- explicit Runtime Input forwarding;
- complete runtime-contract temporal envelope;
- requested Shot and Screen frame.

`DesignPreviewPayload.ThemeMode` is authoritative when explicitly `light` or
`dark`. Session mode applies only when the payload has no explicit effective
mode. The renderer does not parse Module appearance settings.

Required Preview documents are validated as current JSON objects before
dispatch. A blank, malformed, absent or wrong-root required document is an
error. Optionality exists only when declared by the payload contract.

## Manifest and routing

`src/desktop-preview/desktopPreviewManifest.json` is the current registry of
Component and Module identity, category, entrypoint and embedded dependencies.
It is the complete executable catalog of current Preview owners, not a migration
ledger. The current schema contains only fields with an observable routing or
ownership consequence.

Registries:

- match exact stable ids;
- call the declared owner;
- fail for an unknown or missing route.

They do not perform forwarding, defaults, config merging, token resolution,
layout, renderable construction or fallback presentation.

## Concrete behavior authority

For every manifest entry, concrete behavior has one executable owner chain:

- the contract owns required inputs and accepted current shapes;
- the resolver owns validation, semantic resolution, defaults and timing state;
- the renderable owns composition and final generic geometry;
- `embeds` owns the permitted concrete child dependencies;
- focused characterization tests own the observable examples and edge cases.

The active documents specify rules shared across owners. They do not duplicate a
hand-maintained per-Component catalog that could drift from the executable
manifest. Architecture validation requires every manifest identity to have its
declared owner files, exact registry route, permitted dependency edges and
committed database parity. A behavior change is incomplete until its focused
tests change in the same revision.

## Component and Module ownership

Every Component follows:

```text
Component contract/resolver
→ Component renderable
→ common Preview helpers
→ generic renderer
```

Modules own their Screen composition through the same boundary. Common helpers
do not import concrete Component owners. A parent may import an embedded child
only when that dependency is declared and the parent explicitly owns the slot.

Component-specific layout, defaults, behavior and animation remain in the
owner. If a change appears to require branching on a Component type in a
generic bridge or renderer, the responsibility belongs in the owner or a
parameterized generic primitive.

## Bridge

The bridge translates only standard resolved values:

- Theme and Palette values;
- alpha and neutral tint;
- design or device units to final pixels;
- generic boxes, placement, text, images, SVGs, surfaces and shadows;
- generic validation for unresolved values.

It contains no Component-specific layout or business rules.

## Renderer

The web renderer paints final resolved nodes. It knows nothing about:

- inheritance or Variants;
- database records or JSON persistence;
- Theme token names;
- Runtime Input forwarding;
- Component defaults;
- per-Component layout or timing.

New rendering needs are expressed as generic resolved primitives.

## Preview sessions

Design and Production Preview keep only temporary presentation state:

- selected Preview tab;
- Test Values in isolated Design inspection;
- current playhead and playback status;
- preparation result cache;
- panel split and local controls.

Production payload remains owned by the Screen. Repeated Play with unchanged
inputs reuses the prepared HTML. Escape cancels both preparation and playback.
