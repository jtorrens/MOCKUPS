# Codex Response 0016 — Module theme configs and inherited override UI

## Summary

Implemented module theme configs as the next design-token layer:

```text
global theme tokens
  → module theme config tokens
  → screen instance module token overrides
```

Chat-specific visual defaults now live in a seeded `module_theme_configs` record instead of global `themes.tokens_json`. The Chat resolver merges global theme tokens, selected theme mode, module config tokens, selected module config mode, and instance overrides before rendering.

The app shell now exposes a `Module Theme Configs` tab, and the structured JSON editor can receive inherited parent values. Applicable override rows are shown in amber and expose a `Restore inherited` action that removes the local override key and prunes empty containers.

## Files changed

- `docs/architecture/05_decisions_log.md`
  - Added accepted decisions D026–D030.
- `docs/architecture/10_module_theme_configs.md`
  - Added module theme config architecture document.
- `docs/architecture/07_initial_data_schema.md`
  - Documented `module_theme_configs`.
- `docs/architecture/08_visual_tokens_layout_contract.md`
  - Updated ownership and token resolution rules.
- `docs/architecture/09_foundational_module_contracts.md`
  - Updated module boundary and token merge order.
- `docs/examples/theme_ios_light.json`
  - Removed practical Chat-specific design defaults from the global theme.
- `src/domain/schemas/moduleThemeConfig.ts`
  - Added `ModuleThemeConfigSchema`.
- `src/domain/schemas/index.ts`
  - Exported module theme config schema/type.
- `src/domain/repository/types.ts`
  - Added module theme configs to the dataset and repository contract.
- `src/domain/repository/InMemoryRepository.ts`
  - Added module theme config lookup.
- `src/domain/repository/fixtures/exampleDataset.ts`
  - Seeded `module_theme_config_ios_light_core_chat`.
- `src/domain/resolvers/resolveChatScreen.ts`
  - Added global/module/mode token merge helpers and module theme config lookup.
- `src/persistence/sqlite/schema.sql`
  - Added `module_theme_configs` table and lookup index.
- `src/persistence/sqlite/createDatabase.ts`
  - Added additive schema-v3 migration.
- `src/persistence/sqlite/SQLiteRepository.ts`
  - Added `getModuleThemeConfig`.
- `src/persistence/sqlite/seedExampleDataset.ts`
  - Seeded module theme configs.
- `src/persistence/sqlite/validateSQLiteRepository.ts`
  - Validates table, schema version 3, and seeded Chat config.
- `src/debug-server/debugService.ts`
  - Added Module Theme Configs app tab.
  - Added inherited JSON parent calculation for module configs and screen instance overrides.
- `src/debug-server/checkDebugService.ts`
  - Validates new tab, seeded config, and inherited JSON availability.
- `src/debug-ui/api/client.ts`
  - Added `inheritedJson` to app state.
- `src/debug-ui/App.tsx`
  - Passes inherited JSON parents to the active record editor.
- `src/debug-ui/components/RecordEditor.tsx`
  - Passes inherited parent values to JSON editors.
- `src/debug-ui/components/json-editor/JsonTreeEditor.tsx`
- `src/debug-ui/components/json-editor/JsonTreeNode.tsx`
- `src/debug-ui/components/json-editor/JsonArrayEditor.tsx`
- `src/debug-ui/components/json-editor/JsonObjectEditor.tsx`
- `src/debug-ui/components/json-editor/jsonEditorUtils.ts`
  - Added inherited comparison, amber override state, and restore inherited behavior.
- `src/debug-ui/styles.css`
  - Added amber override and restore button styling.
- `PROJECT_STATUS.md`
  - Updated current state, completed work, and next recommendation.
- `docs/exchange/responses/0016_module_theme_configs_response.md`
  - Added this response file.

## Questions / conflicts

No Architecture Question was required.

The implementation follows the accepted JSON/storage boundary: global themes remain reusable visual language, module configs own module-specific defaults, screen-instance token overrides remain local exceptions, and visual modules still receive resolved props without direct persistence access.

## Tests

Passed:

```text
npm run typecheck
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm run validate:sqlite
npm run app:check
npm run app:build
npm test
npm run remotion:check
```

Smoke check passed with Chromium headless against the local app:

```text
✓ module theme configs tab exists
✓ seeded core.chat module theme config loads
✓ Chat-specific tokens moved out of global theme into module config
✓ inherited parents are provided by API
✓ screen instance override marks amber when differing
✓ Restore inherited removes the local override
✓ preview still resolves
```

## Run commands

Start the local app shell:

```bash
npm run app
```

Run app checks:

```bash
npm run app:check
npm run app:build
```

Run full validation:

```bash
npm test
npm run remotion:check
```

## Notes

- `module_theme_configs` are represented in SQLite with:
  - `id`
  - `production_id`
  - `theme_id`
  - `module_id`
  - `module_schema_version`
  - `name`
  - `tokens_json`
  - `metadata_json`
- Zod validates them through `ModuleThemeConfigSchema`.
- The repository exposes `getModuleThemeConfig(themeId, moduleId, moduleSchemaVersion)`.
- The app API exposes them as a normal editable app-shell tab.

Chat tokens moved from global theme into module theme config:

- `layout.screenGutter`
- `header`
- `messages`
- `chatBubbles`
- `avatars`
- `radii.bubble`
- `cursor`

Global `themes.tokens_json` keeps shared values such as font selection, base fonts, base colors, status bar defaults, notifications, spacing scale, radii for non-Chat/shared surfaces, and shadows.

Token resolution order:

```text
1. global theme tokens
2. selected global theme mode overrides
3. module theme config tokens
4. selected module theme config mode overrides
5. screen_instance.module_tokens_override_json
```

Inherited comparison:

- `module_theme_configs.tokens_json` receives resolved global theme tokens as its inherited parent.
- `screen_instances.module_tokens_override_json` receives resolved global + module theme config tokens as its inherited parent.
- The JSON editor compares local and inherited values with deep JSON equality.

Amber override marking:

- Any local JSON node with a matching inherited path and a differing value gets `json-override` styling.
- The row/group appears amber and shows `Restore inherited`.

Restore inherited:

- Restore removes the local override key and prunes empty object containers.
- This keeps sparse override JSON compact and lets absence mean “inherit parent/default”.

Limitations/shortcuts:

- The resolver selects the first/default module theme config for a theme/module/schema version.
- No advanced token expression/reference language was added.
- No color normalization was added; color strings compare exactly.
- Module config mode overrides are supported by merge shape but not deeply surfaced with a specialized editor.
- The override UI is generic, not a full schema-aware design editor.
- No specialized Chat editor, asset picker, font picker, Electron shell, or export pipeline was added.

Recommended next step: review module theme config and inherited override workflow visually, then choose screen-instance creation flow, font picker, asset picker, or Electron shell.
