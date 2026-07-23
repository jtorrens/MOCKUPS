# Standard Editor Text Input Interaction Contract

Status: normative.

This contract governs pointer and selection behavior for editable text surfaces
in the desktop editor, including dictionary fields, numeric controls and dialog
inputs.

## 1. Shared ownership

`EditorTextBoxBehavior` owns the common interaction policy for every configured
editor `TextBox`. Numeric text surfaces opt into their additional selection rule
through `EditorNumericTextStyle`; individual editors and dialogs must not add
local pointer-selection handlers.

Configuration is idempotent. Reconfiguring a templated inner `TextBox` must not
attach duplicate text-input or pointer handlers.

## 2. Standard interaction

Mouse behavior remains the native Avalonia `TextBox` behavior:

- a primary click places the caret;
- primary-button drag selects text;
- Shift-click extends the current selection;
- double-click selects a word in ordinary text;
- keyboard selection, clipboard operations and caret navigation remain native.

Touch retains Avalonia's touch selection and selection-handle behavior.

Avalonia 12 treats Pen input through its touch path, which moves the caret
during a drag instead of extending a text selection. The shared editor behavior
therefore adapts only a primary Pen contact to desktop text selection:

- press establishes the selection anchor;
- movement extends `SelectionEnd` while the pointer remains captured;
- release preserves the final selection;
- capture loss clears the transient interaction;
- barrel, eraser, right and middle actions are not converted into selection.

This adaptation is session-only interaction state. It does not change or
persist field values.

## 3. Numeric double-click

Every numeric text surface selects its complete current value on a primary
double-click. This applies consistently to standalone numeric `TextBox`
controls and the inner text editor of `NumericUpDown`.

`NumericUpDown` must not intercept multiple clicks before its inner `TextBox`
receives them. Numeric selection is declared through the shared numeric text
style rather than inferred from an editor, field name, record class or visual
position.

## 4. Preserved boundaries

- Editable values still follow the dictionary and generic commit path.
- Pointer selection never commits, parses, repairs or normalizes a value.
- Deferred commit, Enter commit and lost-focus commit remain unchanged.
- Read-only text remains selectable.
- No editor-specific code belongs in `MainWindow`.
- No database, resolver, payload, Preview or renderer behavior changes.

## 5. Enforcement

Architecture checks require the shared Pen press/move/release/capture-loss
handlers, the shared numeric double-click opt-in and the absence of the retired
`NumericUpDown` multiple-click interception.

Manual validation covers mouse and Wacom selection in plain text, numeric
dictionary fields and `NumericUpDown`, including double-click replacement,
Shift extension, copy/paste and selection in both directions.
