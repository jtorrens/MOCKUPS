# Keypad

Status: functional System component on the component resolver -> renderable
route.

Source of truth: `src/desktop-preview/keypadComponentContract.ts`,
`keypadComponentResolver.ts` and `keypadComponentRenderable.ts`.

## Responsibility

Keypad owns only an ordered grid of keys. Displays, instructions, Icon Bars and
other chrome belong to the parent composition, normally a Component Stack. A
telephone, PIN screen or calculator may therefore reuse the same Keypad
without inheriting unrelated top or bottom regions.

## Variant data

Each Keypad Variant owns:

- sizing mode: natural content width or available-width fill;
- positive column count and key size;
- tokenized X/Y padding, column gap and row gap;
- an ordered structured collection of keys;
- one shared icon-size token;
- one concrete embedded Label Variant shared by every key and state;
- background color, text/icon color, background alpha and border alpha for
  normal, active, pushed and disabled.

Every key has a stable id, emitted value, `text`/`icon`/`spacer` kind and a
disabled flag. Text keys contain primary text and optional subtext. Icon keys
contain an icon token. Collection order determines cells from left to right and
top to bottom. A spacer occupies its cell but emits no visual node. Duplicate
ids and duplicate non-empty key values are resolver errors.

The shared Label owns typography, subtext layout, padding, border color, border
width, radius, relief, shadow and its embedded Surface shape. State values only
override its background/text colors and background/border alpha. Icon keys use
the same state text color as their icon color and replace Label text content
without creating a second visual-style path.

The key collection uses the generic dictionary structured-collection editor
directly from the field contract. Keypad has no editor-specific collection UI.
The editor presents Layout and Keys as independent first-level cards. Only the
four visual states share vertical internal navigation, so resizing and key
content do not compete with state editing for horizontal space. Layout's Size
and Padding fields use the generic responsive pair controls: they remain
horizontal when space permits and reflow into two labelled rows in compact
editor panels. Embedded Label Variant selection uses the shared shrinking
Component/Variant control and keeps its Open and Override actions visible.

## Runtime inputs

- `availableWidth` supplies the width used only by fill sizing;
- `activeKey` selects a key by stable id or emitted value;
- `pushedKey` selects the key pressed in the requested frame;
- `enabled` disables all keys when false.

The resolver computes each key's final state before rendering using the strict
precedence `disabled > pushed > active > normal`. It resolves the shared Label
and applies the four explicit state values before the generic renderer receives
the frame.

Keypad does not own a digit sequence, PIN, phone number, press duration or
timer. A parent component/module resolves those behaviors for the requested
frame and supplies the final `pushedKey`. Persistent selection remains a
separate `activeKey`. If a reusable sequence later needs
timing, it must use the shared BehaviorTiming/timeline contract rather than a
Keypad-local timer.

## Composition boundary

Keypad explicitly embeds Label. Component Stack explicitly declares Keypad as
an allowed child. No Keypad branch or styling rule exists in the bridge,
generic renderer or MainWindow.
