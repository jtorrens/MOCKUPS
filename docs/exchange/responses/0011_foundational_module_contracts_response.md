# Codex Response 0011 — Foundational module contracts and debug UI boundaries

## Summary

Documented the corrected device-screen shot model and accepted foundational decisions D015–D025. Added the versioned screen-module boundary, module-owned Chat data/config schemas, logical design-space/theme-mode/media/icon/font contracts, debug UI responsibility boundaries, and an additive SQLite schema-v2 compatibility migration.

No debug UI, editor UI, Electron shell, or export pipeline was implemented.

## Files changed

- `PROJECT_STATUS.md`
- `docs/architecture/00_project_vision.md`
- `docs/architecture/01_data_model.md`
- `docs/architecture/02_render_architecture.md`
- `docs/architecture/03_visual_modules.md`
- `docs/architecture/04_shot_builder.md`
- `docs/architecture/05_decisions_log.md`
- `docs/architecture/07_initial_data_schema.md`
- `docs/architecture/08_visual_tokens_layout_contract.md`
- `docs/architecture/09_foundational_module_contracts.md`
- `docs/examples/device_iphone_generic.json`
- `docs/examples/production_minimal.json`
- `docs/examples/resolved_props_chat_screen.json`
- `docs/examples/shot_lock_to_chat.json`
- `src/domain/schemas/index.ts`
- `src/domain/schemas/module.ts`
- `src/domain/schemas/resolvedProps.ts`
- `src/domain/schemas/screen.ts`
- `src/domain/validation/validateExamples.ts`
- `src/domain/repository/fixtures/exampleDataset.ts`
- `src/domain/resolvers/resolveChatScreen.ts`
- `src/persistence/sqlite/schema.sql`
- `src/persistence/sqlite/createDatabase.ts`
- `src/persistence/sqlite/seedExampleDataset.ts`
- `src/persistence/sqlite/SQLiteRepository.ts`
- `src/persistence/sqlite/validateSQLiteRepository.ts`
- `src/visual/modules/contracts.ts`
- `docs/exchange/responses/0011_foundational_module_contracts_response.md`

## Questions / conflicts

None requiring an Architecture Question. The task explicitly permits the current normalized Chat/conversation path to remain as a compatibility layer while documenting module-owned JSON as the target, so no existing data or accepted decision was silently replaced.

## Tests

- `npm run typecheck` — passed.
- `npm run validate:examples` — passed; all six fixtures validate, including Chat module data/config.
- `npm run validate:resolver` — passed.
- `npm run validate:visual` — passed.
- `npm run validate:sqlite` — passed; all 19 tables, SQLite schema version 2, new module columns, repository equivalence, write-on output, and invalid-JSON failure validated.
- `npm test` — passed.
- `npm run remotion:check` — passed; `ChatScreenPreview` remains available at 25 fps, 1290×2796, 100 frames.
- `git diff --check` — passed.

## Notes

Schema/Zod/SQLite changes were needed. `ScreenInstanceSchema` now accepts `module_id`, `module_schema_version`, `theme_mode`, `module_data_json`, `module_config_json`, and `module_tokens_override_json`. SQLite schema version 2 adds the same columns through a non-destructive `ALTER TABLE` compatibility migration. Chat module data/config and media-window/asset-transform shapes now have Zod schemas, and the portable screen-module input/output contract is represented in TypeScript.

The current Chat implementation remains compatibility-based: resolvers still load `data_ref_json` → central `conversations`, `conversation_participants`, and `messages`, and existing visual modules still consume `ResolvedChatScreenProps`. The new Chat `module_data_json` fixture is validated but is not yet the canonical runtime source.

Already used now:

- additive SQLite/Zod screen-instance module fields;
- `theme_mode`, base/mode token merge, and `module_tokens_override_json` in Chat resolution;
- `module_config_json` as an additional compatibility source for current Chat behavior;
- device `designSpace`/`renderSize`/`scaleToPixels` and light/dark/font-selection examples.

Target direction, not yet the main runtime path:

- module registry selection by `module_id` + `module_schema_version`;
- module-owned Chat participants/messages using `senderParticipantId`;
- host-resolved asset/icon maps and final missing-asset policy;
- final renderer-assisted text measurement/grapheme reveal;
- module editors, theme font picker, and debug calibration UI.

Migration recommendation: keep legacy and module fields side by side until a dedicated Chat migration task selects the canonical source. That migration should run transactionally, convert conversation participants/messages into versioned module JSON, validate every result with the module schema, preserve an export/backup, and only then deprecate `data_ref_json`/`props_json` or normalized Chat tables.

Remaining ambiguity before the debug UI is limited to write behavior: whether early calibration edits should persist only to the module JSON columns or also mirror legacy Chat tables while the compatibility resolver remains active. The UI can still proceed if it labels each source clearly and treats `RenderableNode` as read-only calculated output.
