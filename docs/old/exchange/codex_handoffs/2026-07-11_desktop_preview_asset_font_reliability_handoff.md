# Desktop preview asset and font reliability handoff — 2026-07-11

## Purpose

This handoff records the current uncommitted desktop-preview reliability work
so the main development thread can continue without reintroducing the first-play
asset failure or host-system typography fallbacks.

No commit or push was made from this thread.

## Reported symptom

Conversation `Play messages` started correctly, but its first visible playback
could remain on the previous/placeholder state. Avatar, wallpaper and message
media appeared only after a later update or full document load.

The action path was healthy. Logs contained normal `preview.playback.toggle`,
`preview.playback.start` and `preview.playback.tick` events. The failing path was
WebView asset presentation:

```text
image status=decode-error
patch status=skip
preload result unsupported type
```

## Confirmed root causes

### Partial data-URI interning

`CompactPreviewAssets` excluded `;` from its data-URI match. A source such as:

```text
data:image/jpeg;base64,...
```

was split after `data:image/jpeg`. The registry stored an incomplete invalid
URI while the HTML retained `;base64,...` after the internal asset key. The
WebView could neither find the registered key nor decode the resulting `src`.
The replacement patch was skipped and the old DOM stayed visible.

The match now captures the complete URI up to the real HTML/CSS delimiter,
including MIME parameters and encoded content.

Generated SVG data URIs may contain literal parentheses because
`encodeURIComponent` does not escape them. Parentheses are therefore not valid
generic data-URI terminators; compaction must continue to the quoted HTML/CSS
delimiter.

### Unsupported asynchronous preload result

`mockupsPreloadPreviewImages` was an async JavaScript function returned directly
through `NativeWebView.InvokeScript`. The bridge cannot marshal a Promise, so C#
reported `requested=0` and `loadedImages=0` even when browser events showed that
the images had loaded.

Preload now returns a simple request id immediately. JavaScript retains a
serializable `{ done, loaded }` result and C# polls it through
`mockupsPreviewImagePreloadResult`.

### Decode rejection treated as terminal too early

WebKit can reject `HTMLImageElement.decode()` before a valid large data URI
finishes and emits `load`. Decode rejection is now logged as diagnostic; load,
error or timeout determines the terminal status.

## DOM update rules after this change

The desktop host now follows this sequence:

```text
compact complete data URIs before the frame cache
  -> ask the active document which referenced hashes are missing
  -> register those asset keys synchronously
  -> parse compact body
  -> hydrate internal keys
  -> reject patch if any key is unresolved
  -> allow generic morph by stable renderable id, including resident image changes
  -> use decode-gated layer replacement only for structural mismatch
```

Do not retain a host-only "already registered" set as the authority. Full loads
replace the JavaScript `Map`, and navigation timing can otherwise leave C# and
the active document with different asset state.

Image events now report:

- owning `renderableId`;
- `assetHash` when interned;
- `srcKind`;
- source character count;
- natural width/height;
- load/decode status.

The generic WebView layer deliberately does not log component slot names. The
owning resolver's stable renderable id provides identity without leaking
component knowledge across the preview boundary.

## Follow-up: frames skipped during playback

After first-play correctness was restored, Conversation still skipped most
frames. The monotonic playback clock was behaving intentionally: visible DOM
updates took roughly 0.6–1.1 seconds, so latest-frame-wins discarded obsolete
frames instead of replaying a growing backlog.

Two generic costs were removed:

- data URIs are now interned immediately after Node rendering, before insertion
  into the shared frame cache; WebView patches no longer rescan and hash about
  7.7 MB for every visible frame;
- ahead prewarming now obeys the action's `prewarmFrames` declaration. A false
  value prevents the background renderer competing with interactive playback.

The DOM morph no longer forces full decode-gated layer replacement merely
because an image `src` changed. Asset registration and missing-key validation
already occur before morph, so matching generic trees can update the resident
source directly. Structural changes retain the safe replacement path.

The message strip directly below the preview now retains a summary after each
completed playback: presented/target frames, discarded frames and
average/minimum/maximum observed presentation FPS. These figures count
completed WebView frame updates rather than timer requests, and are also
emitted as `preview.playback.summary` for comparison with detailed logs.

The shared playback state disables the complete `Runtime Inputs → Test Values`
surface while any declarative preview action is preparing or playing. This is
generic action state; it does not branch on Conversation or component type.

## Raster playback phase started after checkpoint

Commit `25336725` was pushed before beginning this phase. Subsequent uncommitted
work adds the generic `full / tiles / hold` planner and a persistent Chromium
raster worker. The initial implementation rasterizes the complete declared
action as WebP quality 95 behind the cancellable loader, then presents cached
device-resolution bitmaps instead of invoking Node/DOM during playback. PNG
lossless is the GFX master-frame path. It is a fidelity/performance
checkpoint; replace the unbounded complete buffer with a bounded producer and
content-addressed tile/hold assets before treating 30-second shots as supported.

## Production font and emoji contract

The previous text stack explicitly appended host fonts:

```text
system-ui / Apple Color Emoji / Segoe UI Emoji / Noto Color Emoji
```

That made glyphs and layout dependent on the desktop or future playback device.
The render route now uses only:

```text
selected production text font
  -> theme production emoji font
```

Both come from payload `fontFaces`, backed by `production_fonts`. Missing font
ids, required faces or files fail visibly before HTML rendering. Text renderable
nodes without a resolved production family are rejected by the HTML adapter.

The active FOQN data currently declares SF Pro Text for normal text and
NotoColorEmoji for emoji. Do not restore a `system`, `system-ui`, Apple/Segoe
emoji or generic sans-serif fallback in `previewFontHelpers.ts`.

