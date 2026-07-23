# Keyboard

Status: active system component. Owner: Keyboard contract/resolver/renderable.

Source of truth: `src/desktop-preview/keyboardComponentContract.ts`,
`keyboardComponentResolver.ts` and `keyboardComponentRenderable.ts`.

## Purpose and ownership

Keyboard resolves a mobile keyboard frame, selected key mode, pressed key,
emoji substitutions and optional top/bottom Icon Bars. It owns key geometry and
keyboard-local visual states. Conversation owns the keyboard's screen position
and shared time.

## Runtime inputs and configuration

### Public runtime inputs

- Full revealed text.
- Current grapheme position/identity.
- Trigger/motion frame and shared motion time.

### Variant/configuration

- Language/layout rows, key colors/alpha, typography, padding, radius, border,
  shadow and pressed effect.
- Standard/special/emoji key scales and optional top/bottom Icon Bar variants.
- Typography weight, style, size and line height are variant values. Font family
  is not a Keyboard variant field: Keyboard always uses the Theme
  `systemFontFamilyId` through the explicit `theme.system` selector, followed by
  the Theme emoji font for emoji glyphs.
- Keyboard Surface and motion configuration.

## Composition and dependencies

Keyboard owns generated key rows and may embed Icon Bar above or below them.
It does not depend on Text Input Bar. The parent composes both as siblings.

## Layout, sizing, clipping and z-order

- Keyboard resolves a screen-width bottom keyboard frame; the parent translates
  that frame to the final composer location.
- Key rows use weighted distribution; special keys are compact variants.
- A pressed-key popup follows
  [contract 81](../81_keyboard_pressed_popup_composition_contract.md): its
  wider head, connector and pressed-key base form one continuous silhouette
  with one exterior shadow and one enlarged glyph.
- The popup remains inside the resolved Keyboard frame. Near either horizontal
  edge its head shifts while its connector continues to target the key center.
- Emoji mode omits normal special keyboard keys. Duplicate emojis from source
  text are removed and extra emojis are deterministically distributed.
- **Canonical parent rule:** Keyboard is above Text Input Bar, so popup/key
  visuals remain visible.

## States, triggers and timeline

- Mode is derived from the current grapheme: lower/shift/numeric/symbol/emoji.
- Exactly one key can be pressed per frame.
- **Canonical rule:** Conversation supplies the last revealed grapheme, not
  the first grapheme, to determine current mode and pressed key.
- During Conversation's post-write-on hold, Keyboard receives the completed
  text but no current grapheme, so no key stays pressed.

## Motion

Keyboard motion is configured by its selected motion config/token. The module
is the time source. Keyboard resolves its requested frame; renderer timers or
CSS animations are forbidden.

## Parent data vs local resolution

Conversation provides revealed text, last revealed grapheme/current position,
module frame/time and final frame placement. Keyboard resolves key rows, mode,
pressed state, emoji substitutions and internal motion atoms.

## Defaults and limitations

- Current resolver supports lower, shift, numeric, symbols and emoji modes.
- Conversation supplies the current revealed grapheme index while typing and
  shared module `motionElapsedMs` for keyboard motion.
