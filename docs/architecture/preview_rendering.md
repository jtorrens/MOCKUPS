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

`RuntimePreviewDocumentContract` is the single preparation path for both
Design and Production. It derives effective configuration from the exact
Variant and explicit Overrides, applies declared forwarding and structural
Runtime projection, then overlays the owner Runtime document (Design Test
Values or Production Screen content). A resolver consumes only that prepared
document and never chooses, merges, defaults or repairs a second Variant,
Override or Runtime value source. Theme-token and Palette resolution remains
the subsequent declared visual-resolution stage; it is not a parallel config
or Runtime path.

Animatable Runtime record references are prepared once as an exact catalog of
the declared keyframe ids. Frame resolution selects the already prepared
record by its resolved stable id; it never re-reads persistence per frame,
derives a record from the Shot context or falls back to another Actor.

For a Shot frame inside a Screen boundary transition, the prepared payload also
contains the exact outgoing and incoming Screen payloads, their complete Motion
documents and the shared non-negative elapsed interval. The outgoing payload is
fixed at its final owner-local frame; the incoming payload is fixed at local
frame zero. After the transition, payload preparation keeps that incoming frame
zero through the Screen action delay and then advances its local action frame.
The generic Screen transition resolver composes those already
selected owners and reuses the common Motion helpers. Registries and concrete
Module owners remain unaware of neighboring Screens.

`DesignPreviewPayload.ThemeMode` is authoritative when explicitly `light` or
`dark`. Session mode applies only when the payload has no explicit effective
mode. The renderer does not parse Module appearance settings.

Required Preview documents are validated as current JSON objects before
dispatch. A blank, malformed, absent or wrong-root required document is an
error. Optionality exists only when declared by the payload contract.

`RuntimeContractJson` remains the exact unresolved authoring contract after
forwarding, structural projection and timing preparation. Record-reference
objects such as a resolved Actor are added only to the separate
`DesignPreviewJson` render payload. Editing and structural reconciliation always
restart from `RuntimeContractJson`, so a render-only projection can never be
persisted back into a strict Runtime collection document.

When an already effective Preview document re-enters the authoring surface, the
generic record-reference owner removes only the exact `resolvedJsonKey` values
declared by its Runtime definitions, recursively through structured
collections and Design Test Values. Unknown fields remain errors. Authoring
therefore consumes stable record ids while render preparation alone owns the
resolved record objects.

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

Generic Surface tail geometry belongs to the Surface shape helper. Every tail
anchors to its declared body edge and overlaps through the complete resolved
corner radius so tail and rounded body form one seamless silhouette for every
side, vertical position and tail style.

## Bridge

The bridge translates only standard resolved values:

- Theme and Palette values;
- alpha and neutral tint;
- design or device units to final pixels;
- generic boxes, placement, text, images, SVGs, surfaces and shadows;
- generic validation for unresolved values.

Generic placement always resolves `mode` and alignment into the child's base
position and edge ownership first. The X/Y offsets then translate that resolved
position; they never reclassify a centred axis or transfer ownership to another
edge. Content reservation uses the same pre-offset alignment ownership while
measuring the translated child's actual depth at that edge.

Device scaling derives from the required positive Screen width. Preview has no
pixel-ratio or scale fallback and does not receive retired Device metrics.

After a Module owner has resolved its complete renderable tree, the shared
Module boundary applies the Device `moduleTransparency` policy. Wallpaper and
fallback background nodes identify themselves only through the generic
`moduleBackground` paint role. When the policy is enabled, that boundary
removes those nodes, measures the bottommost visible foreground paint in the
current frame after transforms and clipping, and resolves the gradient start.
Fixed mode uses the authored Device coordinate directly; variable mode adds
the signed offset to that pre-background measurement and takes the larger of
that result and the authored minimum fully-opaque extent before constructing
the mask. The boundary then inserts the resolved Palette surface with only its
authored background opacity, composes the unmodified Module foreground over
it, and attaches one fully-opaque-to-transparent vertical mask to the complete
Module root. Neither route branches on Module identity.

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

The HTML Preview and SVG/export adapters both consume the same strict vertical
opacity-mask primitive; renderers do not calculate the fade or inspect Device
configuration.

