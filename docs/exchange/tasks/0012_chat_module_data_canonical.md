# Codex Task 0012 — Make Chat module_data_json the canonical runtime source

## Goal

Refactor the current Chat runtime path so Chat screen instances use `module_data_json` and `module_config_json` as the canonical source for chat content and behavior.

In this phase we do not need to preserve legacy compatibility.

The target direction was documented in task 0011:

```text
screen_instance
  ├─ module_id
  ├─ module_schema_version
  ├─ module_data_json
  ├─ module_config_json
  ├─ module_tokens_override_json
  └─ runtime context refs
```

The debug UI should not be born editing two sources of truth.

After this task, Chat rendering should come from module-owned JSON, not from the legacy `data_ref_json → conversations/messages` path.

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
docs/exchange/responses/0010_sqlite_persistence_response.md
docs/exchange/responses/0011_foundational_module_contracts_response.md
```

Review current implementation:

```text
src/domain/schemas/
src/domain/repository/
src/domain/resolvers/
src/persistence/sqlite/
src/visual/
src/remotion/
```

## Accepted decision for this task

In this phase, do not maintain old Chat compatibility as an active runtime path.

The canonical runtime source for Chat should be:

```text
screen_instances.module_data_json
screen_instances.module_config_json
screen_instances.module_tokens_override_json
```

not:

```text
screen_instances.data_ref_json → conversations → messages
```

Legacy tables may remain in SQLite for now if removing them is risky or unnecessary, but Chat runtime should not depend on them.

## Important architecture constraints

Preserve these decisions:

- Production is the root entity.
- Shot is the central render unit.
- Shot means device-screen action sequence, not external video placement.
- A shot contains one or more screen instances.
- Chat is one screen module, not the root architecture.
- SQL stores stable relationships.
- JSON stores flexible visual/configuration data.
- Screen modules own their internal data schema.
- Screen instances store module-specific data/config JSON.
- The central app validates module JSON through the module schema but does not interpret module internals beyond routing/validation.
- Modules own their visual behavior and animation interpretation.
- Visual modules must remain pure/deterministic.
- Visual modules must not access DB/repositories directly.
- Themes support light/dark mode and logical design units.
- RenderableNode is calculated output and should not be edited directly.

## Scope

Implement only:

1. Refactor the Chat resolver path to read Chat data from `screen_instance.module_data_json`.
2. Refactor Chat behavior/config to read from `screen_instance.module_config_json`.
3. Keep token overrides from `screen_instance.module_tokens_override_json`.
4. Update fixture/seed data so the example Chat screen instance has complete `module_data_json` and `module_config_json`.
5. Ensure Chat participants and messages live inside Chat module data.
6. Ensure each message references `senderParticipantId`.
7. Ensure group chat support is structurally represented even if the example remains simple.
8. Update Zod schemas if needed.
9. Update SQLite seed and validation.
10. Update Remotion POC path if needed so it still renders from the canonical Chat module data.
11. Update docs/status.
12. Create the Codex response file for this task.

## Do not implement

Do not create or implement:

- debug UI
- editor UI
- Electron shell
- final render/export pipeline
- custom Instagram module
- full asset manager UI
- full font picker UI
- full module editor UI
- destructive removal of tables unless clearly safe
- complex migration tooling

This task is the Chat source-of-truth refactor only.

## Chat module_data_json requirements

The Chat screen instance should store versioned module data.

Required shape conceptually:

```json
{
  "schemaVersion": 1,
  "participants": [
    {
      "id": "p_owner",
      "role": "owner",
      "actorId": "actor_ana"
    },
    {
      "id": "p_other",
      "role": "participant",
      "actorId": "actor_luis"
    }
  ],
  "header": {
    "title": "Luis",
    "subtitle": "online",
    "avatarParticipantId": "p_other"
  },
  "messages": [
    {
      "id": "msg_001",
      "senderParticipantId": "p_other",
      "type": "text",
      "text": "¿Dónde estás?",
      "startFrame": 20,
      "enterDurationFrames": 8,
      "textReveal": {
        "mode": "simple_write_on",
        "startFrame": 28,
        "durationFrames": 24
      }
    }
  ]
}
```

The exact field names should follow current schemas if already defined, but the contract must include:

```text
participants
messages
senderParticipantId
header
message timings
optional message media references
```

## Chat module_config_json requirements

Move instance behavior/config out of generic legacy props where practical.

Required behavior/config examples:

```json
{
  "showHeader": true,
  "showKeyboard": false,
  "initialScroll": "bottom",
  "messageGrouping": "bySender",
  "debugShowBounds": false
}
```

The current implementation may still map some older `props_json` fields into config temporarily, but canonical examples/seed should use `module_config_json`.

## Sender/direction resolution

Resolve message direction from participants:

```text
message.senderParticipantId == owner participant → sent
message.senderParticipantId != owner participant → received
message.type == system → system
```

Participants may reference production actors through `actorId`.

This is required for future group chats.

## Asset references

Chat module data may reference media assets by ID.

For this task, keep asset resolution minimal but structurally correct.

If the current example does not use message media, include at least schema/example support for:

```text
message.mediaAssetId
message.media.window
message.media.transform
```

Do not implement a full asset manager.

## Legacy normalized Chat data

Legacy central Chat tables may remain in SQLite if removing them would require a larger migration:

```text
conversations
conversation_participants
messages
```

But after this task:

- the canonical example Chat screen should not require `data_ref_json` to resolve.
- `resolveChatScreen` should prefer/require `module_data_json`.
- tests should prove the module-data path is used.
- any remaining legacy path should be clearly marked as deprecated or removed from runtime.

If keeping a legacy fallback is unavoidable, make it opt-in and document it clearly. Do not let debug UI depend on it.

## SQLite requirements

Update the seed so the example Chat screen instance stores:

```text
module_id = core.chat
module_schema_version = 1
module_data_json = complete Chat module data
module_config_json = complete Chat module config
module_tokens_override_json = optional object, can be empty
```

Validate that SQLiteRepository returns these fields.

If schema already has the needed columns from task 0011, do not add more columns.

If a migration is needed, keep it additive and non-destructive.

## Resolver requirements

Update Chat resolver flow:

```text
screen_instance.module_data_json
  ↓
