# Desktop preview pipeline

This document describes the current desktop design-preview path: how the editor
builds a payload, how the web renderer resolves one frame, which caches exist,
and which modules own each step.

The goal is operational. If preview playback is not close to real time, this is
the map for profiling and replacing bottlenecks without breaking the component
architecture.

## Real-time update implemented 2026-07-11

The desktop preview now uses a real-time-oriented update path while preserving
the resolver/renderable boundaries described below.

The implemented frame route is:

```text
monotonic project clock
  -> requested project frame
  -> component/module resolver
  -> generic renderable tree with stable node ids
  -> HTML for the resolved tree
  -> repeated data assets replaced by short asset keys
  -> generic WebView DOM morph by renderable id
  -> latest frame presented; obsolete pending frames discarded
```

The important operational rules are:

- playback time comes from elapsed monotonic time, not from the number of UI
  timer ticks that happened to run;
- the playback clock may be calculated internally in seconds, but action time
  fields in the payload keep their declared unit; for example `Play messages`
  writes `conversationFrame` as a frame number because its action declares
  `timeUnit = frames`;
- if rendering falls behind, the pending animation update is replaced by the
  newest requested frame; playback does not replay a backlog in slow motion;
- interactive rendering and prewarming use separate persistent Node processes
  and share the rendered-frame cache;
- every final renderable node exposes `data-renderable-id` from its existing
  generic `RenderableNode.id`;
- the WebView morphs matching generic element trees in place, synchronizing
  attributes and text; a structural mismatch falls back to the full body-swap
  path;
- image/video data URIs are registered once per loaded WebView document and
  subsequent frame bodies transport `mockups-asset:<sha256>` references;
- asset compaction captures the complete data URI, including MIME parameters
  and content after the comma; partial MIME-prefix replacement is invalid;
- the renderer compacts assets before placing a frame in the shared cache; a
  patch registers new assets before parsing that compact body and rejects any
  unresolved `mockups-asset:` reference;
- matching trees morph in place even when an image source changes because the
  referenced asset is already resident in the document registry; only a real
  structural mismatch uses the decode-gated layer replacement route;
- before every patch the host asks the active document which referenced hashes
  are missing and registers only those; C# must not assume its asset state is
  synchronized with the lifetime of the JavaScript document;
- the render cache key contains a SHA-256 request digest rather than retaining
  the complete serialized request as the dictionary key.

None of these mechanisms may branch on component type, component field, slot,
module type or animation kind. Component resolvers remain the only owners of
frame semantics. The WebView update layer only knows generic DOM elements,
stable renderable ids and opaque asset values.

Architecture enforcement in `scripts/checkDesktopPreviewArchitecture.ts`
protects the stable node id, monotonic clock, latest-frame policy, asset
registry and separate prewarm lane.

## Boundaries

The desktop preview is not a second rendering engine. The Avalonia shell owns UI
controls, context selection and WebView hosting. The TypeScript desktop-preview
runtime owns frame resolution and drawing.

The current route is:

```text
Avalonia editor selection / test inputs
  -> DesignPreviewPayloadDataSource
  -> DesignPreviewPayloadFactory
  -> WebDesignPreviewRenderer request
  -> persistent Node renderer
  -> component/module resolver
  -> component renderable module
  -> generic HTML adapter
  -> WebView full-load or DOM body patch
```

Important invariant:

- component-specific decisions stay inside the owning resolver/renderable;
- common helpers resolve generic tokens, colors, geometry, assets and text;
- the final web adapter only paints generic renderable nodes.

## Payload creation

Owners:

- `spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadDataSource.cs`:
  exact current database/context reads and typed source records;
- `spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs`:
  forwarding, effective runtime envelopes, Shot/Screen frame selection and
  final payload construction.

The route receives:

- selected tree node;
- selected theme;
- selected preview mode;
- typed current data through the dedicated payload data source.

It returns a `DesignPreviewPayload` for one of these contexts:

- `componentClass`;
- `module`;
- `moduleInstance`.
- a selected `shot`, resolved to its active `moduleInstance` for the requested
  Shot frame.

The payload includes:

