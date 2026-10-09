# Preview resolution and rendering

Status: normative.

Required Runtime record references remain explicit in Design Test Values as
well as Production. An empty required Actor reference is a contract error;
neither input-session preparation nor nested reference resolution selects the
first available Actor or synthesizes a sample Actor. A declared optional empty
reference remains empty. System Preview Actors resolve only through their
explicit fixture identities.

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
the subsequent declared visual-resolution stage; it joins global Palette ids
to the exact active Production's complete RGB value set and is not a parallel
config or Runtime path. Typography roles resolve through Theme; Preview rejects
direct Production Font ids outside Theme.

The TypeScript stage of that same boundary is
`runtimePreviewDocumentContract.ts`. It owns exact Variant/Override composition,
declared Runtime/config bindings, explicit parent/child value links and
forwarding. Component contracts declare bindings using stable field ids, exact
storage paths and, where needed, exact embedded boundary paths. Concrete
resolvers validate and interpret the resulting values; they do not select
alternative sources themselves. Missing declared Runtime values are errors;
`false`, zero, empty text and `null` are present values, not missing values.

Registry dispatch requires `PreparedRuntimePreviewPayload`, branded by a private
in-process symbol owned by the shared boundary. Serialized payloads cannot claim
preparation through a public boolean. Embedded preparation requires a prepared
parent and retains its complete temporal envelope. Raw requests enter through
the preparation boundary on every deserialization.
Transient Design state requires the exact owner id; display names and Component
types cannot manufacture a scope identity.

Forwarded scalar values are evaluated at the common temporal owner's frame
before registry dispatch. This does not replace the authored Runtime envelope:
presence, previous-value transitions and action clocks still receive their
original data. Static embedded inputs use declared, typed definition defaults
prepared by Application for the exact Variant; configured bindings and explicit
Runtime values take precedence. No Component manufactures a private set of
default Runtime values. Prepared configuration is isolated from its source
Variant and local Overrides, including nested objects and arrays.

A Theme-owned Component boundary receives its exact prepared Theme Variant
reference from the payload and its sparse owner-local Overrides from the
effective Module document. The shared Theme-boundary resolver validates the
declared Component type and forms one complete slot for the concrete resolver;
the Module never persists a parallel Variant reference or chooses a fallback.

Animatable Runtime record references are prepared once as an exact catalog of
the declared keyframe ids. Frame resolution selects the already prepared
record by its resolved stable id; it never re-reads persistence per frame,
derives a record from the Shot context or falls back to another Actor.

For every Shot frame, payload preparation selects all Screen lanes whose
extended interval is active. Each prepared layer carries its exact Screen
payload, the Shot-owned Motion, its entry/content/exit phase and signed
phase-relative clock. Time before the boundary holds Motion at progress zero;
entry holds the owner-local action at frame zero and exit
holds its final frame. The action delay also holds frame zero. The generic
Screen transition resolver composes those already selected owners in lane order
and reuses the common Motion geometry and easing with the Shot's exact duration.
Registries and concrete Module owners remain unaware of neighboring Screens.
The generic payload-layer contract is the single owner of transition-envelope
projection. Semantic reads use its highest ordered owner, transformations map
the declared owner layers and the envelope is then synchronized from that
primary owner while retaining its outer selection identity. A consumer never
copies an ad hoc subset of owner fields or rejects a Screen merely because it
is currently wrapped by entry, exit or overlap composition.

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
- the resolver owns validation, semantic interpretation and timing state of
  already prepared values; value-source selection belongs only to the shared
  Runtime Preview boundary;
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

Component-specific layout, behavior and resolved temporal state remain in the
owner. Definition defaults are prepared at the shared document boundary, never
recovered by a concrete resolver. If a change appears to require branching on a Component type in a
generic bridge or renderer, the responsibility belongs in the owner or a
parameterized generic primitive.

Editable size controls and their contracts reject invalid or non-positive
dimensions. Resolvers repeat that validation for every non-visual source,
including Production and nested Runtime documents. Once geometry is resolved,
a renderable does not treat a child exceeding its assigned frame as an error:
it preserves fixed or intrinsic child dimensions and marks the bounded owner
viewport for clipping. The generic renderer only paints those boxes and the
resolved overflow policy.

