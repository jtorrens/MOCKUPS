# Project status

## Current state

Initial architecture/data schema documentation, the visual token/layout contract, and the foundational module contracts are complete and reviewed. TypeScript/Zod schemas, in-memory and SQLite repository paths, renderer-agnostic visual modules/layout, a minimal Remotion proof of concept, SQLite persistence, the first local core app shell, a generic structured JSON tree/value editor, module theme configs, module-scoped editor hint contracts, and a first Project/Library browser now exist. The shell is still local/dev-only and not a final production editor; no Electron shell, export pipeline, asset manager, font picker, deep duplicate/cascade delete workflow, or specialized Chat content editor has been implemented.

The accepted foundation is production-scoped and shot-centered, where productions contain episodes, episodes contain shots, and a shot is a device-screen action sequence rather than external plate placement. A shot now has an owner actor that supplies the default device and theme for its screen instances. Chat uses versioned module-owned data/config/token-override JSON as its sole runtime source.

## Completed

- Defined project vision and implementation boundaries.
- Defined the conceptual data model and SQL/JSON responsibilities.
- Defined resolver-to-render flow and visual module contracts.
- Defined ShotBuilder responsibilities.
- Recorded architecture decisions D001–D009.
- Defined the Codex task and handoff workflow.
- Defined a practical first schema for all production resources, narrative data, shots, screen instances, and events.
- Added raw-data and resolved-props JSON examples for the next schema implementation task.
- Reviewed schema naming, relationships, SQL/JSON boundaries, frame coordinates, references, and resolved-props sufficiency.
- Added inferred TypeScript types and Zod schemas for all documented domain entities, conversation participants, and resolved visual-module props.
- Added `npm run validate:examples` to validate all six documentation fixtures.
- Added an in-memory repository and fixture dataset for the lock-to-chat example.
- Added shot, screen-instance, chat-screen, and message-bubble resolvers with deterministic local-frame/write-on behavior.
- Added `npm run validate:resolver`; resolved chat and message-bubble props validate with Zod.
- Added a recursive Zod-validated `RenderableNode` tree and a renderer-agnostic visual module interface.
- Added ChatScreen, MessageBubble, StatusBar, ChatHeader, and Avatar module stubs plus a static registry.
- Added `npm run validate:visual`; resolved chat props produce a deterministic validated renderable tree.
- Defined canonical ownership and precedence for theme tokens, device metrics/state, instance props, resolved props, and renderable metadata.
- Added decisions D010–D014 and aligned examples, resolvers, schemas, and modules with the visual token contract.
- Added isolated approximate text measurement and renderer-agnostic ChatScreen/MessageBubble layout helpers.
- Added token-driven screen/header/status/message/avatar boxes, sent/received alignment, deterministic stacking, and keep-latest-visible overflow.
- Expanded visual validation to cover bounds, alignment, stacking, text/avatar boxes, determinism, and overflow.
- Added a Remotion adapter that recursively renders the existing `RenderableNode` tree without replacing domain resolvers or visual modules.
- Added the `ChatScreenPreview` composition and Remotion Studio/composition-check commands.
- Added the initial 19-table SQLite schema with relational fields and JSON TEXT columns.
- Added an idempotent example seed and `SQLiteRepository` implementing the existing resolver-facing contract.
- Added `db:init`, `db:seed`, and `validate:sqlite`; SQLite and in-memory resolve equivalent example ChatScreen props.
- Documented foundational contracts D015–D025 for logical design space, assets/icons, text/fonts, module ownership/versioning/editors, Chat participants, JSON responsibility separation, and debug UI boundaries.
- Added the target `screen_instances` module fields to Zod/examples and an additive SQLite schema-v2 migration without removing legacy fields.
- Added Chat module data/config schemas and a portable screen-module input/output contract; task 0012 subsequently made them canonical at runtime.
- Added light/dark theme-mode examples and merged selected mode plus local token overrides in the current Chat resolver.
- Made `core.chat` schema version 1 canonical: participants, header, messages, timings, senderParticipantId, and optional media references resolve from `screen_instances.module_data_json`.
- Made `module_config_json` the canonical Chat behavior source and retained `module_tokens_override_json` as the canonical local visual override source.
- Removed the Chat runtime fallback to `data_ref_json`, conversations, conversation participants, messages, and generic props; legacy SQLite tables remain physically present but the canonical fixture seeds no Chat rows into them.
- Added resolver/SQLite validation proving senderParticipantId direction and operation without legacy Chat records; Remotion continues through the same canonical resolver path.
- Added a local React/Vite debug calibration UI with production/shot/screen selection, frame calibration, SQLite-backed preview, six validated editable JSON sources, and read-only resolved/RenderableNode inspectors.
- Added a minimal local HTTP API that validates edits, writes them transactionally to SQLite, re-runs the existing resolver/module pipeline, and returns refreshed calculated output.
- Added `debug`, `debug:server`, `debug:ui`, `debug:check`, and `debug:build` scripts; visual browser smoke checks cover loading, invalid JSON blocking, save/re-resolve, and state restoration.
- Evolved the debug UI into the first practical app shell with a left editable workspace and a persistent right preview/output panel.
- Added the project hierarchy `Productions → Episodes → Shots → Screen Instances`, with reusable resources separated into a Library area.
- Added a Project/Library browser: Project exposes compact collapsible hierarchy panels; Library exposes reusable resource tables such as actors, themes, module theme configs, devices, device states, media assets, render presets, apps, animation presets, and screen templates.
- Added a safe first create flow for productions, episodes, and shots. Newly created episodes attach to the selected production; newly created shots attach to the selected episode and receive conservative defaults.
- Added debounced autosave for editable scalar fields and JSON object fields, with per-field save state and invalid JSON blocking before persistence.
- Added app-core HTTP endpoints for loading table definitions/records/options, creating supported hierarchy records, patching validated records, and resolving the current preview context.
- Kept Chat as the reference visual module through the existing resolver → RenderableNode → Remotion adapter path, with read-only resolved props and RenderableNode inspectors.
- Added `app`, `app:check`, and `app:build` aliases over the local app/debug workflow.
- Added a reusable schema-hintable JSON tree/value editor for all current app-shell JSON fields, with collapsible object/array navigation and primitive value controls.
- Added raw JSON fallback mode that shares the existing autosave pipeline and blocks invalid JSON before persistence.
- Restricted object key add/rename/delete controls by default so normal editors do not encourage manual schema mutation.
- Kept array structural controls for module data where line-like content such as Chat messages may need add/duplicate/delete/reorder behavior.
- Added UI hint infrastructure with color picker, checkbox, textarea, select/dropdown widgets, human labels, collapsed row summaries, and arrow icon controls for row movement.
- Added module-scoped editor hint contracts. `core.chat@1` now contributes Chat-specific labels/widgets/summaries without hardcoding Chat behavior into the generic JSON tree.
- Kept JSON autosave validated through the existing client parse checks and backend Zod/schema validation; current editors remain generic/hint-driven rather than final specialized module editors.
- Added accepted decisions D026–D030 for module theme configs and inherited override UI.
- Added `module_theme_configs` as an additive SQLite/Zod/repository entity scoped by `theme_id`, `module_id`, and `module_schema_version`.
- Seeded an iOS Light Chat module theme config for `theme_ios_light` + `core.chat` schema version 1.
- Moved practical Chat-specific design defaults out of global `themes.tokens_json` and into `module_theme_configs.tokens_json` while preserving current visual output.
- Updated Chat token resolution to merge global theme tokens, selected theme mode, module theme config tokens, selected module config mode, and screen-instance token overrides.
- Exposed Module Theme Configs as a core app-shell tab.
- Added inherited JSON parent data to the app API for `module_theme_configs.tokens_json` and `screen_instances.module_tokens_override_json`.
- Updated the structured JSON editor to mark differing inherited overrides in amber and offer a Restore inherited action.
- Added accepted decisions D031–D036 covering Chat typography, episode hierarchy, shot owner runtime defaults, Chat text+media messages, Project/Library browser direction, and extensible module editor hint contracts.

## Next

- Review the Project/Library browser visually, then choose: screen-instance creation flow, deep duplicate/delete policy, specialized Chat content editor, font picker, asset picker, or Electron shell.
- Create an Architecture Question before changing any accepted decision.