- `kind`;
- `componentType` or module record class;
- `configJson`;
- `designPreviewJson`;
- `componentBaseConfigsJson`;
- `appConfigJson`;
- `instanceJson`;
- `themeTokensJson`;
- palette maps;
- project media root;
- icon theme root and mapping;
- production font faces;
- frame rate;
- selected effective theme mode.

For module instances, the factory resolves real shot/module context:

- module config comes from the selected module;
- app config comes from the instance app;
- `designPreviewJson` is the module's declared runtime contract populated with
  the concrete values from `module_instances.content_json`;
- `instanceJson.animation` comes from `module_instances.animation_json`;
- actor references are resolved into that same runtime envelope;
- `instanceJson.context` carries render context only, not a second content or
  behavior channel;
- frame rate comes from the shot.

For Shot preview, cut slots are sequential. The requested Shot frame selects
the active slot, is converted to its local module frame, and is written to the
module-declared `timelineFrameJsonKey`. Parent-owned timeline inputs are marked
`calculated`; they are not persisted as instance runtime values.

For module and component design previews, `designPreviewJson` comes from test
values via `DesignPreviewTestValues.RuntimeJson(...)`.

TypeScript shape:

- `src/desktop-preview/designPreviewPayload.ts`

## Preview frame geometry

Owner:

- `spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs`
- `spikes/desktop-editor-shell/Common/DeviceMetricRules.cs`

The renderer does not pass a device object into TypeScript. It passes resolved
preview frame geometry:

```json
{
  "canvasWidth": 0,
  "canvasHeight": 0,
  "screenX": 0,
  "screenY": 0,
  "screenWidth": 0,
  "screenHeight": 0,
  "scaleToPixels": 0
}
```

This is `previewFrame` in the TypeScript payload. Resolvers/renderables use it
as the final design surface and should not inspect device records directly.

Canonical mode changes this geometry before the payload reaches the web path:
the same component/module resolver receives a different preview frame.

## Rendering request and process model

Owner:

- `spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs`
- `src/desktop-preview/renderDesignPreviewHtmlServer.ts`
- `src/desktop-preview/renderDesignPreviewHtml.tsx`
- `src/desktop-preview/renderDesignPreviewMarkup.tsx`

`WebDesignPreviewRenderer.RenderBodyAsync(...)` creates a serializable request
from:

- device preview metrics;
- theme mode;
- marker visibility;
- the `DesignPreviewPayload`.

The renderer prefers a persistent Node process:

- dev route: `src/desktop-preview/renderDesignPreviewHtmlServer.ts` through
  `tsx`;
- packaged route: `desktop-preview/renderDesignPreviewHtmlServer.cjs` through
  bundled Node.

The persistent process uses line-delimited JSON over stdin/stdout:

```json
{ "id": "1", "payload": { "...": "..." } }
```

and returns:

```json
{ "id": "1", "ok": true, "html": "..." }
```

If the server script is missing or the process fails, the code falls back to the
one-shot renderer (`renderDesignPreviewHtml.tsx`) using a temporary JSON file.
That fallback is much slower and should not be part of a real-time path.

## TypeScript frame resolution

Owner:

- `src/desktop-preview/renderDesignPreviewMarkup.tsx`
- `src/desktop-preview/designPreviewRenderableRegistry.ts`
- `src/desktop-preview/componentClassRenderableRegistry.ts`
- `src/desktop-preview/moduleRenderableRegistry.ts`
- component/module resolver and renderable files.

The web runtime resolves exactly one requested frame.

The typical component route is:

```text
payload.configJson + payload.designPreviewJson
  -> component resolver
  -> component contract
  -> component renderable
  -> generic renderable tree
  -> DesktopRenderableHtmlAdapter
```

The module route is analogous, but starts in a module renderable. Conversation,
for example, composes status bar, header, messages, text input and keyboard
inside `src/desktop-preview/conversationModuleRenderable.ts`.

Animation is frame data. Resolvers calculate the state for the current frame
from values such as:

- `conversationFrame`;
- `writeOnFrame`;
- `motionElapsedMs`;
- `currentTimeSeconds`;
- action-provided time fields.

The web adapter must not run timers or CSS animations that affect component
state. If a thing moves, fades, writes-on or changes media frame, the resolver
must emit the resolved frame.

## HTML adapter and WebView update