Media viewport dimensions remain the stable framing coordinate space when an
assigned layout box changes size. The assigned box owns clipping; it does not
replace the Media viewport or rescale its image. Authored and animated Media
scale multiplies that stable framing scale, while Media X/Y offset remains in
the same viewport-relative coordinate system. A collection reflow may resize
or move the clip, but it never rewrites or substitutes those Media values.

Generic Surface tail geometry belongs to the Surface shape helper. Every tail
anchors to its declared body edge and overlaps through the complete resolved
corner radius so tail and rounded body form one seamless silhouette for every
side, vertical position and tail style.

Surface resolves its signed Palette tint after its final semantic background
color, before producing either a generic surface node or tail SVG. For each
RGB channel, negative intensity blends toward screen (`1-(1-base)*(1-tint)`),
positive intensity toward multiply (`base*tint`), with absolute intensity as
the blend weight. Base alpha is preserved; zero preserves the exact original
color. Light/Dark backgrounds use the same operation. The bridge and web
renderer receive final colors and never interpret tint parameters or Button
state.

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
layout. Explicit Runtime Input forwarding separately carries the source
authoring target alongside the forwarded value. A leaf that paints that value
publishes its Runtime source target, while surrounding appearance boundaries
retain their Variant or local Override targets. Selecting a path level sends
that exact target to the desktop authoring
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
When that boundary is a Runtime collection item's declared
`ComponentVariantSlot`, its opaque target names the collection field, stable
item id, slot field and child record class. Desktop activates that exact
registered Runtime control and opens its existing Runtime Override source;
it never manufactures an `EmbeddedComponentSlotCatalog` entry for Runtime data.
An exact Screen owner with no embedded slot focuses its Screen Payload surface;
an embedded slot focuses the contextual Overrides editor for that slot chain.
Interactive Production Preview documents publish their exact Screen owner so
the same opaque target can reach Screen Payload, local Overrides or a complete
referenced Variant through the declared boundary chain. Navigation crosses to
the workspace that owns the exact target while the locked Preview retains its
Production context. Raster and Render Queue documents expose no authoring
target.

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

Job-start preparation, including asset capture, owns one complete asynchronous
operation on the session's shared `EditorOperationCoordinator`. Authored writes
queued during preparation wait until it finishes or is canceled. Encoding runs
outside that operation, so editing may continue while the worker renders the
coherent prepared revision. Every execution and retry reads current data again;
neither the queue item nor a previous execution supplies cached authored data.
This boundary serializes application-owned writes, not changes made to external
files by another application.

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
The persistent raster worker treats the incoming document head as part of that
exact frozen document. It may replace only the renderable body while both the
viewport and head are unchanged. A change to either performs a complete
document load before asset hydration and `document.fonts.ready`, so font-face
rules can never leak from or remain missing after another Screen or job.
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
Text Box paints that Cursor as a separate resolved child after text layout. The
cursor never participates in text measurement or wrapping and is not clipped
when its final-line position extends beyond the text or Bubble edge. The text
and Cursor consume the same resolved painted-line origin. Lines whose baselines
fall outside Text Box's clipped text viewport remain layout results only and
cannot move the independently painted Cursor.

Conversation Preview prepares message layout transitions by stable message id.
For each visible message it also resolves the `Actions revealed` hold value and
the Conversation Variant's reveal timing, then prepares one group containing
the displaced Bubble and its external actions Icon Row. Direction chooses only
which sibling side receives that prepared row. The generic renderer paints the
resolved group; it does not infer an action from the row's cosmetic icons.
At an appearance it resolves the previous and target vertical layouts once. At
an explicit disappearance, the Screen Runtime Input `Reflow at message Out
start` selects either the first Out Motion/Fade frame or the completed-Out
frame as that same reflow boundary. It then interpolates message displacement
and viewport overflow with the Conversation Variant's single reflow timing;
an exiting message remains in the painted set, receives the same resolved
reflow displacement as the adjacent surviving messages while its own
Motion/Fade continues, and disappears only when Out completes. The generic
renderer only paints the resulting geometry.
If an appearance or viewport-overflow event begins during that advanced Out
window, it does not replace the active disappearance reflow. The original Out
boundary remains authoritative and its track-end target contains the complete
message set visible at that frame.

Message starts reach Conversation only through the prepared common owner
timeline. That timeline consumes the declared Screen-instance positioning mode
and resolves either the signed serial delay or the Screen-local start frame.
The Conversation resolver, bridge and renderer do not choose a mode or
recalculate sibling positions.

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

