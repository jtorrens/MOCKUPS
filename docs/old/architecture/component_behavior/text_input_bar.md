# Text Input Bar

Status: active system component. Owner: Text Input Bar contract/resolver/renderable.

Source of truth: `src/desktop-preview/textInputBarComponentContract.ts`,
`textInputBarComponentResolver.ts` and `textInputBarComponentRenderable.ts`.

## Purpose and ownership

Text Input Bar composes a messaging input surface from a Surface, Text Box and
two-state Icon Bar. It owns the bar's internal width allocation and the idle/
typing icon-state selection. Its parent owns screen placement.

## Runtime inputs and configuration

### Public runtime inputs

- Sample/current text.
- Available width supplied by the parent frame.

### Variant/configuration

- Bar height, bar padding pair and icon gap token.
- Surface, Text Box and Icon Bar concrete variant references and child
  overrides.
- Text Box placeholder, line policy and left/right icon-row configuration.
- Icon Bar idle/active content and action-icon configuration.

## Composition and dependencies

Text Input Bar depends on Surface, Text Box and Icon Bar. It asks Icon Bar for
the applicable idle or active state based on whether current text is empty.
The embedded Text Box owns text wrapping, cursor and its own optional icon-row
layout.

## Layout, sizing, clipping and z-order

- Available width is a parent input.
- Internal text width equals bar width minus bar edge padding, left/right icon
  zone widths and the configured icon gaps.
- With one text line, icon zones center vertically with the text field. With
  multiple lines, icon zones align to the bottom of the Text Box.
- The bar itself permits visual overflow required by child surfaces; the parent
  decides screen-level clipping and z-order.
- **Canonical parent rule:** Conversation anchors this component to the
  keyboard frame top/base. Text Input Bar never infers a keyboard's popup or
  visual bounds itself.

## States, triggers and timeline

- Idle when text is empty; active/typing when it is non-empty.
- It has no independent trigger or timer. The parent supplies revealed text,
  which makes state changes deterministic per frame.

## Motion

The component has no self-owned motion clock. Any movement into/out of a
composer region is owned by Conversation's layout/motion state.

## Parent data vs local resolution

The parent provides width and text. Text Input Bar resolves state, child
variant configuration, internal zones and local boxes.

## Defaults and limitations

- The active state is presently determined by trimmed non-empty text.
- Runtime icon content propagates through the embedded Text Box/Icon Bar
  contract; it must not be recreated as a Text Input Bar-specific picker.
