# Codex Task 0010 — Minimal SQLite persistence and repository adapter

## Goal

Implement minimal SQLite persistence for the MOCKUPS domain model and a SQLite-backed repository adapter that can feed the existing resolver pipeline.

This task should move the project from fixtures-only data toward real app persistence, without changing the architecture.

The target path is:

```text
SQLite tables
  ↓
SQLiteRepository
  ↓
existing resolvers
  ↓
resolved props
  ↓
visual modules/layout
  ↓
Remotion adapter / preview
```

The existing in-memory repository should remain useful for tests and development fixtures.

Do not implement a full editor UI yet. That is the next task.

## Current context

Read these files first:

```text
docs/architecture/00_project_vision.md
docs/architecture/01_data_model.md
docs/architecture/02_render_architecture.md
docs/architecture/03_visual_modules.md
docs/architecture/04_shot_builder.md
docs/architecture/05_decisions_log.md
docs/architecture/06_codex_workflow.md
docs/architecture/07_initial_data_schema.md
docs/architecture/08_visual_tokens_layout_contract.md
PROJECT_STATUS.md
```

Review recent task responses:

```text
docs/exchange/responses/0007_visual_tokens_layout_contract_response.md
docs/exchange/responses/0008_chat_layout_pass_response.md
docs/exchange/responses/0009A_remotion_poc_response.md
```

Review current implementation:

```text
src/domain/schemas/
src/domain/repository/
src/domain/resolvers/
src/visual/
src/remotion/
```

## Important architecture constraints

Preserve all accepted decisions:

- Production is the root entity.
- Shot is the central render unit.
- A shot contains one or more screen instances.
- Chat is only one screen type.
- SQL stores stable relationships.
- JSON stores flexible visual/configuration data.
- Visual modules do not access the database directly.
- Resolvers create resolved props.
- ShotBuilder composes screens but does not draw them.
- Modules receive props + frame and return renderables.
- Renderer should be frame-based and deterministic.
- Visual style values live in theme tokens unless instance-specific.
- Device geometry lives in device metrics.
- Device live state lives in device state JSON.
- Visual modules receive render-ready token values through resolved props.
- Renderable metadata is diagnostic, not canonical configuration.

## Storage model

Implement SQLite with stable relational columns and flexible JSON stored as TEXT columns.

Examples:

```text
themes.tokens_json
devices.metrics_json
device_states.state_json
screen_instances.data_ref_json
screen_instances.transform_json
screen_instances.props_json
screen_instances.transition_in_json
screen_instances.transition_out_json
screen_events.payload_json
messages.style_override_json
messages.animation_override_json
messages.layout_override_json
messages.metadata_json
data_sources.data_json
```

Do not store large media files directly in SQLite. Store paths/URIs in `media_assets`.

## Scope

Implement only:

1. SQLite dependency and minimal database module.
2. Initial schema/migration SQL for the documented entities.
3. A seed process that inserts the current example dataset into SQLite.
4. A `SQLiteRepository` that implements the same repository contract used by the current resolvers.
5. Validation that resolving from SQLite produces equivalent or compatible output to the in-memory repository for the current example.
6. Scripts to initialize/seed/validate SQLite.
7. Update `PROJECT_STATUS.md`.
8. Create the Codex response file for this task.

## Do not implement

Do not create or implement:

- debug UI
- editor UI
- Electron shell
- asset management UI
- final import/export workflow
- final render/export pipeline
- complex migrations system beyond what is needed now
- ORM if not necessary
- visual fidelity changes
- schema redesign

This task is persistence + repository adapter only.

## Dependency guidance

Prefer a small SQLite dependency suitable for Node.

Allowed:

- `better-sqlite3` or another lightweight SQLite package if better suited.
- Small helper packages only if genuinely necessary.

Avoid:

- Prisma
- Drizzle
- Sequelize
- TypeORM
- Electron
- unrelated frameworks

If native SQLite dependency setup is problematic, stop and document the issue rather than introducing a large framework.

## Suggested file structure

Use a structure similar to this unless the repo already has a better compatible structure:

```text
src/
  persistence/
    sqlite/
      schema.sql
      createDatabase.ts
      seedExampleDataset.ts
      SQLiteRepository.ts
      validateSQLiteRepository.ts
      json.ts
```

Optional:

```text
data/
  mockups-dev.sqlite
```

Do not commit generated SQLite database files unless there is a strong reason. Prefer adding `*.sqlite`, `*.sqlite3`, `*.db` to `.gitignore`.