The interactive Preview Light/Dark selector is authoritative for every
Component, Module, Screen and Shot shown in that host, including Modules whose
authored `appearanceMode` is fixed. Authored Module appearance remains
authoritative only while preparing Production render jobs. Production Preview
exposes this session-only selector through its Mode context control instead of
replacing it with authored Module state. With Device
`moduleTransparency` enabled, the interactive Device shell uses a fixed black
matte for inspecting the masked result in either selected Theme mode; the clean
raster document remains transparent and contains no Device-shell matte. The
generic Preview canvas and Screen-transition roots omit their Theme background
while that policy is enabled, so neither boundary can flatten the Module mask
before it reaches the shell or raster output.

Preview owns two additional session-only inspection controls. `Grid` alternates
the interactive matte between black and a classic gray/white transparency
checkerboard. `Alpha` forces a black matte and converts the already composed
Preview visual to white while retaining its final alpha, yielding the exact
white/gray-on-black channel view. Neither control changes payloads, Device or
Theme data, transient render documents or Render Queue output. When either
inspection is active, Preview uses the HTML route for presentation even if the
playback preference is raster, because the native raster surface is not an
authoring output transform. Preview utility switches remain compact controls
without additional textual state content. Their track color and thumb position
are painted directly from `IsChecked`, independent of the active shell theme's
native switch template. The Production transport paints its Play and Pause
glyphs explicitly in white over the accent action surface.

The interactive desktop Preview host may inspect the generic
`data-renderable-id` and `data-renderable-type` attributes already emitted for
resolved nodes. Hover identification and the right-click rendered path are
host presentation only. Prepared Design payloads may additionally carry an
exact authoring owner id and an ordered list of declared embedded-slot field
ids, plus an optional exact visible dictionary field id and stable structured
item id. A full Component Variant reference replaces the prior authoring owner
and resets its slot chain; a `ComponentVariantSlot` keeps the current owner and
appends its declared slot. Renderable owners attach that opaque authoring target
at the boundary they own, and the generic HTML adapter exposes it without
interpreting Component types, Variants, Overrides, collection positions or card
layout. Selecting a path level sends that exact target to the desktop authoring
navigator, which selects the owner and resolves every slot through
`EmbeddedComponentSlotCatalog`. After the asynchronous prepared editor commit,
the desktop resolves an optional field id through that prepared layout, expands
the unique top-level card containing it and brings the card into view. The
registered control for a structured field consumes an optional stable item id.
Missing or ambiguous field-to-card or field-to-item matches warn and do not
infer a replacement.
Missing owners, unknown slots and malformed targets fail; no layer infers a
target from a renderable id, type, name, label, prefix, order or position. Each
renderable boundary must also match the exact current authoring
`recordClassId` before it may append its slot and move the scope to the declared
child record class. A child reached without its declared parent therefore
inherits the nearest valid target instead of publishing an invalid shortcut.
Production, raster and Render Queue documents expose no authoring target.

The resident desktop WebView boundary normalizes `InvokeScript` results before
Preview code consumes them. A plain result and the equivalent JSON string
literal returned by Windows WebView2 resolve to the same text, while numeric
and boolean results remain unchanged. DOM patch status, browser patch events,
asset queries, raster viewport geometry and image-preload responses all use
that one boundary; individual consumers never trim platform-specific quoting
or compensate by increasing Preview timeouts.

## Render Queue boundary

Render Queue reuses the same prepared Production payload and generic web
renderer, but it is not a second Preview mode. Enqueue stores only live plans
and leaves them `PENDING`; it does not resolve payloads or create frame data.
When the user launches the exact current pending set, each child independently
reads the latest Shot and Screens at its own start and prepares its explicit
Theme, Device and requested Light/Dark payloads into a temporary store.