`ComponentPreviewInputSession` has no repository or resource capability. It
owns only scoped temporary values, prepared action state and playback. The
controller captures that state on the visual thread; `DesignPreviewInputPreparer`
loads and prepares current Runtime inputs, exact Variant dependencies and
record/media values through `EditorOperationCoordinator`. Only the still-current
result is applied to the session and Preview. Static refresh and the start of
each Play execution use this same preparation; playback ticks present its
prepared frames without rereading persistence. Reset-on-completion actions
request a new static preparation, while Hold Final retains the prepared final
frame. Action timing is shared by input preparation, the session and frame
generation through `ComponentPreviewActionRuntimeValue`.

Play/Pause, Restore, frame stepping and frame selection carry the exact Design
owner as well as the declared action id, including nested item actions. They
enter the same operation queue as Runtime edits and Reset. Static and action
preparation capture the current temporary state on the UI thread only after
acquiring that gate, prepare it on the worker, and publish it on the UI thread
before releasing the gate. An action applies to that freshly prepared state
inside the same operation. Owner and selection-revision checks cancel obsolete
requests; an old control never redirects its action or transport queries to a
new owner, even if both declare the same action id. Controls await command
publication before refreshing their state. Frame generation and playback remain
asynchronous outside the mutation gate; the gate is not held during playback.

Scalar Test Value callbacks carry their exact owner, including when Preview is
pinned elsewhere or the owner's first preparation is pending. Structured item
edits enter the same operation queue and capture the latest temporary document
when their turn starts. Publication of the complete updated collection finishes
before releasing the queue, so two queued edits cannot overwrite each other
with snapshots captured before either edit was applied. Add, duplicate, move and
delete use that same queued capture/preparation/publication boundary, including
nested collections. The preparer reads the current declaration and applies the
shared structured mutation engine; the visual collection editor never mutates
a detached Test Values snapshot. A nested result publishes its complete root
collection while returning the addressed child collection to its control.

Root Runtime field edits use the same queue, including live Production drafts.
`RuntimeInputDocumentContract.UpdateValue` validates the declaration and applies
the field's declared effects through the common timeline owner. Design preparation
uses the active Theme and Project frame rate; Production uses its exact Screen
context. A field and every changed collection root are published together, with
one Preview refresh, before releasing the gate. The editor neither recognizes
positioning fields nor converts collections from its mounted document. A control
waits for its pending field publication before committing or rebuilding its panel.
Successful Production commits discard temporary field/collection values by the
exact edited owner identity, never by the active Preview scope; this also applies
before a first Preview is prepared and while another owner is pinned.

`Reset test values` enters that same operation queue with its exact owner even
when no temporary values have been published yet. It clears and publishes on
the UI thread before releasing the gate: earlier pending edits are discarded,
later edits start from the persisted baseline, and unrelated owners retain their
temporary values. The button awaits this operation without a synchronous bypass
or disabling editing to enforce ordering. Save-as-defaults acknowledgement is
not Reset: it must preserve changes outside the confirmed snapshot.

Design transport memory uses the exact owner scope and declared action id for
its live playhead, selected action and held-frame state, as it does for action
overlays and Restore origins. Leaving a prepared context stops its visual clock
without discarding that owner's transport memory. Returning restores its own
playhead and keeps a scrubbed or completed frame held; an identically named
action in another Variant cannot supply either value. Reset retires only its
owner's transport and origins, including pending frame preparation. Reset of a
different or unvisited owner never stops the visible transport, clears its
held frame or invalidates its pending Play.

Design `Save as defaults` and its dirty-state indicator capture that same
owner-keyed transient state, including structured item edits, rather than the
document originally captured by the mounted controls. Their document reads and
preparation run through the session operation coordinator; obsolete dirty-state
results cannot re-enable the action. Confirmation saves the prepared snapshot.
The owning document store holds the operation gate through persistence and
awaited UI acknowledgement. Only captured scalar values and complete collection
storage roots that still equal their confirmed values are cleared. Changed or
new values remain temporary, including later structural collection mutations;
other scopes and unsaved playback state are untouched. Successful acknowledgement
invalidates prepared default metadata (also used by dependent Variants) so
refreshing the new baseline does not treat retained drafts as an obsolete contract. No per-item merge or
second persistence path is introduced. Cancel or persistence failure acknowledges
nothing. After a successful write acknowledgement is completed even if shutdown
has requested cancellation. The next dirty-state preparation reads the newly
persisted baseline. Design collection mutations also consume the current captured state,
so duplicating or reordering an item cannot discard earlier temporary edits.
Transient collection documents are complete storage-root snapshots, not sparse
item overlays. The shared transient preparation applies them before Runtime
structure preparation, preserving added/deleted ids and ordering as well as
field values. The existing authoring-document projection removes resolved
presentation and playback fields before a structured mutation.