Owner:

- `src/desktop-preview/DesktopRenderableHtmlAdapter.tsx`
- `spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs`

The TypeScript renderer returns HTML split into:

- font style block;
- preview body.

`DesignWebPreviewPane` uses two update paths:

1. Full document load:
   - used for first render;
   - used when context, chrome, fonts or non-animation state changed;
   - calls `NavigateToString(...)`.

2. DOM body patch:
   - used when the next update is animation-only relative to the last rendered
     update;
   - calls `window.mockupsSetPreviewBody(...)` inside the WebView;
   - avoids reloading the full HTML shell.

The current animation-only check lives in:

- `spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs`
  (`DesignPreviewUpdate.IsAnimationOnlyUpdateOf(...)`).

This distinction is important for real-time playback. Full WebView reloads are
not viable for sustained frame-by-frame playback.

## Playback and prewarming

Owner:

- `spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs`
- `spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs`
- `spikes/desktop-editor-shell/EditorShell/ComponentPreviewActions.cs`

Preview actions are declared in payload data. The editor does not know what a
module-specific action means; it executes the action contract.

The architecture check validates the seeded Test Values inventory: Conversation
`Play messages`; Keyboard `In`; Audio/Media `Play`; Media `Full screen`; Bubble
`Write-on`, `Play` and `Full screen`; plus Conversation message `Play video` and
`Play audio`. Collection actions are addressed by the item's stable `id` and
their `mediaType` applicability, never by array index or editor-owned rules.

An action clock reads its declared `durationInputId` from the same runtime Test
Values state applied to the payload. It must not use a separate action-private
duration fallback. Motion-derived actions run for the complete resolved motion
interval (`delayMs + durationMs`), and a non-frame-aligned duration always emits
one explicit terminal frame before playback state and controls are released.

For frame playback:

- `ComponentInputsPanel` triggers an action.
- `EditorPreviewController.PreparePlaybackFramesAsync()` builds the list of
  per-frame payloads.
- The first frames are pre-rendered before playback starts.
- Additional frames are pre-rendered ahead while playback continues.

Prewarm window constants:

- initial preload: 32 frames;
- ahead preload: 16 frames;

There is deliberately no animation-frame queue. At most one pending update is
kept, and a newer animation update replaces it.

The opaque preparation scrim belongs only to an active declarative playback.
Any static full refresh renders the requested frame normally and clears a
residual playback scrim.

Reference view mode, swipe, opacity and angle are overlay state. They update the
resident WebView layer directly and coalesce rapid slider changes to the latest
state; they do not regenerate component HTML. Design markers are likewise a
resident generic overlay toggled immediately. Enabling markers also renders the
same payload with generic renderable bounds and patches that body into the
resident document, so first activation gains element frames without blanking
the current static preview.

Renderable bounds use a repeating color palette keyed only by tree depth. This
makes parent/child nesting legible without exposing component types or slot
knowledge to the HTML adapter. Color is diagnostic and must never affect layout
or become part of a component contract.

The controller reserves frame-cache capacity for the initial and ahead window
before prewarming.

## Caches

### Rendered HTML frame cache

Owner: `spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs`

Cache key:

```text
renderer version + SHA-256(serialized render request)
```

The serialized request includes all payload data and preview frame geometry.
Changing any input, theme token, marks toggle, geometry, frame time or source
data changes the key.

Current capacity:

- default: 180 frames;
- max: 4096 frames;
- playback can reserve a larger window up to the max.

This cache stores full rendered HTML strings. It helps when playback revisits
the exact same frame payload, but it does not avoid the cost of producing new
frames.

The cache key is `renderer version + SHA-256(serialized request)`. The digest
keeps dictionary keys bounded even when the request contains large configuration
or theme payloads.

### Persistent Node renderer

Owner: `WebDesignPreviewRenderer.PersistentPreviewRenderer`

This is not a frame cache. It avoids process startup for each frame. Without it,
the one-shot route writes a temp file and launches a process per render.

### WebView image preload

Owner: `spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs`

During playback prewarm, the controller extracts image sources from generated
HTML and calls:

```text
window.mockupsPreloadPreviewImages([...])
```

