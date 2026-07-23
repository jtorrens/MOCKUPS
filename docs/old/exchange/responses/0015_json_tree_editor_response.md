# Codex Response 0015 — Schema-driven JSON tree/value editor

## Summary

Implemented a reusable structured JSON tree/value editor and applied it to the app shell's JSON fields.

The editor keeps canonical JSON storage unchanged while making normal editing possible through collapsible object/array groups, primitive controls, add/delete/duplicate/reorder actions, and a raw JSON fallback for advanced edits. It still feeds the existing debounced autosave path, so backend validation remains authoritative and invalid raw JSON is not persisted.

## Files changed

- `src/debug-ui/components/RecordEditor.tsx`
  - Replaced JSON textareas with the new `JsonTreeEditor`.
  - Kept scalar-field autosave behavior unchanged.
- `src/debug-ui/components/json-editor/JsonTreeEditor.tsx`
  - Added the reusable tree/raw JSON editor shell.
- `src/debug-ui/components/json-editor/JsonTreeNode.tsx`
  - Added collapsible object/array node rendering and primitive rows.
- `src/debug-ui/components/json-editor/JsonValueEditor.tsx`
  - Added primitive widgets for string, number, boolean, null, textarea, color, and select values.
- `src/debug-ui/components/json-editor/JsonObjectEditor.tsx`
  - Added object key listing, add key, rename key, and delete key controls.
- `src/debug-ui/components/json-editor/JsonArrayEditor.tsx`
  - Added array item listing, add item, duplicate, delete, and up/down reorder controls.
- `src/debug-ui/components/json-editor/RawJsonEditor.tsx`
  - Added raw JSON fallback editor.
- `src/debug-ui/components/json-editor/uiHints.ts`
  - Added simple path-based UI hint infrastructure.
- `src/debug-ui/components/json-editor/jsonEditorUtils.ts`
  - Added JSON-compatible value types plus path/set/delete/rename/parse/stringify helpers.
- `src/debug-ui/styles.css`
  - Added structured JSON editor, tree node, primitive widget, raw editor, and action control styles.
- `PROJECT_STATUS.md`
  - Updated current state, completed work, and next recommendation.
- `docs/exchange/responses/0015_json_tree_editor_response.md`
  - Added this response file.

## Questions / conflicts

No architecture conflicts found.

The task fits the accepted SQL/JSON boundary: JSON fields remain canonical storage; the editor is only a UI layer over the same stored shapes. RenderableNode remains calculated/read-only, and no specialized Chat editor, asset picker, font picker, Electron shell, or export pipeline was added.

## Tests

Passed:

```text
npm run typecheck
npm run app:check
npm run app:build
npm test
npm run remotion:check
git diff --check
```

Smoke check passed with Chromium headless against the local app:

```text
✓ structured JSON editor renders for module_config_json
✓ tree primitive edit autosaves and refreshes preview output
✓ invalid raw JSON is rejected
✓ color and select hints render for theme tokens/config
✓ array add/duplicate/delete works on actor metadata_json
✓ smoke edits restored original JSON values
```

## Run commands

Start the local app shell:

```bash
npm run app
```

Run app service checks:

```bash
npm run app:check
```

Build the app shell:

```bash
npm run app:build
```

Run the full current validation suite:

```bash
npm test
npm run remotion:check
```

## Notes

- The tree editor is structured as a small reusable component family under `src/debug-ui/components/json-editor/`.
- `JsonTreeEditor` owns tree/raw mode switching and parses the current raw draft.
- `JsonTreeNode` renders collapsible object/array groups and primitive rows.
- `JsonObjectEditor` handles object add/rename/delete key actions.
- `JsonArrayEditor` handles array add/duplicate/delete/reorder actions.
- `JsonValueEditor` renders primitive value widgets.
- `RawJsonEditor` preserves the advanced raw JSON editing path.

Supported widgets:

- inferred string text input;
- inferred number input;
- inferred boolean checkbox;
- null display with conversion selector;
- collapsible object group;
- collapsible array group;
- raw JSON textarea;
- color picker plus text value for hinted/hex color strings;
- select/dropdown for hinted enum-like paths;
- textarea for hinted long strings.

UI hints:

- Hints are simple path maps in `uiHints.ts`.
- Paths use dot notation with `[]` as the array wildcard, for example `messages.[].type`.
- Exact hints are preferred; a small color fallback detects hex color strings on color-like keys.
- Current hints cover obvious fields such as theme modes/colors, Chat `initialScroll`, `messageGrouping`, message `type`, participant `role`, device-state booleans/orientation, and transform numeric values.

JSON fields using the new editor:

- All current app-shell fields with `kind: "json"` now use the editor automatically.
- This includes screen instance module data/config/token overrides/transform fields, theme tokens, device metrics, device-state state, actor metadata, media metadata/dimensions, render preset JSON fields, app config/metadata, animation preset parameters, screen template defaults/config, production settings/metadata, and shot canvas/metadata.

Raw JSON only:

- No app-shell JSON field is raw-only now.
- Raw JSON remains available as an advanced fallback for every JSON field.

Autosave/validation:

- Tree edits produce JSON-compatible values by construction and update the same draft used by existing autosave.
- Raw edits can be invalid; invalid raw JSON marks the field invalid, disables tree switching, and is not sent to persistence.
- Final persistence still goes through the existing debounced `updateAppRecord` path and backend Zod/schema validation.
- Server-side validation remains authoritative.

Limitations/shortcuts:

- Hints are hand-written and intentionally lightweight; this is not full schema form generation yet.
- Defaults for new keys/items are generic rather than schema-aware.
- Delete confirmation uses native `window.confirm`.
- Rename uses native `window.prompt`.
- Array reorder uses Up/Down buttons rather than drag-and-drop.
- There is no undo/redo history.
- There is no specialized Chat conversation editor yet.
- There is no asset picker, font picker, Electron shell, or final export pipeline.

Recommended next UI improvement: review structured JSON editing workflow visually, then choose schema-aware field controls, a font picker, an asset picker, screen-instance creation flow, or Electron shell.
