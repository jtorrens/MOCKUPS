# Codex Response 0012 — Chat module_data_json canonical runtime source

## Summary

Refactored `core.chat` schema version 1 so `screen_instances.module_data_json`, `module_config_json`, and `module_tokens_override_json` are its only runtime sources. Chat participants, header, messages, timings, sender references, optional media framing, behavior, and local token exceptions now resolve without `data_ref_json`, conversations, conversation participants, messages, or generic props.

The canonical in-memory and SQLite fixtures contain no normalized Chat records. Remotion continues through the same resolver and therefore now renders from module-owned JSON.

## Files changed

- `PROJECT_STATUS.md`
- `docs/architecture/01_data_model.md`
- `docs/architecture/02_render_architecture.md`
- `docs/architecture/04_shot_builder.md`
- `docs/architecture/05_decisions_log.md`
- `docs/architecture/07_initial_data_schema.md`
- `docs/architecture/08_visual_tokens_layout_contract.md`
- `docs/architecture/09_foundational_module_contracts.md`
- `docs/examples/shot_lock_to_chat.json`
- `docs/examples/resolved_props_chat_screen.json`
- `src/domain/schemas/module.ts`
- `src/domain/schemas/resolvedProps.ts`
- `src/domain/schemas/screen.ts`
- `src/domain/repository/fixtures/exampleDataset.ts`
- `src/domain/resolvers/resolveChatScreen.ts`
- `src/domain/resolvers/resolveMessageBubble.ts`
- `src/domain/resolvers/resolveScreenInstance.ts`
- `src/domain/validation/validateExamples.ts`
- `src/domain/validation/validateResolver.ts`
- `src/persistence/sqlite/validateSQLiteRepository.ts`
- `docs/exchange/responses/0012_chat_module_data_canonical_response.md`

## Questions / conflicts

None. Task 0012 explicitly resolves the temporary compatibility ambiguity documented in task 0011: module-owned Chat JSON is canonical and no dual-write/runtime fallback is maintained.

## Tests

- `npm run typecheck` — passed.
- `npm run validate:examples` — passed.
- `npm run validate:resolver` — passed; Chat resolves with no legacy records and `senderParticipantId` determines direction.
- `npm run validate:visual` — passed.
- `npm run validate:sqlite` — passed; canonical module fields round-trip, legacy Chat tables have zero fixture rows, invalid JSON fails loudly, and SQLite/in-memory outputs are equivalent.
- `npm test` — passed.
- `npm run remotion:check` — passed; `ChatScreenPreview` remains available at 25 fps, 1290×2796, 100 frames.
- `git diff --check` — passed.

## Notes

The `conversations`, `conversation_participants`, and `messages` tables remain physically present in SQLite. Removing them would be unnecessary and potentially destructive in this phase. The canonical seed deliberately inserts no rows into them.

The Chat resolver has no fallback path. It requires `module_id = core.chat`, `module_schema_version = 1`, matching `module_data_json.schemaVersion`, valid `module_data_json`, valid `module_config_json`, and `module_tokens_override_json`. `data_ref_json` must be null for Chat. Generic `props_json` and screen-template default props are not merged into Chat behavior.

`senderParticipantId` is looked up in module-owned participants. The single participant with role `owner` produces `outgoing`; any other participant produces `incoming`; a message with type `system` produces `system`. Participants may reference production actors or use a module-local display name, so group chats are structurally supported.

The SQLite seed now stores the complete `core.chat` module document and config on `screen_instance_chat`, including schema version, three participants, header/avatar participant reference, message timings, simple write-on data, and an example `mediaAssetId` with logical media window and transform. It stores empty token overrides and null legacy Chat `data_ref_json`.

The next debug UI should edit only:

- `screen_instances.module_data_json`
- `screen_instances.module_config_json`
- `screen_instances.module_tokens_override_json`
- reusable theme tokens
- device metrics/state

It should inspect `RenderableNode` as read-only calculated output and must not edit or mirror normalized conversations/messages.

Migration recommendation: before physically deleting legacy tables/columns, provide a one-way transactional migration for existing databases that converts each legacy Chat instance into `core.chat` schema version 1 JSON, validates all actor/media references and module schemas, preserves an export/backup, and reports rows that cannot be converted. No complex migration tooling or destructive cleanup was added here.