The preload call returns a plain request id synchronously. The asynchronous
browser load stores a serializable `{ done, loaded }` result that the host polls
through `mockupsPreviewImagePreloadResult(...)`. Do not return a JavaScript
`Promise` directly through `NativeWebView.InvokeScript`; the host bridge cannot
marshal that result type.

The WebView then asks the browser image cache to load those URIs before playback
uses them. A rejected `HTMLImageElement.decode()` call is diagnostic rather
than an immediate terminal failure: WebKit may reject decode before a valid
data URI finishes and emits its authoritative load/error event.

### Video frame extraction cache

Owner: `src/desktop-preview/previewAssetResolver.ts`

For video sources:

- duration is cached by source path;
- extracted frame files are written under:
  - `${tmp}/mockups-video-frames/{sha1}.jpg`;
- in-process data URI results are cached in `videoFrameCache`;
- the last good frame per video path is retained as fallback.

Current in-process max video-frame entries: 240.

Frame extraction uses `ffmpeg` and duration reads use `ffprobe`. Candidate
executables are:

- `MOCKUPS_FFMPEG` / `MOCKUPS_FFPROBE`;
- `FFMPEG_PATH` / `FFPROBE_PATH`;
- Homebrew paths;
- system path.

This is a likely bottleneck for real-time video previews if requested frame
times are not already on disk and in memory.

### Asset and font loading

Owner: `src/desktop-preview/previewAssetResolver.ts`

Icons and local images are currently converted to data URIs during render.
Font faces are selected from requirements inferred from config and component
base configs, then emitted as `@font-face` entries.

Renderable text may use only production font ids supplied by the payload. Theme
typography declares three explicit production-font roles:

- `fontFamilyId` for ordinary text;
- `systemFontFamilyId` for text owned by System components;
- `emojiFontFamilyId` for emoji glyphs.

Typography uses the explicit selector `theme` or `theme.system`; generic font
resolution never infers a font from component category. Keyboard does not expose
its own font selector and always stores `theme.system`. Status Bar and Text Input
Bar also declare `theme.system` in their owning resolver/composition route.

The resolved CSS stack contains the selected production text/system font followed
by the theme's production emoji font. It must not append `system-ui`, platform UI
fonts, Apple/Segoe emoji fonts or any host-system fallback. Missing font ids,
required faces or font files fail visibly before HTML rendering. This preserves
glyph and metric parity across preview, deterministic export and future
portable runtimes.

Generic layout measurement uses the same production font files before creating
renderable boxes. `fontkit` parses each face once per renderer process, selects
the declared weight/style, shapes consecutive runs to retain kerning and
ligatures, and switches to the declared emoji face only for emoji or missing
glyphs. Wrapping preserves explicit line breaks and splits long content only on
Unicode grapheme boundaries, so accents, combining sequences and joined emoji
remain intact. Face selection and shaped advances are cached; components must
not add their own width estimator or host-font fallback.

Static faces must be shaped directly even though the font library exposes a
generic variation method for them. A requested weight is applied through
`getVariation` only when the selected file declares a `wght` variation axis,
clamped to that axis range.

Measured wrapping uses the complete resolved text frame width. The retired
heuristic's 12% safety contraction/inflation must not be restored: it produced
early wraps that did not align with renderable bounds even when font shaping was
exact. Status and sibling slots may size the parent surface but do not reduce a
Text Box's declared wrapping width.

When a vertically composed media slot makes a Bubble wider than its text's
natural width, Bubble performs a second normal Text Box measurement using that
final interior width. This lets text reflow across the available Bubble width;
the status remains a separately aligned sibling and never supplies the text
wrap constraint.

Node initially emits data URIs, but the desktop renderer interns them before a
frame enters the shared cache. Cached and transported frame bodies therefore
contain stable `mockups-asset:<sha256>` references instead of repeated binary
payloads.

WebView asset interning is a desktop transport optimization. It is not part of
the Shot/Module payload contract and must not become a requirement of future
export or playback adapters.

## Reference overlay

Owner:

- `spikes/desktop-editor-shell/EditorShell/PreviewReferenceOverlay.cs`
- `spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs`
- `spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs`

Reference view is WebView chrome, not component resolver output.

The reference state includes:

