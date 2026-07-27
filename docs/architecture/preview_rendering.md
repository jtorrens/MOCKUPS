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
source rather than a general database capability.

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

The dependency graph is collected recursively and each literal module
specifier is resolved with the TypeScript resolver before ownership is
compared. Static imports, exports, import assignments, `require` and dynamic
imports are covered. Computed module loads are invalid because their owner edge
cannot be proven structurally.

A renderable consumes only the state resolved for the requested frame. It never
reads the playhead, frame rate or animation document and never derives
write-on, playback, presence, fade or motion progress. A parent resolver may
project an already resolved child-local frame across an embedded boundary; the
child resolver then resolves that child frame before its renderable paints it.
Architecture validation rejects raw temporal evaluation in renderable owners.

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

Editable size controls and their contracts reject invalid or non-positive
dimensions. Resolvers repeat that validation for every non-visual source,
including Production and nested Runtime documents. Once geometry is resolved,
a renderable does not treat a child exceeding its assigned frame as an error:
it preserves fixed or intrinsic child dimensions and marks the bounded owner
viewport for clipping. The generic renderer only paints those boxes and the
resolved overflow policy.

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

## Render Queue boundary

Render Queue reuses the same prepared Production payload and generic web
renderer, but it is not a second Preview mode. Enqueue resolves every Shot
frame with an explicit Theme, Device and requested Light/Dark mode, renders a
clean raster document, and streams that frozen document into the queue-owned
content-addressed store. The visible queue receives `PREPARING` children before
this stream starts; preparation never builds an in-memory list of frame HTML.

The queue worker receives no Project database port, repository or current tree
selection. It uses the same document-to-raster owner as raster Preview through
its own persistent Chromium session, reads one frozen document per request and
then writes a MOV or image sequence. A repeated document hash reuses the
already generated lossless raster. The renderer still knows nothing about
queue state, output naming, Production Output paths or codecs.

Conversation keeps composer presence under its temporal owner. When an
explicit `text` track replaces the base write-on, the resolved bubble text and
the effective track duration remain separate: the former paints the message,
while the latter keeps Text Input Bar and Keyboard present for the outgoing
write interval.

## Preview sessions

Design and Production Preview keep only temporary presentation state:

- selected Preview tab;
- Test Values in isolated Design inspection;
- current playhead and playback status;
- preparation result cache;
- panel split and local controls.

Design Test Values are captured as one immutable, scope-keyed snapshot before
Preview preparation leaves the visual context. Scalar values and structured
collection documents travel together in that snapshot. Preview rendering and
the Test Values authoring surface must consume the same captured revision; they
must not read each other's mutable controls or keep parallel copies of the
active authored context. Preview-authoring document reads, transient
reconciliation and Runtime contract discovery run behind the session operation
coordinator. That preparation recursively follows declared embedded Runtime
contracts and closes their dictionary options, resources and exact Component
Variant references. It does not discover dependencies by names or visual
position. The visual shell shows the shared loading state and constructs
controls only from the prepared result whose selection revision is still
current.

Preview Setup resource options follow the same rule. Device options and their
exact metrics, Theme options and the Project media root are loaded together on
the session operation worker. The Preview controller retains only that
immutable Project snapshot. A later Project preparation cancels the previous
one, and visual refresh, playback preparation and reference browsing consume
only the latest committed snapshot without direct persistence reads.

The same preparation closes the complete current Production timeline catalog:
each Shot's frame rate, ordered Screens, exact frame ranges and keyframes, plus
each Screen's Variant config and each Shot's exact Actor-owned Device, Theme and
appearance context. Production navigation, context presentation, validation,
playhead controls, appearance selection, history subtitles and playback timing
consume only that catalog. They never recalculate or query the timeline or Shot
context from visual callbacks. A tree-changing command prepares the new tree
and refreshed catalog without publishing either. It commits the catalog and
tree revision together before selecting or rendering a new Shot or Screen. If
that preparation fails, is canceled or is superseded, the prior tree, catalog
and selection remain current.

Interactive render requests follow the same revision rule. After the external
renderer returns, the Preview host checks the request sequence before either
committing the result or publishing its error. A result or error superseded by
a newer interactive request is discarded; an invalid latest request still
reports the strict owner error and retains the last valid Preview.

Production payload remains owned by the Screen. Repeated Play with unchanged
inputs reuses the prepared HTML. Isolated Design actions use the same exact
reuse rule: the controller retains prepared frames only while their
cryptographic request signature still matches the resolved payload, action and
Preview setup. Completion leaves the final frame visible without discarding
that reusable preparation. Escape cancels both preparation and playback.
Production playback payload and signature frames are created and runtime-
resolved on the session operation worker. The visual controller captures the
request inputs, awaits the immutable frame list and never reads persistence
while iterating playback frames. Each playback tick selects its exact payload
from that prepared list by stable owner identity and absolute frame; it does
not submit a second payload-preparation operation that a later tick could
cancel. Preparation closes the static payload once per exact Screen and derives
only that Screen's frame-owned fields for its remaining frames; it does not
repeat Theme, Actor, resource or document reads for every tick. Cancellation
is checked between preparation frames.
Static Production refresh follows the same boundary: the selected Shot or
Screen, Theme mode and Shot frame are captured before payload construction and
Runtime resolution run on the worker. A newer selection, frame or setup revision
cancels the previous preparation, and only the still-current immutable payload
may update the Preview host or Production history. Production playback consumes
the already prepared first frame for setup and never constructs an additional
payload on the visual thread.
Closing the editor disposes the Preview session owner: Design and Production
preparation, ahead preload, playback timing, frame-cache reservations and the
external rasterizer lifetime are canceled or released before the window
becomes unreachable. A Preview operation may not outlive its window.