Design action membership is not a Runtime declaration change. The shared input
preparer compares complete action definitions independently by their stable ids;
the session retains those immutable signatures per exact owner across navigation.
Adding or reordering collection items does not reset scalar drafts or surviving
action state. A removed action retires only its session keys, restore snapshot
and playback position; a changed action contract resets only that action. The
prepared result publishes this retirement and the replacement values together
inside the same operation gate. Action-key construction is shared by preparation
and session publication; neither infers item ownership from names or positions.
Authored action-target snapshots are immutable preparation metadata for detecting
edits and initializing a requested action; they are not another Runtime value
source. Playback stores its target under the same action-key contract at every
boundary, including root actions. It never writes a target into the scalar draft
dictionary. Idle preparation leaves authored values untouched, and target edits
retire the corresponding playback overlay before preparing the effective document.

Scalar Test Values are sparse session edits: merely opening or preparing an
owner never stores its displayed values as temporary authoring. Each request
reads unedited values from the current prepared owner document through the
shared Test Values contract. Explicit edits remain owned by that exact session
scope, including empty values and edits equal to the current default; equality
does not remove ownership. Prepared scalar values stay in the Runtime document,
while the published `TransientValues` contains only explicit edits and action
transport state. The visual session retains no parallel scalar-default cache.
Default values are authoring data, not Runtime structure signatures. Updating
defaults from another Variant or while the owner stays mounted refreshes its
unedited values without clearing unrelated edits. Reset releases those edits
and prepares the current baseline, not the values captured on first visit.

The shared Runtime document boundary validates collection item keys from their
declarations, recursively through structured fields and embedded Runtime owners.
Allowed keys consist of the stable item identity, declared fields and resolved
reference keys, explicit boundary documents and parent links, and declared action
transport keys. Concrete resolvers must not maintain parallel item-field
whitelists: adding an authored field never requires changing a visual resolver.
Unknown keys still fail at preparation; the boundary does not discard or repair
them. Resolvers validate the visual semantics of the prepared values they consume.

Design preparation may resolve only the synthetic Actor and media identities
declared by the System Preview fixture catalog. The payload carries the exact
App Support fixture root. Production continues to reject System Preview Actor
identities and does not carry a Design fixture media root. Runtime preparation
preserves exact image, media-file and media-directory values, including nested
contracts and animation keyframes. Missing media is resolved to a visible
`Media ausente` notice; read/extraction failure produces `Error al leer media`.
Missing icons resolve to a red square of the declared icon size. Shared resource
helpers prepare these visuals and log nonblocking diagnostics; the generic
renderer only paints them, identically in Preview and export. No resource is
substituted by Design data, another root, a semantic name or an older frame.

The current-database validator applies Actor fixture isolation to the single
effective Production Runtime document before Preview resolution. Conditional
collection fields that are not enabled by their declared item discriminator
are inert: neither reference resolution nor fixture-isolation validation
consumes their placeholder value. Every Production Runtime write validates
that same effective document before persistence. Structured-item creation
first completes any recursively embedded required Actor through the shared
creation contract; media never blocks creation.

Preview Setup resource options follow the same rule. Device options and their
exact metrics, Theme options and the Project media root are loaded together on
the session operation worker for the coordinator's exact active Project.
Preview never selects a Project by tree order and never exposes a Device or
Theme from another Project. When the active Project has no Device or Theme, the
corresponding selector remains empty and Preview reports the missing resource.
The Preview controller retains only that immutable Project snapshot. A later
Project preparation cancels the previous one, and visual refresh, playback
preparation and reference browsing consume only the latest committed snapshot
without direct persistence reads.

