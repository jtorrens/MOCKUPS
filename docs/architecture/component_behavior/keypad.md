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
- concrete embedded Label Variant slots for normal, active and disabled keys.

Every key has a stable id, emitted value, primary text, optional subtext,
`key`/`spacer` kind and a disabled flag. Collection order determines cells from
left to right and top to bottom. A spacer occupies its cell but emits no visual
node. Duplicate ids and duplicate non-empty key values are resolver errors.

The key collection uses the generic dictionary structured-collection editor
directly from the field contract. Keypad has no editor-specific collection UI.

## Runtime inputs

- `availableWidth` supplies the width used only by fill sizing;
- `activeKey` selects a key by stable id or emitted value;
- `enabled` disables all keys when false.

The resolver computes each key's final normal, active or disabled state before
rendering and resolves the corresponding embedded Label Variant with the key's
text and subtext. The renderer receives resolved labels and grid geometry only.

Keypad does not own a digit sequence, PIN, phone number, press duration or
timer. A parent component/module resolves those behaviors for the requested
frame and supplies the final `activeKey`. If a reusable sequence later needs
timing, it must use the shared BehaviorTiming/timeline contract rather than a
Keypad-local timer.

## Composition boundary

Keypad explicitly embeds Label. Component Stack explicitly declares Keypad as
an allowed child. No Keypad branch or styling rule exists in the bridge,
generic renderer or MainWindow.
