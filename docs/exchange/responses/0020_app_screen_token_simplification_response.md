# Codex Response 0020 — App/screen token simplification and inspector-first UI pass

## Summary

Implemented the design-stage simplification around the active model:

```text
Theme → App → Screen/Module
```

The local app shell was also pushed toward an inspector-first, Figma-collections-like UI with accordion cards, friendlier token groups, centralized color editing, and structured module-content editing for Chat participants/messages.

Follow-up clarification implemented in this checkpoint: visual overrides at App Instance / Module Instance / Screen Instance level are removed from the active model. Explicit `module_instances` now own per-shot module content and behavior.

This remains a design-stage breaking phase. Compatibility with older local SQLite data was not preserved as a priority; explicit `db:reset` remains the safe path when the local fixture state needs to be regenerated.

## Architecture outcome

- Removed `Screen Template` from the active runtime/editor direction.
- Did not introduce `Screen Preset` or `App Theme Config`.
- App-level reusable defaults now live on existing App records.
- Module/screen reusable defaults remain in module theme configs.
- Added explicit `module_instances` as the persistence/runtime layer for module payloads.
- `module_instances.content_json` stores shot-specific module content.
- `module_instances.behavior_json` stores per-shot module behavior.
- Per-instance visual token overrides are no longer active.
- Mode-aware colors can exist at Theme, App, and Module levels and are collapsed only at render/preview resolution.
- Numeric visual tokens are authored in logical design units and scaled through selected device metrics for preview/render.
- Added a field-descriptor layer that maps compact storage paths to canonical UI paths without duplicating metadata in stored JSON.

## UI outcome

- Left workspace now uses accordion sections for:
  - Project;
  - Apps;
  - Production data.
- Each section contains its tree/list rather than mixing top-level tabs with tree navigation.
- Main editors use accordion cards for high-level areas.
- Nested token/design groups use compact accordion cards with logical icons.
- Mode-aware color roles are centralized into `Colors` sections where possible.
- Removed redundant table headers such as `Property / Override` where the card context is enough.
- Updated visual styling toward a lighter inspector surface with softer borders and less heavy dark UI.
- Preview remains right-aligned and focused on visual feedback.

## Module Content editor

The module-instance editor now presents `content_json` as `Module Content`.

This is an important conceptual distinction:

```text
module_instances.content_json
  shot-specific content for the module attached to one screen instance

module_instances.behavior_json
  per-shot behavior for that same module instance
```

For `core.chat@1`:

- `participants` are edited as structured content cards.
- `messages` are edited as structured content cards.
- Collapsed participant rows summarize display name, role, and actor when available.
- Collapsed message rows summarize sender, message kind, text/media summary, and frame timing.
- Message/participant fields use friendly labels and typed widgets from module editor hints.
- Array-like content supports add, duplicate, delete, and move controls.
- Raw JSON remains a fallback/recovery surface, not the normal authoring UI.

## Important fixes made during the pass

- Fixed grouped JSON editors so root arrays such as `messages` and `participants` can render structurally.
- Unwrapped double-serialized JSON strings where safe before structured rendering.
- Applied group-context hints so paths such as `messages.[].text` still resolve when editing only the `messages` array.
- Replaced native `<details>` for content rows with React-controlled accordions and stable keys.
- Restored useful collapsed row summaries after the accordion pass.
- Restyled `Module Content` inputs/selects/textareas so they do not look disabled.
- Kept internal token paths visible where they identify token roles, while visible group/field labels stay friendly.

## Files changed

Main implementation:

- `src/debug-ui/components/AppPreviewPanel.tsx`
- `src/debug-ui/components/ProjectTree.tsx`
- `src/debug-ui/components/RecordEditor.tsx`
- `src/debug-ui/components/json-editor/JsonTreeEditor.tsx`
- `src/debug-ui/components/json-editor/JsonTreeNode.tsx`
- `src/debug-ui/components/json-editor/JsonValueEditor.tsx`
- `src/debug-ui/components/json-editor/ModeColorEditor.tsx`
- `src/debug-ui/components/json-editor/TokenOverrideEditor.tsx`
- `src/debug-ui/components/json-editor/jsonEditorUtils.ts`
- `src/debug-ui/components/json-editor/uiHints.ts`
- `src/debug-ui/styles.css`
- `src/domain/schemas/screen.ts`
- `src/domain/repository/types.ts`
- `src/domain/repository/InMemoryRepository.ts`
- `src/domain/repository/fixtures/exampleDataset.ts`
- `src/domain/resolvers/resolveChatScreen.ts`
- `src/domain/resolvers/resolveScreenInstance.ts`
- `src/persistence/sqlite/schema.sql`
- `src/persistence/sqlite/createDatabase.ts`
- `src/persistence/sqlite/SQLiteRepository.ts`
- `src/persistence/sqlite/seedExampleDataset.ts`
- `src/persistence/sqlite/validateSQLiteRepository.ts`
- `src/debug-server/debugService.ts`
- `src/debug-server/checkDebugService.ts`
- `src/debug-server/checkPersistence.ts`
- `src/debug-ui/field-descriptors/*`

Documentation:

- `docs/exchange/tasks/0020_app_screen_token_simplification.md`
- `docs/exchange/responses/0020_app_screen_token_simplification_response.md`
- `docs/architecture/05_decisions_log.md`
- `docs/architecture/01_data_model.md`
- `docs/architecture/02_render_architecture.md`
- `docs/architecture/04_shot_builder.md`
- `docs/architecture/07_initial_data_schema.md`
- `docs/architecture/08_visual_tokens_layout_contract.md`
- `docs/architecture/09_foundational_module_contracts.md`
- `docs/architecture/10_module_theme_configs.md`
- `PROJECT_STATUS.md`

## Validation run

The following checks passed during the final pass:

```text
npm run typecheck
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm run validate:sqlite
npm run app:check
npm run app:persistence-check
npm run debug:build
npm run debug:check
```

## Follow-ups

- Build the unified inspector renderer on top of field descriptors so sections, groups, labels, widgets, restore buttons, and row layout come from one grammar.
- Add first-class create/duplicate/delete workflows for module instances where needed.
- Continue refining the left tree actions for add/duplicate/delete workflows.
- Add stronger browser/UI smoke tests for editing Chat participants/messages through the new content-card editor.
- Run the broader project validation suite before a release-style checkpoint.
