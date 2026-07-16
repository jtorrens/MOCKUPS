# Audio Component

Status: active component. Owner: Audio contract/resolver/renderable.

Source of truth: `src/desktop-preview/audioComponentContract.ts`,
`audioComponentResolver.ts` and `audioComponentRenderable.ts`.

## Purpose and ownership

Audio renders a message-style audio player: Surface, play control, waveform,
progress knob, remaining-time text, optional Avatar and optional Badge. It owns
audio-local geometry and visual progress. It does not own real audio decoding
or a playback timer.

## Runtime inputs and configuration

### Public runtime inputs

- Available width, duration, current time and playback mode.
- Actor data passed explicitly to the optional Avatar.
- Badge visibility, Icon/Text content mode, icon/text value, diameter and
  palette background/content colors.

### Variant/configuration

- Surface variant, padding spacing pair, play-circle geometry/colors, waveform
  count/gap/min/max heights, progress-knob size and duration-text styling.
- Optional Avatar and generic Badge concrete Variant slots plus placements.

## Composition and dependencies

Audio depends on Surface, Avatar, Label and Badge. The Avatar is placed relative
to the audio player; Badge is placed relative to Avatar and has higher local
z-order. Waveform bars, knob, play circle and duration text are Audio-owned
atoms rather than separate reusable components.

## Layout, sizing, clipping and z-order

- Available width comes from parent.
- The waveform shares vertical alignment with play circle; knob centers on the
  waveform.
- Remaining time aligns below/right of waveform.
- Avatar may reserve left or right horizontal space. Badge overlays Avatar with
  a higher z-index.
- Surface sizes around audio content plus padding. External Avatar/Badge may
  overflow according to their placements.

## States, triggers and timeline

- Progress is `currentTime / duration`, normalized to valid duration range.
- Played waveform bars and knob position derive from that progress.
- Remaining duration text is derived from duration minus current time.
- Playback state/timing is supplied by parent. Audio itself has no separate
  play trigger or independent temporal source.

## Motion

Audio uses parent-supplied requested frame/current time. If a parent animates
audio playback, it supplies successive contract values. The web renderer never
counts down autonomously.

## Parent data vs local resolution

The parent provides width, time, actor and Badge runtime data. Audio resolves
player geometry, waveform sample shape, progress visuals and optional child
placement. No Actor value propagates from an outer parent by matching its name;
it must be bound or forwarded explicitly.

## Defaults and limitations

- Waveform heights are deterministic from component identity, allowing stable
  preview output without source audio analysis.
- `once` clamps at the source duration; `loop` wraps source time. The parent
  owns the finite playback hold, so an audio loop never creates an unbounded
  module duration.