## Required tables

Implement initial tables for:

```text
productions
shots
screen_templates
screen_instances
screen_events
themes
devices
device_states
actors
apps
media_assets
animation_presets
render_presets
conversations
conversation_participants
messages
notifications
calls
data_sources
```

Use the field names documented in `07_initial_data_schema.md` and current Zod schemas.

The schema may be minimal but must support the current example dataset and current repository methods.

## Required JSON handling

Create clear helpers for JSON TEXT fields.

Requirements:

- stringify JSON objects before insert/update.
- parse JSON TEXT after select.
- validate parsed records with existing Zod schemas where practical.
- fail loudly on invalid JSON rather than silently returning empty objects.
- preserve snake_case raw record fields.

## Required repository methods

`SQLiteRepository` should support the methods currently used by the resolver pipeline.

At minimum, it should match the in-memory repository contract used in task 0005:

```text
getProduction(id)
getShot(id)
getScreenInstancesForShot(shot_id)
getScreenEventsForInstance(screen_instance_id)
getScreenTemplate(id)
getTheme(id)
getDevice(id)
getDeviceState(id)
getActor(id)
getMediaAsset(id)
getConversation(id)
getConversationParticipants(conversation_id)
getMessagesForConversation(conversation_id)
getNotification(id)
getApp(id)
```

Add any required methods already present in the current repository interface.

Do not expose SQL details to resolvers.

## Seed requirements

The seed should insert enough records to reproduce the current example shot.

It may reuse the existing `exampleDataset` from the in-memory repository.

Required behavior:

```text
npm run db:init
npm run db:seed
npm run validate:sqlite
```

or equivalent.

If combining init+seed is simpler, document the scripts clearly.

## Validation requirements

Create a validation script that:

1. Creates or opens a development SQLite database.
2. Applies the initial schema.
3. Seeds the example dataset.
4. Instantiates `SQLiteRepository`.
5. Resolves the same example shot/frame used by current resolver/visual validation.
6. Validates resolved chat props and message bubble props with Zod.
7. Optionally compares key output fields against the in-memory repository path.
8. Exits non-zero on failure.

All existing validation must still pass:

```text
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm test
```

Add SQLite validation to `npm test` if stable and practical.

## Remotion proof-of-concept interaction

Do not rework Remotion in this task.

If easy, add a small note or helper showing that Remotion can continue using the fixture/in-memory path for now.

The next task will introduce a debug UI that may choose between in-memory and SQLite-backed data.

## Documentation update

Update `PROJECT_STATUS.md` to reflect:

- SQLite persistence exists.
- SQLite seed for the example dataset exists.
- SQLiteRepository can feed the existing resolver pipeline.
- The in-memory repository remains available for fixtures/tests.
- No debug UI/editor/Electron implementation exists yet.

Set next recommended task to:

```text
Create a minimal debug calibration UI for selecting the example shot/frame, viewing the Remotion preview, and inspecting/editing theme/device/screen JSON values.
```

## Update exchange response

Create this response file:

```text
docs/exchange/responses/0010_sqlite_persistence_response.md
```

Use this format:

```md
# Codex Response 0010 — Minimal SQLite persistence and repository adapter

## Summary

## Files changed

## Questions / conflicts

## Tests

## Run commands

## Notes
```

## Notes requirements

In `## Notes`, include:

- chosen SQLite dependency.
- where the dev database is created.
- whether generated DB files are gitignored.
- whether SQLite and in-memory resolver outputs are equivalent or just compatible.
- any schema fields intentionally deferred.
- any future migration/versioning recommendations.

## Architecture Question rule

If you find a conflict between:

- architecture docs
- current Zod schemas
- current repository interface
- current resolver implementation
- current examples/fixtures
- accepted decisions D001–D014

do not silently invent a new architecture.

Instead, stop and create an Architecture Question in the response file.

## Acceptance criteria

- SQLite schema exists.
- SQLite init/seed script exists.
- SQLiteRepository exists.
- SQLiteRepository implements the resolver-facing repository contract.
- Example dataset can be seeded into SQLite.
- Existing resolver pipeline can resolve from SQLite.
- SQLite validation command passes.
- Existing validation commands still pass.
- `npm test` passes if present.
- Generated database files are ignored unless intentionally committed.
- `PROJECT_STATUS.md` is updated.
- Response file exists in `docs/exchange/responses/`.
- No debug UI, editor UI, Electron shell, final export pipeline, or visual redesign is added.