- source path;
- view mode (`preview` or `split`);
- swipe;
- opacity;
- angle;
- preview frame;
- frame rate;
- media kind.

The overlay is applied after the preview HTML is loaded or patched through:

```text
window.mockupsSetReferenceOverlay(...)
```

The current reference layer stretches to the device/screen preview area and sits
above the generated preview when split/reference mode requires it.

## Remaining real-time limitations

The current architecture is correct but not optimized for real-time frame
production. Main cost centers to measure:

1. Full HTML generation still occurs in Node per uncached frame.
   - Every unique frame currently becomes a complete HTML body string.
   - Even with DOM patching, the body content is regenerated.

2. Data URIs still exist transiently inside Node output.
   - C# interns them once immediately after rendering and stores compact HTML in
     the rendered-frame cache.
   - A future asset-URI provider can remove the remaining transient Node/C#
     allocation and hashing cost.

3. Video frame extraction.
   - Cold ffmpeg extraction is not real-time.
   - Disk-hit frame reads still become base64 data URIs.

4. Cold production-font shaping.
   - Production faces, glyph-face choices and shaped advances are cached in the
     persistent renderer process.
   - A cold renderer still pays initial font parsing and shaping before steady
     cache reuse.

5. Structural WebView fallbacks.
   - Any update that is not detected as animation-only reloads the full
     document.
   - Matching trees morph in place. Frames that add/remove/reorder nodes fall
     back to the compact full-body replacement path.

6. JSON serialization and hashing still happen for every request.
   - Cache keys are now bounded digests, but constructing the serialized request
     remains measurable work.

7. The rendered-frame cache stores complete but asset-compacted HTML strings.
   - Long timelines can still create managed-memory pressure from repeated DOM
     structure, but no longer duplicate multi-megabyte image data per frame.

8. Prewarming is action policy.
   - An action with `prewarmFrames = false` must not launch either initial or
     ahead-of-playback prewarming. Background rendering must not compete with
     the interactive lane contrary to that declaration.

## Candidate directions for real-time playback

These are design directions to evaluate separately:

### Raster streaming route

The accepted playback/export direction is a generic raster presentation cache
above the deterministic HTML/CSS renderer:

```text
resolved renderable frames
  -> generic node comparison
  -> full / tiles / hold frame plan
  -> interchangeable raster backend
  -> buffered desktop playback / MOV / on-set package
```

`src/visual/renderable/rasterFramePlan.ts` owns the renderer-independent dirty
region plan. It compares final nodes by stable id and visual value, includes
both old and new bounds for moved/removed nodes, aligns changes to fixed tiles
and promotes large changes to full frames. It contains no module/component
names. Actual bitmap capture and streaming remain adapter responsibilities.

The persistent Node renderer also interns data URI assets before writing its
response. It returns compact HTML plus only newly observed `{ key, uri }`
manifest entries. The desktop host registers those entries before caching the
frame. One-shot rendering retains host-side compaction as a recovery path.

The active raster checkpoint uses one persistent Playwright Chromium worker on
macOS and Windows. It receives the same resolved HTML/CSS, production fonts and
assets, fixes the viewport/device scale to the device canvas, waits for fonts
and images, and captures the resolved root through Chrome DevTools Protocol.
The worker loads the document head and embedded production font faces once. On
later frames it parses and replaces only the resolved canvas from `body`; font
data must never be reparsed per frame. Content-addressed media is registered
once as stable browser blob URLs and reused by all following frame bodies.
Preview frames are WebP quality 95 at the final physical device resolution in a
temporary disk cache. They are never downscaled during capture. Avalonia places
the physical bitmap over the exact viewport rectangle calculated by the WebView
and applies `fit` or the selected preview zoom only at presentation time; zoom
changes therefore do not invalidate raster content. The GFX path uses PNG
lossless from the same 1:1 Chromium surface. Avalonia never renders HTML or
recreates visual nodes; during playback it only decodes and presents the cached
bitmap. The native WebView supplies the viewport geometry before playback and
is hidden while bitmaps are presented because native-control airspace always
paints above Avalonia siblings; it is restored on the next non-playback refresh.

