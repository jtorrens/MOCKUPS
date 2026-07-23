# Codex Task 0015 — Schema-driven JSON tree/value editor

## Goal

Replace the raw JSON textarea workflow with a generic schema-driven JSON tree/value editor.

This editor should become the foundation for most MOCKUPS input interfaces:

```text
JSON data + schema/UI hints
  ↓
structured tree/form editor
  ↓
validated autosave
  ↓
preview refresh
```

Raw JSON editing must remain available as an advanced fallback, but normal editing should be possible without manually editing braces, commas or quotes.

## Current context

Read these files first:

```text
docs/architecture/00_project_vision.md
docs/architecture/01_data_model.md
docs/architecture/02_render_architecture.md
docs/architecture/03_visual_modules.md
docs/architecture/04_shot_builder.md
docs/architecture/05_decisions_log.md
docs/architecture/07_initial_data_schema.md
docs/architecture/08_visual_tokens_layout_contract.md
docs/architecture/09_foundational_module_contracts.md
PROJECT_STATUS.md
```

Review recent task responses:

```text
docs/exchange/responses/0013_debug_calibration_ui_response.md
docs/exchange/responses/0014_core_app_shell_response.md
```

Review current UI/server implementation:

```text
src/debug-ui/
src/debug-server/
src/domain/schemas/
src/domain/repository/
src/domain/resolvers/
src/persistence/sqlite/
```

## Current phase decision

The app shell exists with tabs for core entities and a persistent preview panel.

JSON fields are currently edited as raw text. This is useful for debugging, but not user-friendly enough for the app workflow.

We now want a reusable structured JSON editor that can later power:

```text
Theme editor
Device editor
Device state editor
Chat module data editor
Chat module config editor
Screen instance override editor
Media metadata editor
Render preset editor
```

## Important architecture constraints

Preserve these decisions:

- JSON fields remain canonical storage for flexible module/config/token data.
- `module_data_json`, `module_config_json`, and `module_tokens_override_json` remain canonical for screen module data/config/overrides.
- `theme.tokens_json`, `device.metrics_json`, and `device_state.state_json` remain editable source data.
- RenderableNode remains calculated output and read-only.
- Raw JSON remains available as advanced fallback.
- UI hints describe editing behavior; they should not change the stored data shape.
- Specialized editors may later sit on top of the generic JSON editor, but should write the same canonical JSON fields.
- Invalid JSON or schema-invalid data must never be persisted.

## Scope

Implement only:

1. A reusable `JsonTreeEditor` or equivalent component.
2. Tree/object/array navigation with collapsible sections.
3. Key/value editing for existing values.
4. Basic widgets inferred from value type:
   - string input
   - number input
   - boolean checkbox
   - null display/edit if practical
   - object group
   - array group
5. Array controls:
   - add item
   - duplicate item
   - delete item
   - reorder item if practical
6. Object controls:
   - add key/value pair
   - delete key
   - rename key if practical
7. Raw JSON fallback mode.
8. Autosave integration with existing validated autosave.
9. UI hint infrastructure for future widgets.
10. Basic widget support for:
    - color picker
    - select/dropdown
11. Apply the editor to all current JSON fields in the app shell.
12. Update `PROJECT_STATUS.md`.
13. Create the Codex response file for this task.

## Do not implement

Do not create or implement yet:

- full asset picker
- full font picker
- visual timeline editor
- specialized Chat conversation editor
- advanced schema form generation
- drag-and-drop rich UI beyond simple array reorder if easy
- Electron shell
- final render/export pipeline
- undo/redo history
- multi-user workflows
- complex JSON Patch system
- database schema redesign

This task is the generic structured JSON editor only.

## Suggested file structure

Use a structure similar to this unless the current UI structure suggests a better fit:

```text
src/debug-ui/components/json-editor/
  JsonTreeEditor.tsx
  JsonTreeNode.tsx
  JsonValueEditor.tsx
  JsonArrayEditor.tsx
  JsonObjectEditor.tsx
  RawJsonEditor.tsx
  uiHints.ts
  jsonEditorUtils.ts
```

If a simpler structure is preferable, explain it in the response.

## Data model

The editor should operate on generic JSON-compatible data:

```ts
type JsonValue =
  | string
  | number
  | boolean
  | null
  | JsonValue[]
  | { [key: string]: JsonValue };
```

If the existing code already has a JSON type, reuse it.

## UI behavior

### Tree mode

Tree mode should show:

```text
object keys
array indices
nested expandable groups
editable primitive values
clear labels/paths
```

Example visual intent:

```text
▾ chat
  screenGutter        [18]
  messageSpacing      [8]

▾ messages
  ▾ [0] msg_001
      id                  msg_001
      senderParticipantId p_luis
      text                ¿Dónde estás?
      startFrame          20
  ▾ [1] msg_002
      id                  msg_002
      senderParticipantId p_owner
      text                Llegando.
      startFrame          80
```

### Raw mode

Raw mode should:

- show full JSON text.
- allow paste/editing large blocks.
- validate before save.
- share the same save pipeline as tree mode.

### Switching modes

Switching between tree and raw modes should not lose unsaved changes.

If raw text is invalid, show an error and prevent switching back to tree until fixed or reverted.

## Autosave integration

The editor should integrate with the existing autosave state model:

