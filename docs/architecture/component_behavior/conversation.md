# Conversation Module

Status: active module. Owner: `conversationModuleRenderable.ts` and the
Conversation module contract.

Source of truth: `src/desktop-preview/conversationModuleRenderable.ts` and the
module records/instance payload adapters that call it.

## Purpose and ownership

Conversation composes a complete messaging screen from reusable component
variants. It owns screen-level sequencing, the shared temporal context,
message collection order, frame regions, and placement of system chrome. It
does not own the internal layout of Bubble, Keyboard, Text Input Bar or their
embedded children.

## Runtime inputs and configuration

### Public runtime inputs

- Module/shot context: device, theme, color mode, orientation, screen frame,
  frame rate, owner actor and optional wallpaper.
- Message collection: ordered message records with direction/state, text,
  status, independent `actorId`, media data, timing and visibility settings.
  A message actor never inherits the header/shot actor implicitly; this keeps
  group conversations representable through the same payload contract.
- Module timeline inputs: current frame and optional composer-transition
  trigger/time.
- Header data: actor-derived identity, runtime subtitle and independent
  left/right Button collections. The title is resolved by the selected Avatar
  variant from the actor; Conversation does not duplicate it as a text input.

### Variant/configuration

- Selected concrete variants for Keyboard, Text Input Bar, Bubble, header
  Avatar and the two header Icon Rows. Status Bar and Navigation Bar variants
  come exclusively from the active Theme; Conversation owns only their
  visibility switches.
- `appearanceMode`: `inherit`, `light` or `dark`. `inherit` follows the
  selected device/theme preview mode; a forced value becomes the effective
  payload mode before any Conversation child resolves its variants.
- Header visibility/layout, Avatar alignment (`left`, `center`, `right`) within
  the space left by both Icon Rows, wallpaper usage, screen gutter, message gap,
  composer placement policy and message viewport motion configuration.
- Default message behavior such as reveal policy and tail/head framing.

The module configuration chooses reusable variants; individual message content
and actor/media values remain runtime data.

## Composition and declared dependencies

Conversation composes, from back to front:

1. optional wallpaper;
2. optional header, which may bleed visually behind Status Bar and composes a
   vertically centred left Icon Row, Avatar and right Icon Row;
3. Status Bar;
4. clipped message viewport containing ordered Bubbles;
5. Text Input Bar;
6. Keyboard;
7. Navigation Bar.

Bubble owns its nested Text Box, optional Media/Audio, Label and Avatar.
Keyboard and Text Input Bar own their own nested icon bars. Conversation must
declare these component dependencies through its selected variants; it must not
recreate their internal rules.

## Layout, sizing, clipping and z-order

- Header may extend behind Status Bar, but its layout height starts below the
  Status Bar region.
- The message viewport is bounded between header/status chrome and the
  composer/keyboard area. It clips messages that leave that region.
- Incoming bubbles align to the left gutter, outgoing bubbles to the right,
  and system bubbles center within the message viewport.
- **Canonical rule:** Text Input Bar anchors to the keyboard frame's top/base,
  not to the visual bounds of an individual key, popup, shadow or overflow.
- **Canonical rule:** Keyboard is above Text Input Bar in z-order so a pressed
  key and popup remain visible over the bar.
- Navigation Bar is above Keyboard.

## States, triggers and timeline

- Messages are an ordered runtime collection. Each message must declare its
  hierarchy/order and editor groups so the module/instance editor can expose a
  stable collection rather than separate hard-coded message fields.
- In Design Test Values, `messages[]` is a sourced runtime collection:
  `sourceCollectionJsonKey` points at the base message array, each base item
  has a stable `id`, and the current editor session keeps only id-matched
  in-memory overrides. The preview payload receives the merged collection.
  Test Values never persist in the module record; `Save as defaults` is the
  explicit operation that writes their current values to the base payload.
- A message can have delay, write-on duration, post-write-on hold,
  state/direction, media playback state and status state. Per-message delay,
  write-on and hold remain part of the ordered sequence because they define the
  rhythm of each message.
- Video and audio attachments additionally expose `playbackMode` (`once` or
  `loop`) and `playDurationFrames`. `durationSeconds` remains the physical
  source duration; it is not the duration of the timeline event. Design action
  playback uses `playDurationFrames` and a local `playbackFrame`.
- Outgoing bubble reveal policy, incoming reveal policy and composer visibility
  while writing are global Conversation runtime behavior. They are not repeated
  in each message item.
- Incoming messages use the global `incomingRevealMode`: `instant`, `writeOn`
  or `typingIndicator`. System messages are centered and ignore write-on.
- Outgoing messages use the global composer behavior. Text Input Bar and
  Keyboard remain visible through the message `postWriteOnHoldFrames` after the last
  revealed grapheme before dismiss/reveal timing completes. The keyboard releases
  its pressed key at the end of write-on; the hold shows completed text without a
  stuck key.
- Attachments/media do not appear during message write-on or typing-indicator
  phases; they enter with the completed message.
- A message visible interval extends through its last animated event, not only
  through write-on.
- Conversation derives the revealed composer text from the active outgoing
  write-on message.
- **Canonical rule:** Text Input Bar and Keyboard receive this same revealed
  text. Keyboard uses the **last revealed grapheme** to select its mode/layout
  and pressed key, never the first character.
- Trigger buttons in Test Values invoke the same public runtime action data as
  a module instance. They do not define a preview-only animation protocol.
- The concrete ModuleInstance editor is generated from this same runtime
  contract. Its scalar values and sourced `messages[]` collection persist
  directly to the instance payload; there is no separate Conversation-specific
  message editor.
- `Play video` and `Play audio` run only for their finite declared
  `playDurationFrames`. A loop repeats the source inside that interval and
  cannot create an unbounded preview run.

## Motion and shared time

Conversation owns the shared source of time: module frame plus the inherited
frame rate. Children receive the appropriate frame/time/state derived by the
module. There is no independent `motion time` per message. Component motion is
governed by the selected component's motion configuration/token and resolved
for the requested module frame.
The module declares `conversationFrame` as its calculated
`timelineFrameJsonKey`; Shot preview supplies the local slot frame and never
persists that calculated value in the instance payload.

## Parent data vs local resolution

Conversation supplies selected variants, screen frame, actor/runtime message
data, ordering and shared temporal position. Each child resolver resolves its
own concrete geometry, tokens, clipping and per-frame atoms from that supplied
data.

## Defaults, limitations and discrepancies

- Design Test Values reproduce the same `messages[]` payload shape as a module
  instance. The renderer does not synthesize messages from legacy direct
  preview fields.
- Current composition sets explicit z-order with Keyboard above Text Input Bar
  and anchors Text Input Bar from the Keyboard base/frame top. These canonical
  rules are implemented.
- Conversation passes the current revealed grapheme index to Keyboard only
  while write-on is active; it passes no pressed grapheme during the post-write
  hold while retaining the completed composer text.
- A sourced collection can declare `itemActions`. The editor renders those
  actions only on matching item cards and executes the payload-declared
  callback/state fields. Conversation uses this for `Play video` and `Play
  audio`; `Play messages` remains the module timeline action.
- The future general animation evaluator will compute screen duration as the
  maximum endpoint of the ordered message sequence and every finite property
  event, including media `isPlaying` holds. It must not sum media loops into
  the module duration.
- Message editor grouping is declared by the runtime field metadata for the
  `messages[]` collection. Explicit hierarchy metadata on the message item
  itself remains optional while array order is the canonical sequence.