Theme typography now owns three explicit `production_fonts` references:
ordinary text, System-component text and emoji. The semantic selectors are
`theme` and `theme.system`. Generic resolution does not infer the selector from
the component manifest category.

Keyboard no longer exposes or stores a selectable font-family field. Its
registered `TypographySystemStyle` dictionary control edits weight, style, size
and line height while enforcing `fontFamilyId: "theme.system"`. Status Bar and
Text Input Bar explicitly use the same role in their owning component routes.
Navigation Bar currently emits no text.

Existing themes were migrated so `systemFontFamilyId` initially equals their
current `fontFamilyId`; seed/new-theme creation does the same. The two roles may
later be edited independently in Theme, but both must reference text-category
production fonts.

The persisted ordinary typography selector is either a concrete
`production_fonts.id` or the semantic value `theme`. The retired value
`theme.typography.fontFamily` is not a font id and must be migrated to `theme`,
not accepted as a runtime alias. The committed Keyboard config and variants
were migrated onward to `theme.system`, and the architecture check rejects the
return of either retired state.

The font used by desktop preview chrome and diagnostic placeholders is outside
the renderable Shot surface and is not part of this runtime contract.

## Architecture and future compatibility

`docs/architecture/26_shot_module_instance_contract.md` now contains only a
future compatibility declaration for on-set mobile playback. It is not a mobile
implementation phase.

Intent only:

- a future exported Shot/ModuleInstance package may run in local HTML/CSS on a
  mobile device;
- the anticipated on-set visual choice is effective `light` or `dark` mode;
- play/pause/seek are playback state, not Shot edits;
- deterministic frame resolution, explicit assets and production fonts must
  remain portable;
- desktop `InvokeScript`, DOM morph and `mockups-asset:` are adapter-local and
  must not enter the canonical Shot/Module contract.

No package format, mobile UI, transfer mechanism or persistence behavior should
be implemented yet.

## Files intentionally changed by this preview work

- `docs/architecture/26_desktop_preview_pipeline.md`
- `docs/architecture/26_shot_module_instance_contract.md`
- `docs/architecture/component_behavior/atoms.md`
- `docs/exchange/codex_handoffs/2026-07-11_desktop_preview_asset_font_reliability_handoff.md`
- `scripts/checkDesktopPreviewArchitecture.ts`
- `spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs`
- `src/desktop-preview/DesktopRenderableHtmlAdapter.tsx`
- `src/desktop-preview/previewAssetResolver.ts`
- `src/desktop-preview/previewFontHelpers.ts`

Earlier real-time work in the same dirty worktree also touches playback timing,
prewarm renderer isolation and stable DOM ids. Other local changes belong to
the user/another thread and must not be discarded or swept into a broad commit.

## Validation and next verification

Run:

```text
npm test
git diff --check
```

Then perform one live Conversation check from a freshly loaded preview:

1. Open the Conversation module instance.
2. Confirm avatar, wallpaper and initial media are present before playback.
3. Trigger `Play messages` once.
4. Confirm the first visible update advances without refresh.
5. Inspect `logs/desktop-preview-debug.log`.

Expected evidence:

- compact `bodyChars` is much smaller than `originalBodyChars` when repeated
  base64 assets exist;
- no invalid `mockups-asset:<hash>;base64,...` source;
- no `asset-missing` event;
- preload batches report the true requested and loaded counts;
- `image-decode-rejected` may occur, but a later `load` can still commit;
- image-source changes take the decode-gated `replace` route;
- stable frames without image-source changes may take `mode=morph`;
- text uses the payload's production text and emoji faces.

If a font fails, fix its `production_fonts` record/path/face metadata. Do not add
a system fallback.

## Typography measurement and action coverage closure

The conservative character-width estimator is no longer used by production
renderable layout. `previewTextHelpers.ts` now opens the payload's production
font files with `fontkit`, shapes text with the requested face/weight/style and
uses the declared production emoji face for emoji or missing glyphs. Parsing,
glyph-face decisions and shaped advances are process caches. Wrapping keeps
manual newlines and divides overlong words only between Unicode graphemes.

Text Box, Label, Media text overlays and Bubble status measurement all use this
common path. The HTML adapter remains paint-only and receives already measured
boxes and explicit wrapped lines; no component-specific typography logic was
added to the bridge or renderer.

`checkDesktopPreviewArchitecture.ts` now rejects production preview consumers
of the retired approximate helpers and validates the complete seeded Test
Values action inventory, including the mutually applicable per-message video
and audio actions. Keep new actions declarative in payload data and preserve
stable collection item ids.
# Final playback checkpoint

Desktop design preview now exposes three generic playback routes: HTML priority
FPS, HTML every frame (default), and physical-resolution raster. HTML assets are
interned as stable WebView blob URLs, structural morphs reconcile local children,
and HTML preload warms render frames plus image decoding under a circular loader.
Raster captures the final device resolution 1:1 and applies preview zoom only in
Avalonia presentation.

Measured warm results for the 25 fps Conversation action are 138/138 at about
27 fps for HTML every-frame, 135-137/138 at about 25 fps for HTML FPS-priority,
and 138/138 at about 25 fps for raster. Cold HTML remains 14-19 fps; this is an
accepted deferred WebKit/first-state cost. Do not reintroduce data-URI expansion
per morph or component-specific logic in the bridge to pursue it.

Nested media action follow-up: `playVideo` and `playAudio` share item fields, so
only the action applicable to the item's `mediaType` may write them. The generic
preparer now receives the requested action and targets the nested collection item.
Video HTML is validated at 73/73 frames and about 24 fps; extraction diagnostics
are routed to the repository-level preview log through the renderer environment.