The first integration still prepares the complete action under the existing
cancellable loader. This validates fidelity, capture portability and
presentation FPS before introducing a parallel producer. It is not the final
package model: long-shot support must use content-addressed full/tiles/hold
assets and streaming from the temporary disk cache. Desktop playback already
uses a bounded decoded window (18 frames ahead and 2 behind) and releases
evicted bitmaps instead of decoding on the presentation tick or retaining the
whole shot in memory.
The loading surface is a scrim above an active WebView. It must not make the
WebView pane invisible because WebKit may suspend image/decode timers and stall
frame commits or raster capture while hidden.
The opaque in-WebView scrim reports completed/total raster frames from the
generic declared action timeline; it must not use component-specific progress.
The bounded streaming implementation must retain decoded bitmaps only for its
active window and release them after eviction.

Playback scheduling samples the monotonic project clock at twice the target
frame rate and presents only when the logical project frame changes. This avoids
platform dispatcher quantization (for example, a nominal 40 ms timer becoming
roughly 52 ms) without building a late-frame queue or changing shot duration.

The desktop playback-route selector is generic and offers three explicit
policies. `HTML · Priority FPS` follows the monotonic project clock and may skip
late frames. `HTML · Every frame` is the design-preview default and uses a
commit handshake: it advances one logical project frame only after the WebView
confirms presentation of the preceding frame, preserving order even when
wall-clock playback becomes longer. `Raster · Every frame` prepares physical-resolution bitmaps and uses the
buffered player at the project FPS. Route policy belongs to the preview host;
component resolvers and action contracts do not branch on it.

Collection-item actions are filtered by their declared applicability before
runtime values are written. Hidden sibling actions may share `playInputId` and
`timeJsonKey`, but must never overwrite the applicable action. Frame preparation
receives the concrete requested action and writes time/play values through its
collection target; it must not assume top-level fields or select the first action.
This is what allows message-scoped video/audio playback to use the same generic
HTML/raster pipeline as a module timeline action.

HTML routes prewarm the compact rendered-frame cache before playback. The WebView
is not mutated during frame rendering; all referenced image assets are then
decoded once in the WebView before the playback clock starts. DOM morphing compares interned assets by hash
and reconciles child insertions/removals locally; a structural text/emoji change
must not rebuild or decode unchanged wallpaper, avatar or media subtrees.
Interned WebView assets are materialized once as stable browser blob URLs rather
than retained as multi-megabyte `data:` attributes. Preload and morph use the
same blob URL so a cold structural transition does not trigger a second decode.

The preview-card cadence light uses a rolling presentation window shared by all
routes: red means measured FPS is below the project target, green means it is
within two percent of target, and blue means it is above target. Loading may use
the same blue brush, but it is not included in playback performance summaries.

### Measured checkpoint (2026-07-11, 25 fps Conversation action)

The accepted desktop checkpoint after warmup is:

- HTML every-frame: 138/138 frames, no discarded frames, approximately 27 fps;
- HTML FPS-priority: 135-137/138 frames, approximately 25 fps;
- physical-resolution raster: 138/138 frames, no discarded frames,
  approximately 25 fps.

A nested 73-frame video action is also validated at 73/73 frames with DOM patch
updates around 10-12 ms and approximately 24 fps in HTML every-frame mode. The
raster route prepares the same action-specific resolved frame sequence at final
device resolution before playback.

The first HTML play remains approximately 14-19 fps even after render-cache,
image-decode and blob-asset prewarming. Measurements indicate a cold WebKit /
first-state cost rather than steady morph cost; stable hash-aware morphs are
typically around 4-5 ms. Further cold-start work is deferred because the warm
interactive and raster routes meet the current design-preview target and added
renderer/process complexity is not justified by this checkpoint.

Future portable shot packaging should declare the production font ids and the
set of text/emoji glyphs used by the complete light/dark sequence. The packager
may create deterministic font subsets once per package, including a small emoji
subset, but it must never fall back to host-system fonts or change font faces
during playback.

1. Direct renderable delta instead of HTML string per frame.
   - Keep a persistent preview document.
   - Send compact frame deltas or final renderable JSON.
   - Let the browser patch attributes/text/images directly.

2. Asset URI indirection before HTML generation.
   - Serve local assets through stable `mockups.local` URLs or file URLs.
   - Stop embedding repeated image/icon/video-frame data URIs in every frame.