The same preparation closes the complete current Production timeline catalog:
each Shot's frame rate, ordered Screen lanes, signed starts, exact effective
frame ranges, shared transition Motion and duration, action-shifted keyframes
and reference-video document, plus each Screen's action delay, action duration
and Variant config. Each Shot carries its exact Actor and one
effective Device; each Screen carries its exact authored Theme and sparse
non-geometric Device settings. The Screen document is applied only after the
Shot Device is resolved. Gaps resolve to an empty alpha-zero frame and overlaps
compose every active lane, painting the first/highest ordered lane last.
The Shot interval hard-clips transition layers. Motion time outside it is never
prepared or painted. A transition that crosses an outer cut contributes only
its intersection inside the Shot.
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
and playback duration on one committed revision after a Shot transition or a
Screen duration, delay, animation or Runtime collection change.
The shared authoring refresh coordinator selects this route from the effective
Preview workspace, not the editor's navigation workspace. A pinned Production
Screen therefore refreshes its catalog and Timeline while the editor is in
Design, retaining the pinned owner, playhead and mounted payload controls.
The collection editor retains its mounted view after its own successful scalar
writes only when the next prepared document matches that live view's values,
declarations, Variant config, dictionary resource context and animation. Local
collection values use the shared stable-id update contract. The retained editor
accepts the new prepared temporal context; changed structure, external values,
resources or animation rebuild the view instead of retaining stale callbacks.

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
Production payloads are loaded and runtime-resolved once per participating
Screen on the session operation worker into `PreparedProductionPreview`. This
request-scoped value document has no persistence capability. Static Preview,
Screen Play, multi-Screen Shot Play and job-start Render use its same frame
evaluation path. `ScreenTimelineTiming` owns action frames, delays and Motion
phases; the prepared document only projects that state into its Screen layers.
The playback signature hashes those complete prepared Screen documents, not a
second set of sampled payload reads. A new Render execution always prepares a
new document from current authoring; no document is persisted in a queue item.
The visual controller captures the
request inputs, awaits the immutable frame interval and never reads persistence
while iterating playback frames. Each playback tick selects its exact payload
from that prepared list by stable owner identity and absolute frame; it does
not submit a second payload-preparation operation that a later tick could
cancel. The sequence retains every requested frame, including an explicit null
payload for an alpha-zero Shot gap. A successful prepared-frame lookup with
that value is not a cache miss. Leading, intermediate and trailing gaps, complete
empty intervals and transitions between multiple Screens use the same prepared
HTML/raster route; no gap switches playback to per-tick database preparation.
Transparent frames use the common empty surface document and participate in
normal presentation acknowledgement, scrubbing and exact-range replay.
Preparation closes the static payload once per exact Screen and derives
only that Screen's frame-owned fields for its remaining frames; it does not
repeat Theme, Actor, resource or document reads for every frame, including
during initial preparation. Cancellation is checked between Screens and frames.
Focused port-counting tests enforce that frame evaluation and signature
generation perform zero persistence calls and that increasing the requested
frame count for the same Screen does not increase preparation reads.
The interval evaluates frame documents on demand, without allocating a document
for every frame before presenting the selected frame. Component catalogs contain
only the transitive embedded dependencies declared by the owner's Preview
manifest entry. One preparation reuses the catalog for Screens of the same
Module; a Screen's catalog is independent of the other Screens in the interval.
All Variants of each required Component remain available. Focused tests compare
scoped catalogs and rendered output against the complete catalog.
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
The Preview host has two presentation lifetimes. A Shot or Screen owner change
increments the owner revision, cancels pending presentation work, removes the
resident document immediately and shows `Preparando preview…`. A result from the
previous owner cannot publish, including a playback or raster result. Moving the
playhead within the same owner keeps the resident frame until the replacement
frame has rendered and committed. The WebView stages and decodes replacement
images before mutating the resident DOM, including its fast morph path. A stale
staged layer never commits. Browser regression tests exercise the real generated
document with delayed image responses and compare painted pixels through owner
changes, re-entry and transparent frames. Resident DOM patches already recalculate the
viewport, so they do not run the full document reflow polling loop; that loop is
reserved for a newly loaded Preview shell. Preparation logs record queue and
payload time separately so slow persistence reads remain distinguishable from
WebView presentation.
Closing the editor disposes the Preview session owner: Design and Production
preparation, ahead preload, playback timing, frame-cache reservations and the
external rasterizer lifetime are canceled or released before the window
becomes unreachable. A Preview operation may not outlive its window.
