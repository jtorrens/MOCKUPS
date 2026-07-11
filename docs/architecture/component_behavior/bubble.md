# Bubble Component

Status: active component. Owner: Bubble contract/resolver/renderable.

Source of truth: `src/desktop-preview/bubbleComponentContract.ts`,
`bubbleComponentResolver.ts` and `bubbleComponentRenderable.ts`.

## Purpose and ownership

Bubble resolves one message surface for incoming, system or outgoing content.
It owns message-local layout, write-on state, palette-color selection, optional
media/audio composition, actor affordances and status display. Its parent owns
the message's place in a conversation and the shared frame/timeline.

## Runtime inputs and configuration

### Public runtime inputs

- `state`: incoming, system or outgoing.
- Text, maximum width percentage, write-on trigger/frame/duration.
- Actor record/name and optional actor-label/avatar visibility data.
- Status text and status state.
- Media type, source, viewport, scale, offset, playback time/duration,
  play/pause, full-frame state and controls elapsed time.

### Variant/configuration

- Surface, Text Box, Media, Audio, Label and Avatar concrete variant slots.
- Bubble padding and state-specific palette color pairs for background/text.
- Maximum message-line policy, status icons/colors/sizes and embedded-child
  placement settings.
- Whether actor label uses the active bubble color as an override.

## Composition and dependencies

Bubble declares dependencies on Surface, Text Box, Media, Audio, Label and
Avatar. It selects one optional media child at a time: image/video through
Media or audio through Audio. It does not make Media and Audio siblings for one
message.

## Layout, sizing, clipping and z-order

- Without media, the Bubble surface sizes to Text Box plus Bubble padding and
  status requirements.
- With media/audio, the selected child and text are composed top/bottom/left/
  right according to the media position. Vertical media can force a Bubble
  wider than text; text then starts at the Bubble's left content edge instead
  of being visually centered in the media width.
- Bubble maximum width is a percentage of the screen supplied at runtime.
- Actor Label can extend into the Bubble vertically. Its intrusion increases
  the Bubble's vertical surface/padding only; it must not move Bubble X.
- Avatar sits outside the Bubble content surface and does not change the
  Bubble's own frame. Actor Label and Avatar render only for incoming messages.
- Status text and checks align at the lower-right of the Bubble.
- Bubble's regular composition allows visible external avatar/label overflow.
  The conversation viewport applies the actual screen clipping.
- A Media full-frame state is elevated to a sibling overlay above Bubble,
  Avatar and Label, so it is not clipped by Bubble and has maximum local
  z-order.

## States, triggers and timeline

- State selects incoming/system/outgoing colors and tail behavior.
- Write-on reveals text by grapheme/frame; cursor is visible only while the
  reveal remains active and disappears after completion.
- Media and audio runtime states are passed to their child contracts rather
  than interpreted by Bubble itself.

## Motion

Bubble receives a frame/shared time from Conversation. Write-on duration is in
frames. Any embedded Media full-frame transition uses Media's motion contract.
Bubble does not own a second wall-clock timer.

## Parent data vs local resolution

Conversation supplies message runtime data, selected Bubble variant, frame,
frame rate and message placement. Bubble resolves state colors, child variant
selection, message-local sizing and child runtime inputs.

## Defaults and limitations

- The current Text Box child is resolved with a maximum line policy suitable
  for messages; future variants may expose a tighter message-specific limit.
- Emoji/font measurement and write-on preview intermittency remain known
  preview-quality issues. They must be fixed in common text measurement/frame
  handling, not with Bubble-specific fallbacks.
