# Desktop preview pipeline

This document describes the current desktop design-preview path: how the editor
builds a payload, how the web renderer resolves one frame, which caches exist,
and which modules own each step.

The goal is operational. If preview playback is not close to real time, this is
the map for profiling and replacing bottlenecks without breaking the component
architecture.

## Boundaries

The desktop preview is not a second rendering engine. The Avalonia shell owns UI
controls, context selection and WebView hosting. The TypeScript desktop-preview
runtime owns frame resolution and drawing.

The current route is:

```text
Avalonia editor selection / test inputs
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

Owner: `spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs`

The factory receives:

- selected tree node;
- selected theme;
- selected preview mode;
- database access.

It returns a `DesignPreviewPayload` for one of these contexts:

- `componentClass`;
- `module`;
- `moduleInstance`.

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
- `instanceJson.content` comes from `module_instances.content_json`;
- `instanceJson.behavior` comes from `module_instances.behavior_json`;
- `instanceJson.animation` comes from `module_instances.animation_json`;
- `instanceJson.context.ownerActor` is resolved from the shot owner actor;
- frame rate comes from the shot.

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
- `motionTimeSeconds`;
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

For frame playback:

- `ComponentInputsPanel` triggers an action.
- `EditorPreviewController.PreparePlaybackFramesAsync()` builds the list of
  per-frame payloads.
- The first frames are pre-rendered before playback starts.
- Additional frames are pre-rendered ahead while playback continues.

Current constants:

- initial preload: 32 frames;
- ahead preload: 16 frames;
- max queued animation updates in WebView pane: 240.

The controller reserves frame-cache capacity for the initial and ahead window
before prewarming.

## Caches

### Rendered HTML frame cache

Owner: `spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs`

Cache key:

```text
renderer version + serialized render request
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

The WebView then asks the browser image cache to load those URIs before playback
uses them.

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

Because data URIs are embedded in HTML, large local images can inflate frame
HTML size and make DOM patching heavier.

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

## Current real-time limitations

The current architecture is correct but not optimized for real-time frame
production. Main cost centers to measure:

1. Full HTML generation per frame.
   - Every unique frame currently becomes a complete HTML body string.
   - Even with DOM patching, the body content is regenerated.

2. Large embedded data URIs.
   - Images, video frame JPEGs and SVG icons are embedded in the generated HTML.
   - This increases string allocation, serialization, WebView script payload and
     DOM update cost.

3. Video frame extraction.
   - Cold ffmpeg extraction is not real-time.
   - Disk-hit frame reads still become base64 data URIs.

4. Text layout and wrapper approximations.
   - Current text layout is CPU-side approximation per frame.
   - It is acceptable for correctness work but may need caching per text/style
     tuple.

5. WebView full loads.
   - Any update that is not detected as animation-only reloads the full
     document.
   - Real-time playback must stay on the DOM-patch path or a future direct
     scene-update path.

6. JSON request hashing and cache key size.
   - The frame cache key uses the entire serialized request.
   - Large payloads make key construction and dictionary comparisons more
     expensive.

## Candidate directions for real-time playback

These are design directions to evaluate separately:

1. Scene graph diff instead of HTML string per frame.
   - Keep a persistent preview document.
   - Send compact frame deltas or final renderable JSON.
   - Let the browser patch attributes/text/images directly.

2. Asset URI indirection.
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

6. Worker renderer pool.
   - The current persistent renderer is serialized through one process.
   - A pool can pre-render ahead frames in parallel, but only after asset access
     and cache contention are controlled.

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
