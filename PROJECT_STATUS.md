# Project status

## Current state

Initial architecture/data schema documentation, the visual token/layout contract, and the foundational module contracts are complete and reviewed. TypeScript/Zod schemas, in-memory and SQLite repository paths, renderer-agnostic visual modules/layout, a minimal Remotion proof of concept, SQLite persistence, the first local core app shell, structured JSON/token editors, module theme configs, module-scoped editor hint contracts, a production-first Project/App/Production Data browser, a persistence audit/check, a minimal Electron development shell, and explicit module-instance persistence now exist. The shell is still local/dev-only and not a final production editor; no export pipeline, asset manager, deep duplicate/cascade delete workflow, packaging/installer, or final module-instance creation workflow has been implemented.

The current app shell is now substantially more usable for authoring: it has a production-first layout, accordion-based Project/App/Production Data navigation, resizable left/editor/preview panels, independent panel scrolling, responsive preview fitting, screen-instance editors organized by conceptual accordion cards, module-theme-config token editors grouped by friendly labels, inherited/override rows with restore behavior, installed-font discovery where available, typed controls for number/text/color/font/select values, centralized mode-aware color editors, and structured Chat `Module Content` cards for participants and messages.

The accepted foundation is production-scoped and shot-centered, where productions contain episodes, episodes contain shots, and a shot is a device-screen action sequence rather than external plate placement. A shot now has an owner actor that supplies the default device and theme for its screen instances. Chat uses versioned `module_instances.content_json` and `module_instances.behavior_json` as its runtime source. Visual values are resolved from Theme → App → Module Theme Config; per-shot visual token overrides are no longer part of the active model.

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
- Made `core.chat` schema version 1 canonical: participants, header, messages, timings, senderParticipantId, and optional media references resolve from module-owned JSON.
- Added explicit `module_instances`; Chat content now resolves from `module_instances.content_json` and per-shot behavior resolves from `module_instances.behavior_json`.
- Removed per-instance visual token overrides from the active resolver/editor model; visual values now resolve from reusable Theme → App → Module Theme Config layers.
- Removed the Chat runtime fallback to `data_ref_json`, conversations, conversation participants, messages, and generic props; legacy SQLite tables remain physically present but the canonical fixture seeds no Chat rows into them.
- Added resolver/SQLite validation proving senderParticipantId direction and operation without legacy Chat records; Remotion continues through the same canonical resolver path.
- Added a local React/Vite debug calibration UI with production/shot/screen selection, frame calibration, SQLite-backed preview, six validated editable JSON sources, and read-only resolved/RenderableNode inspectors.
- Added a minimal local HTTP API that validates edits, writes them transactionally to SQLite, re-runs the existing resolver/module pipeline, and returns refreshed calculated output.
- Added `debug`, `debug:server`, `debug:ui`, `debug:check`, and `debug:build` scripts; visual browser smoke checks cover loading, invalid JSON blocking, save/re-resolve, and state restoration.
- Evolved the debug UI into the first practical app shell with a left editable workspace and a persistent right preview/output panel.
- Added the project hierarchy `Productions → Episodes → Shots → Screen Instances`, with reusable resources separated into a Library area.
- Added a Project/Library browser: Project exposes compact collapsible hierarchy panels; Library exposes reusable resource tables such as actors, themes, module theme configs, devices, device states, render presets, apps, animation presets, and screen templates.
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
- Added inherited JSON parent data to the app API for `module_theme_configs.tokens_json`.
- Updated the structured JSON editor to mark differing inherited overrides in amber and offer a Restore inherited action.
- Added accepted decisions D031–D038 covering Chat typography, episode hierarchy, shot owner runtime defaults, Chat text+media messages, Project/Library browser direction, extensible module editor hint contracts, non-destructive SQLite startup/validation, and the minimal Electron shell boundary.
- Audited the SQLite save path. Current UI autosave reaches backend validation and SQLite writes; `app:persistence-check` verifies scalar and JSON edits survive database reopen and are used by preview resolution.
- Fixed a persistence-risk bug: `validate:sqlite` now uses isolated in-memory SQLite instead of reseeding `data/mockups-dev.sqlite`.
- Made `db:seed` non-destructive when the development database already has productions, and added explicit destructive `db:reset` for restoring fixture data.
- Added a minimal Electron shell that loads the existing Vite app/debug server workflow with `contextIsolation` enabled, renderer Node integration disabled, and a narrow `window.mockupsNative` preload boundary for future native file/font APIs.
- Added a production switcher and production action modal shell; production add is implemented, while deep duplicate/delete remains intentionally deferred.
- Reworked the app layout into resizable left navigation/editor and right preview panes with independent scrolling and preview scaling that fits both width and height.
- Removed read-only resolver/adapter inspector panels from the main preview surface to prioritize authoring and visual feedback.
- Added accordion editors for screen instances: General, Module Content, Behavior, and Overrides. Module Content has nested module-data groups such as Participants, Header, and Messages.
- Removed Screen Templates from the active authoring model; the previous screen-template inheritance decision is superseded by direct Theme → App → Screen/Module → Screen Instance inheritance.
- Added tabbed module theme config editing: Design and Theme. Design tabs follow token groups; nested conceptual groups render as sections only when they contain multiple rows.
- Added a token/override editor that shows Property, Token/Internal Path, and Override columns. Property labels are friendly and compacted by group; the Token column intentionally stays internal.
- Added typed override controls for booleans, numbers, text, colors, font families, and font styles/weights.
- Added installed-font discovery through `queryLocalFonts` when available and through Electron's narrow `mockups:listFonts` bridge on macOS, with fallback font families/styles.
- Added accepted decisions D039–D041 covering production-owned workspace/templates, screen-template inheritance, and structured editor label/group conventions.
- Added accepted decisions D042–D044 covering removal of Screen Templates from active runtime/editor inheritance, logical design-unit scaling, and the inspector-first accordion/module-content UI direction.
- Added App-level token/default editing through existing App records and centralized mode-aware color editing for Theme/App/Module levels.
- Reworked the left workspace into accordion sections for Project, Apps, and Production Data, each containing its own hierarchy/tree.
- Reworked Chat module-instance `content_json` presentation as `Module Content`: participants and messages now render as structured content cards with friendly labels, typed widgets, useful collapsed summaries, and add/duplicate/delete/reorder controls.
- Fixed grouped JSON/content editing for root arrays, double-serialized JSON strings, and group-context module editor hints.
- Added SQLite schema v7 `module_instances`, in-memory/SQLite repository accessors, seed/reset support, and resolver validation for the new module-instance boundary.
- Added the first field-descriptor catalog for canonical UI paths such as `app.general.id`, `module.design.typography.message.size`, and `moduleInstance.content.messages[].text` without storing that metadata inside JSON values.
- Added the first shared inspector field primitive (`InspectorFieldRow` / `InspectorRestoreButton`) and migrated token overrides, mode colors, and module-content primitive rows onto it as the first UI-unification step.

## Next

- Review the current app shell visually and choose the next workflow: continue unifying accordion sections around field descriptors, module-instance creation/duplication UI, screen-instance creation, asset registration/native file picker, production duplicate/delete policy, stronger UI smoke tests for content editing, or Electron menu/package polish.
- Create an Architecture Question before changing any accepted decision.