3. Pre-resolved timeline cache.
   - Resolve component contracts/renderable trees for a playback span once.
   - Reuse immutable assets/layout and vary only animated fields.

4. Dedicated video frame provider.
   - Extract or decode frames asynchronously ahead of playback.
   - Return stable local frame URLs instead of data URIs.

5. Text measurement cache.
   - Cache measured/wrapped lines by text, font id, size, line height and max
     width.

6. Expand renderer concurrency only from measurements.
   - Interactive and prewarm work already have isolated persistent processes.
   - Add more prewarm workers only if Node frame production remains below the
     required project FPS after the transport changes are measured.

## Performance acceptance criteria

Measure playback using `logs/desktop-preview-debug.log`. For a 25 fps project:

- the requested frame must be derived from real elapsed time;
- no pending-frame backlog may exist;
- repeated frame `bodyChars` after asset compaction should be orders of
  magnitude smaller than `originalBodyChars`;
- steady matching frames should report patch event `mode=morph`;
- median end-to-end frame time should be at most 25 ms and p95 at most 40 ms;
- preview clock drift should remain below two project frames;
- missed frames may be skipped, but must never be replayed late.

For 50/60 fps projects, target 8-12 ms for the WebView morph portion and keep
end-to-end p95 inside the project frame interval. These are acceptance targets,
not claims that every current component already meets them; video extraction
and structural frame changes still require measurement.

## Files that govern the pipeline

Desktop shell:

- `spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs`
- `spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs`
- `spikes/desktop-editor-shell/EditorShell/ComponentPreviewActions.cs`
- `spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs`
- `spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs`
- `spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs`
- `spikes/desktop-editor-shell/EditorShell/PreviewReferenceOverlay.cs`

Common/device:

- `spikes/desktop-editor-shell/Common/DeviceMetricRules.cs`
- `spikes/desktop-editor-shell/Common/PreviewDebugLog.cs`

TypeScript preview entrypoints:

- `src/desktop-preview/designPreviewPayload.ts`
- `src/desktop-preview/renderDesignPreviewHtmlServer.ts`
- `src/desktop-preview/renderDesignPreviewHtml.tsx`
- `src/desktop-preview/renderDesignPreviewMarkup.tsx`
- `src/desktop-preview/DesktopRenderableHtmlAdapter.tsx`

Registries:

- `src/desktop-preview/designPreviewRenderableRegistry.ts`
- `src/desktop-preview/componentClassRenderableRegistry.ts`
- `src/desktop-preview/moduleRenderableRegistry.ts`
- `src/desktop-preview/desktopPreviewComponents.ts`

Generic helpers:

- `src/desktop-preview/componentRenderableCommon.ts`
- `src/desktop-preview/componentResolverCommon.ts`
- `src/desktop-preview/previewAssetResolver.ts`
- `src/desktop-preview/previewColorHelpers.ts`
- `src/desktop-preview/previewFontHelpers.ts`
- `src/desktop-preview/previewGeometryHelpers.ts`
- `src/desktop-preview/previewMotionHelpers.ts`
- `src/desktop-preview/previewTextHelpers.ts`
- `src/desktop-preview/previewTextRevealHelpers.ts`

Active module/component owners:

- `src/desktop-preview/conversationModuleRenderable.ts`
- `src/desktop-preview/bubbleComponentResolver.ts`
- `src/desktop-preview/bubbleComponentRenderable.ts`
- `src/desktop-preview/textBoxComponentResolver.ts`
- `src/desktop-preview/textBoxComponentRenderable.ts`
- `src/desktop-preview/textInputBarComponentResolver.ts`
- `src/desktop-preview/textInputBarComponentRenderable.ts`
- `src/desktop-preview/keyboardComponentResolver.ts`
- `src/desktop-preview/keyboardComponentRenderable.ts`
- `src/desktop-preview/mediaComponentResolver.ts`
- `src/desktop-preview/mediaComponentRenderable.ts`
- `src/desktop-preview/audioComponentResolver.ts`
- `src/desktop-preview/audioComponentRenderable.ts`

Architecture guard:

- `scripts/checkDesktopPreviewArchitecture.ts`
