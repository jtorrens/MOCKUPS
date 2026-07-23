# Keyboard and text input audit

Status: current audit

This audit records the current state of the generic keyboard and text input bar
work so future module changes do not accidentally collapse editor, resolver, and
preview responsibilities back together.

## Current flow

The Chat module owns the runtime behavior decision:

- `module_instances.behavior_json.showTextInputBar` enables the text input bar
  behavior.
- `module_instances.behavior_json.showKeyboard` enables keyboard behavior, but
  the keyboard only appears while there is an active outgoing write-on message
  and the text input bar is visible.
- Incoming and system messages do not drive the keyboard.
- System messages ignore write-on display behavior and should appear as complete
  text.

The resolver prepares render-ready state:

- `resolveChatScreen` finds the active write-on outgoing message.
- It injects the current visible draft text into `textInputBar.text`.
- It computes `keyboard.pressedKey` from the next character.
- It hides the outgoing bubble until write-on completes when the text input bar
  is driving composition.
- It resolves keyboard and text-input icon tokens against the active icon
  theme.

The visual modules draw only from resolved props:

- `KeyboardModule` renders rows, keys, bottom utility icons, and the Apple-style
  pressed-key popover.
- `TextInputBarModule` renders left/right icon zones, the text field, draft
  text, and cursor.
- `ChatScreenModule` composes wallpaper, status bar, header, messages,
  text-input bar, keyboard, and navigation bar.

The React adapter is responsible only for converting renderable nodes into DOM:

- keyboard bottom and text-input icons use resolved `maskImage` when available;
- text cursors are rendered inline with text content;
- the keyboard popover is positioned as a child of the pressed key.

## What is healthy

- Keyboard and text input are visual modules, not editor widgets.
- The same renderable tree feeds preview and PNG render paths.
- Bottom keyboard icons use the same icon-token resolution model as status bar
  icons.
- Keyboard language/mode data is centralized in
  `src/domain/keyboards/standardKeyboardLayout.ts`.
- Keyboard dimensions, gaps, and key sizes are scaled by the resolver before
  reaching visual modules.
- Cursor color, width, and blink timing flow from theme tokens into both text
  input and outgoing message write-on cursor rendering.
- Editor UI uses shared field/card primitives for module behavior controls.

## Watch points

- `KeyboardBehaviorFields` and `TextInputBarBehaviorFields` are editor-specific
  structure components. They should keep using shared editor fields and should
  not introduce local visual styling.
- `KeyboardModule` and `TextInputBarModule` must remain renderer-facing modules.
  They should not import debug UI components or read persistence records.
- If text input bar config becomes reusable across more modules, extract a
  shared config editor component under `src/debug-ui/editors/module-behavior`
  or `src/debug-ui/editor-ui`, depending on whether it is domain-specific or
  purely visual.
- Accented key matching should continue to normalize pressed-key lookup for the
  keyboard while preserving the real typed character in the text input.
- Long text should grow the text input field only when wrapping requires it; it
  should not jump to final width/height early.
- Any future emoji replacement logic should live in resolver/runtime behavior,
  not inside the DOM adapter.

## Current architectural assessment

No major regression was found in the high-level separation:

- editor controls configure JSON;
- resolver converts JSON plus theme/device/icon context into render-ready props;
- visual modules emit renderable nodes;
- the adapter paints those nodes.

The main remaining risk is not the keyboard render path, but consistency of UI
composition as more module-specific editors appear. New Chat header, bubble, or
message controls should follow the encapsulation contract in
`12_editor_encapsulation_contract.md`: domain-specific structure in the editor,
visual primitives in `editor-ui`, and final drawing in visual modules.