The queue manager receives no Project database port, repository or current tree
selection. The preparation owner resolves the live plan through its focused
Production and Preview ports, then hands the resulting transient snapshot to
the worker. The worker uses the same document-to-raster owner as raster Preview
through its own persistent Chromium session, reads one prepared document per
request and then writes a MOV or image sequence. A repeated document hash within
that job reuses the already generated lossless raster. The transient snapshot
and assets are deleted after the job and never become queue persistence. The
renderer still knows nothing about queue state, output naming, Production
Output paths or codecs.
Each raster request declares the exact asset hashes referenced by that
document. The worker revokes browser object URLs outside that set, while the
Preview frame cache releases registry assets when their last cached document
is evicted. Render Queue execution resolves frozen assets directly from its
temporary snapshot store and never promotes them back into the process-wide
Preview registry.
The persistent HTML renderer returns the complete deduplicated asset set for
each generated document; it retains no cross-document asset catalogue.
The worker's output boundary converts the straight-alpha Chromium raster to
black-premultiplied RGB while retaining alpha in alpha-capable formats.
MOV conversion also owns complete frame and container color metadata. It
propagates limited range, BT.709 primaries, CSS sRGB transfer and BT.709 matrix
coefficients into encoded frames and the QuickTime `colr` atom. An alpha-capable
MOV tags its QuickTime video-media graphics mode as black-premultiplied.

The installed macOS application carries the exact Playwright runtime and
Chromium Headless Shell revision used by its raster worker. Packaging verifies
that bundled browser by launching it before signing and installation; raster
execution selects that application-owned browser directory explicitly and does
not depend on a developer checkout or a user Playwright cache.

Conversation keeps composer presence under its temporal owner. When an
explicit `text` track replaces the base write-on, the resolved bubble text and
the effective track duration remain separate: the former paints the message,
while the latter keeps Text Input Bar and Keyboard present for the outgoing
write interval. Conversation resolves each message's `keepCursorAfterWrite`
Runtime value for the current frame and forwards it explicitly to Bubble.
When Text Input Bar is active, a true value keeps that outgoing message in the
composer and suppresses only its Bubble. Later messages remain independent and
may appear on their own timelines. The first effective false is that message's
send boundary and releases its Bubble. Bubble still forwards cursor state to
Text Box, which owns the cursor's blinking presentation.

Conversation Preview prepares message layout transitions by stable message id.
At an appearance or completed explicit disappearance it resolves the previous
and target vertical layouts once, then interpolates both sibling displacement
and viewport overflow with the Conversation Variant's single reflow timing.
The generic renderer only paints the resulting geometry.

## Preview sessions

Design and Production Preview keep only temporary presentation state:

- selected Preview tab;
- Test Values in isolated Design inspection;
- current playhead and playback status;
- preparation result cache;
- panel split and local controls.

A locked Design Preview retains its exact stable owner identity. Component and
Module Variants remain full `ownerId::variant::variantId` references while the
editor selection changes; Preview never manufactures a parentless Variant node
or infers its owner from the new selection.

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
each Shot's frame rate, ordered Screen lanes, signed starts, exact effective frame ranges and
action-shifted keyframes, Shot reference-video document, plus each Screen's transition Motion, action delay,
action duration and Variant config. Each Shot carries its exact Actor and one
effective Device; each Screen carries its effective Theme and sparse
non-geometric Device settings. The Screen document is applied only after the
Shot Device is resolved. Gaps resolve to an empty alpha-zero frame and overlaps
select the first/highest ordered lane.
Production navigation,
context presentation, validation,
playhead controls, appearance selection, history subtitles and playback timing
consume only that catalog. They never recalculate or query the timeline or Shot
context from visual callbacks. A tree-changing command prepares the new tree
and refreshed catalog without publishing either. It commits the catalog and
tree revision together before selecting or rendering a new Shot or Screen. If
that preparation fails, is canceled or is superseded, the prior tree, catalog
and selection remain current.

An authored Preview mutation in Production follows the same catalog boundary,
even when the tree itself is unchanged. It invalidates prepared playback and
prepares a replacement Production catalog before the next interactive Preview
or Play request. This keeps the slider range, active Screen, payload frame list
and playback duration on one committed revision after a Screen duration,
transition, delay, animation or Runtime collection change.

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
The prepared Production playback remains valid until an owning authored input
or Preview visual setup changes explicitly. Play, pause, frame stepping,
selection changes and playhead movement do not validate it by rebuilding
payloads or signatures. When its exact owner and frame range still match,
replay starts directly from the session snapshot. The Preview timeline slider
uses the same snapshot for every value change while dragging, so each requested
frame may replace an obsolete render before pointer release.
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