```text
Saved
Unsaved changes
Invalid JSON
Saving…
Save failed
```

Tree edits should produce valid JSON values by construction whenever possible.

Raw mode can produce invalid JSON; invalid raw JSON must not persist.

When editing values in tree mode:

```text
edit value
  ↓
update JSON object in component state
  ↓
validate with existing field/schema validation
  ↓
debounced save
  ↓
refresh preview/output
```

## UI hints infrastructure

Add a small UI hint system that can map JSON paths to editor widgets.

Do not overbuild. A simple object map is enough.

Example concept:

```ts
type JsonUiHint = {
  widget?: 'text' | 'number' | 'checkbox' | 'color' | 'select' | 'textarea';
  label?: string;
  options?: string[];
  min?: number;
  max?: number;
  step?: number;
};

type JsonUiHints = Record<string, JsonUiHint>;
```

Paths may use dot notation or another simple convention:

```text
modes.light.colors.bubbleSent
chat.initialScroll
messages[].type
```

Use whichever convention is easy and document it in code/comments.

## Required widgets

### Inferred widgets

Without hints:

- string → text input
- number → number input
- boolean → checkbox
- object → collapsible group
- array → collapsible list
- null → display/edit as null

### Color widget

If a hint says `widget: 'color'`, show an HTML color input and text value.

It should work for hex values like:

```text
#007AFF
#FFFFFF
```

### Select/dropdown widget

If a hint says `widget: 'select'`, show a dropdown with provided options.

Use this for obvious enum-like fields if easy:

```text
theme defaultMode: light/dark
themeMode: light/dark
initialScroll: top/bottom/keep_latest_visible
message type: text/image/video/system
```

Do not spend too much time wiring every possible enum. The infrastructure is more important.

## Apply to current JSON fields

Replace or augment raw textareas for:

```text
screen_instances.module_data_json
screen_instances.module_config_json
screen_instances.module_tokens_override_json
screen_instances.transform_json
themes.tokens_json
devices.metrics_json
device_states.state_json
actors.metadata_json
media_assets.metadata_json
render_presets settings/quality JSON if present
```

If some fields remain raw temporarily, document why.

## Add/delete/duplicate behavior

For arrays:

- add item using a simple default value.
- duplicate selected item.
- delete item.
- reorder item if practical.

For objects:

- add key.
- delete key.
- rename key if practical.

Safety:

- confirm delete for object keys/array items if simple.
- prevent duplicate keys.
- show readable errors for invalid key names if relevant.

Default values can be simple:

```text
new string: ""
new number: 0
new boolean: false
new object: {}
new array item: {}
```

A more schema-aware default system can come later.

## Validation

The final saved value must still go through the existing backend validation.

Client-side validation should catch obvious JSON/value issues, but server-side validation remains authoritative.

All existing validation must still pass:

```text
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm run validate:sqlite
npm run app:check
npm run app:build
npm test
```

If `remotion:check` exists, keep it passing:

```text
npm run remotion:check
```

## Smoke checks

Update or add app smoke checks if practical.

Check at least:

- structured editor renders for a JSON field.
- editing a primitive value saves and refreshes preview/output.
- invalid raw JSON is rejected.
- array add/duplicate/delete works on a safe test field or local editor state.
- color/select hints render where configured.

Do not add brittle visual screenshot assertions.

## Documentation update

Update `PROJECT_STATUS.md` to reflect:

- core app shell exists.
- structured JSON tree/value editor exists.
- raw JSON fallback remains available.
- JSON autosave remains validated.
- current editors are still generic, not final specialized module editors.
- no asset picker/font picker/Electron/export pipeline exists yet.

Set next recommended task to one of:

```text
Review structured JSON editing workflow visually, then choose: schema-aware field controls, font picker, asset picker, screen-instance creation flow, or Electron shell.
```

## Update exchange response

Create this response file:

```text
docs/exchange/responses/0015_json_tree_editor_response.md
```

Use this format:

```md
# Codex Response 0015 — Schema-driven JSON tree/value editor

## Summary

## Files changed

## Questions / conflicts

## Tests

## Run commands

## Notes
```

## Notes requirements

In `## Notes`, include:

- how the tree editor is structured.
- which widgets are supported.
- how UI hints work.
- which JSON fields use the new editor.
- what remains raw JSON only, if anything.
- how autosave/validation works.
- limitations/shortcuts.
- recommended next UI improvement.

## Architecture Question rule

If you find a conflict between:

- architecture docs
- current schemas
- current SQLite schema
- current repository implementation
- current resolver implementation
- current app shell
- accepted decisions already in the log

do not silently invent a new architecture.

Instead, stop and create an Architecture Question in the response file.

## Acceptance criteria

- Generic structured JSON editor exists.
- Tree/object/array navigation exists.
- Primitive key/value editing exists.
- Raw JSON fallback exists.
- Autosave remains validated.
- Invalid raw JSON is not persisted.
- JSON editor is used for current JSON fields in the app shell.
- At least color and select UI hints are supported.
- Array add/duplicate/delete exists.
- Object add/delete key exists.
- Existing app preview still works.
- Existing validation commands pass.
- npm test passes.
- PROJECT_STATUS.md is updated.
- Response file exists in docs/exchange/responses/.
- No asset picker, font picker, Electron shell, final export pipeline, or specialized Chat editor is added.