ChatModuleDataSchema validation
  ↓
participants/header/messages resolution
  ↓
ResolvedChatScreenProps
```

The resolver may still resolve common context:

```text
owner actor
device
device state
theme/mode
assets/icons
```

but Chat-specific content should come from module JSON.

## Validation requirements

All existing validation should still pass:

```text
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm run validate:sqlite
npm test
```

If Remotion scripts exist, keep them passing:

```text
npm run remotion:check
```

Add or update tests to assert:

- Chat screen instance has `module_data_json`.
- Chat resolver uses module data.
- Chat messages resolve from `module_data_json`.
- senderParticipantId determines sent/received direction.
- legacy conversation/message records are not required for the canonical example path.

## Documentation update

Update docs as needed, especially:

```text
docs/architecture/09_foundational_module_contracts.md
docs/architecture/07_initial_data_schema.md
docs/architecture/02_render_architecture.md
PROJECT_STATUS.md
```

Document clearly:

```text
Chat module_data_json is now the canonical runtime source.
Legacy central Chat tables are deprecated compatibility structures if still present.
Debug UI should edit module_data_json/module_config_json/module_tokens_override_json, not conversations/messages.
```

Set next recommended task to:

```text
Create a minimal debug calibration UI for selecting a shot/frame, viewing the Remotion preview, and inspecting/editing module data, module config, theme tokens, device metrics/state and renderable output.
```

## Update exchange response

Create this response file:

```text
docs/exchange/responses/0012_chat_module_data_canonical_response.md
```

Use this format:

```md
# Codex Response 0012 — Chat module_data_json canonical runtime source

## Summary

## Files changed

## Questions / conflicts

## Tests

## Notes
```

## Notes requirements

In `## Notes`, include:

- whether legacy conversations/messages tables remain physically present.
- whether the resolver has any fallback path left.
- how senderParticipantId is resolved.
- what was changed in SQLite seed.
- what the debug UI should edit next.
- any migration recommendation.

## Architecture Question rule

If you find a conflict between:

- architecture docs
- current schemas
- current SQLite schema
- current repository interface
- current resolver implementation
- current visual modules/layout
- Remotion integration
- accepted decisions already in the log

do not silently invent a new architecture.

Instead, stop and create an Architecture Question in the response file.

## Acceptance criteria

- Chat module_data_json is the canonical runtime source.
- Chat module_config_json is the canonical instance behavior/config source.
- Chat module_tokens_override_json remains the canonical per-instance visual override source.
- Chat participants and messages live in module_data_json.
- Messages reference senderParticipantId.
- Resolved Chat props are generated from module_data_json.
- SQLite seed stores complete Chat module JSON.
- Existing validation commands pass.
- Remotion POC still works.
- PROJECT_STATUS.md is updated.
- Response file exists in docs/exchange/responses/.
- No debug UI/editor UI/Electron/export pipeline is added.
