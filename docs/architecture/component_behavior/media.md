# Media Component

Status: active component. Owner: Media contract/resolver/renderable.

Source of truth: `src/desktop-preview/mediaComponentContract.ts`,
`mediaComponentResolver.ts` and `mediaComponentRenderable.ts`.

## Purpose and ownership

Media renders a static image or a supplied video frame inside a reusable
surface, with inline/full-frame states, control Icon Bars and text overlays.
It owns media-local crop/viewport, controls and full-frame transition geometry.
It does not decode video or run playback.

## Runtime inputs and configuration

### Public runtime inputs

- Source URI and requested media kind (image/video).
- Viewport size, scale and X/Y offset.
- Playback state, current time and duration.
- Full-frame state, full-frame orientation and transition trigger/frame.
- Controls elapsed time and shared motion time.

### Variant/configuration

- Surface variant/overrides, control-bar height and icon-bar padding spacing
  pair.
- Inline and full-frame top/center/bottom Icon Bar variants.
- Optional global theme-token icon color override, while explicit action-icon
  overrides remain stronger.
- Idle/play text-overlay variants, typography, placement and count-up/down
  behavior.
- Motion configuration for the normal-to-full-frame transition.

## Composition and dependencies

Media depends on Surface and three Icon Bar zones. It can supply one selected
state for each zone (inline or full-frame), plus an optional text overlay.
Audio is a separate component and is selected by Bubble, not by Media.

## Layout, sizing, clipping and z-order

- Inline Media is clipped by its selected Surface shape/corners.
- Viewport, crop scale and offset are runtime values; the resolver produces the
  final media box from them.
- Full-frame grows from the center of inline media toward the screen frame and
  is not clipped by the Bubble container.
- To preserve correct clipping during expansion, the Media surface clips the
  visual media while it grows. At full-frame the parent elevates the Media node
  above Bubble/Avatar/Label as a sibling overlay.
- Top/center/bottom Icon Bars position within the current media frame.

## States, triggers and timeline

- Inline vs full-frame is explicit runtime state.
- Play/pause and current time are runtime state. For video, the resolver or
  upstream frame provider supplies the appropriate frame; web preview paints
  it as media data and does not own playback.
- Overlay text can be free text, count-up or count-down and has idle/play
  variants.

## Motion

The selected Media motion configuration controls the normal/full-frame
transition. Parent/module frame/time is the only clock. Controls fade uses
resolved elapsed state, delay and duration; it is not a web-renderer timer.

## Parent data vs local resolution

Bubble or another parent supplies source, state, viewport, timeline values and
selected Media variant. Media resolves crop, surface, control-zone selection,
overlay text and requested-frame geometry.

## Defaults and limitations

- A video source may be rendered as a static image frame, and an image source
  may be used in video mode as a stationary frame.
- Video preview buffering can still show delayed/blank frames on cold runs.
  The renderer must eventually use a deterministic preloaded frame sequence;
  this is a known preview-performance limitation.
