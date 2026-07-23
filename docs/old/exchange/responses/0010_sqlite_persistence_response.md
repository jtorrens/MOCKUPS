# Codex Response 0010 — Minimal SQLite persistence and repository adapter

## Summary

Implemented the initial 19-table SQLite schema, JSON TEXT serialization helpers, an idempotent example seed, and a Zod-validating `SQLiteRepository` with the same resolver-facing contract as `InMemoryRepository`. The existing shot/chat/message pipeline resolves from SQLite without SQL leaking into resolvers or visual modules.

## Files changed

- `.gitignore`
- `package.json`
- `package-lock.json`
- `src/domain/repository/types.ts`
- `src/domain/repository/fixtures/exampleDataset.ts`
- `src/persistence/sqlite/schema.sql`
- `src/persistence/sqlite/json.ts`
- `src/persistence/sqlite/paths.ts`
- `src/persistence/sqlite/createDatabase.ts`
- `src/persistence/sqlite/initializeDevelopmentDatabase.ts`
- `src/persistence/sqlite/seedExampleDataset.ts`
- `src/persistence/sqlite/seedDevelopmentDatabase.ts`
- `src/persistence/sqlite/SQLiteRepository.ts`
- `src/persistence/sqlite/validateSQLiteRepository.ts`
- `PROJECT_STATUS.md`
- `docs/exchange/responses/0010_sqlite_persistence_response.md`

## Questions / conflicts

None. Stable fields and relationships are SQL columns/foreign keys; flexible configuration remains validated JSON stored as TEXT, preserving D004 and the existing repository boundary.

## Tests

- `npm run db:init` — passed.
- `npm run db:seed` — passed.
- `npm run validate:sqlite` — passed: 19 required tables, seed integrity, Zod ChatScreen/MessageBubble output, invalid-JSON failure, and output equivalence.
- `npm test` — passed, including TypeScript, examples, in-memory resolver, visual layout, and SQLite validation.
- SQLite and in-memory paths produce exactly equivalent resolved ChatScreen props for shot frame 210.
- Package audit reports no vulnerabilities.

## Run commands

```bash
npm run db:init
npm run db:seed
npm run validate:sqlite
npm test
```

## Notes

SQLite dependency: `better-sqlite3@12.11.1`, chosen for its small synchronous Node API, which matches the current synchronous repository contract without an ORM. Types are development-only.

The development database is generated at `data/mockups-dev.sqlite`. `*.sqlite`, `*.sqlite3`, `*.db`, and companion WAL/SHM files are gitignored; no generated database is committed.

The current example seed is idempotent and transactional. It seeds the records needed by the lock-to-chat shot, including the two referenced animation presets. `render_presets`, `calls`, and `data_sources` tables exist but intentionally have no example rows. The repository exposes only methods currently required by resolvers; CRUD writes and methods for unused entities are deferred.

All documented initial fields are represented. Media uses the current canonical `uri` field rather than adding a second `path` column. Large media remains external to SQLite.

Schema versioning currently uses `PRAGMA user_version = 1`. Before multiple production databases or destructive schema changes exist, replace the single idempotent schema application with ordered, transactional migration files and a recorded migration history/checksum.

Remotion continues using the fixture/in-memory bridge for deterministic preview. It requires no changes: a future debug UI may explicitly choose either repository adapter upstream of the same resolvers.

No debug/editor UI, Electron shell, ORM, final import/export or render pipeline, visual redesign, asset-management UI, or persistence framework was added.
