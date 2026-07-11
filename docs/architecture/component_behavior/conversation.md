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
  status, actor/media data, timing and visibility settings.
- Module timeline inputs: current frame and optional composer-transition
  trigger/time.
- Header data: title/subtitle and actor-derived avatar data.

### Variant/configuration

- Selected concrete variants for Status Bar, Navigation Bar, Keyboard, Text
  Input Bar, Bubble and header Avatar.
- Header visibility/layout, wallpaper usage, screen gutter, message gap,
  composer placement policy and message viewport motion configuration.
- Default message behavior such as reveal policy and tail/head framing.

The module configuration chooses reusable variants; individual message content
and actor/media values remain runtime data.

## Composition and declared dependencies

Conversation composes, from back to front:

1. optional wallpaper;
2. optional header, which may bleed visually behind Status Bar;
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
  has a stable `id`, and `testValues.messages[]` stores only id-matched
  overrides. The preview payload receives the merged collection.
- A message can have delay, write-on duration, post-write-on hold,
  state/direction, media playback state and status state. Per-message delay,
  write-on and hold remain part of the ordered sequence because they define the
  rhythm of each message.
- Outgoing bubble reveal policy, incoming reveal policy and composer visibility
  while writing are global Conversation runtime behavior. They are not repeated
  in each message item.
- Incoming messages use the global `incomingRevealMode`: `instant`, `writeOn`
  or `typingIndicator`. System messages are centered and ignore write-on.
- Outgoing messages use the global composer behavior. Text Input Bar and
  Keyboard remain visible through the message `postWriteOnHoldFrames` after the last
  revealed grapheme before dismiss/reveal timing completes.
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

## Motion and shared time

Conversation owns the shared source of time: module frame plus the inherited
frame rate. Children receive the appropriate frame/time/state derived by the
module. There is no independent `motion time` per message. Component motion is
governed by the selected component's motion configuration/token and resolved
for the requested module frame.

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
- Current Conversation derives and passes the final revealed grapheme index and
  shared module `motionTimeSeconds` to Keyboard.
- Message editor grouping is declared by the runtime field metadata for the
  `messages[]` collection. Explicit hierarchy metadata on the message item
  itself remains optional while array order is the canonical sequence.
