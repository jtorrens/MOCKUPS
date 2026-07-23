# Codex Response 0013 — Minimal debug calibration UI

## Summary

Implemented a local SQLite-backed debug calibration UI for selecting a production, shot, screen instance, and frame; editing the six canonical JSON sources; saving validated changes transactionally; re-resolving the selected frame; previewing the resulting ChatScreen through the existing Remotion adapter; and inspecting resolved props plus the read-only `RenderableNode` tree.

This is deliberately a technical calibration instrument, not the final editor.

## Files changed

- `.gitignore`
- `package.json`
- `package-lock.json`
- `tsconfig.json`
- `vite.config.ts`
- `index.html`
- `src/debug-server/debugService.ts`
- `src/debug-server/server.ts`
- `src/debug-server/checkDebugService.ts`
- `src/debug-ui/App.tsx`
- `src/debug-ui/main.tsx`
- `src/debug-ui/styles.css`
- `src/debug-ui/api/client.ts`
- `src/debug-ui/components/SelectionPanel.tsx`
- `src/debug-ui/components/JsonEditorPanel.tsx`
- `src/debug-ui/components/PreviewPanel.tsx`
- `src/debug-ui/components/InspectorPanel.tsx`
- `docs/architecture/09_foundational_module_contracts.md`
- `PROJECT_STATUS.md`
- `docs/exchange/responses/0013_debug_calibration_ui_response.md`

## Questions / conflicts

None. SQLite access remains inside the debug API/persistence boundary; resolvers and visual modules still receive repository/domain data rather than SQL. The preview consumes calculated `RenderableNode` through the existing adapter and does not become a source of truth.

## Tests

- `npm run typecheck` — passed.
- `npm run validate:examples` — passed.
- `npm run validate:resolver` — passed.
- `npm run validate:visual` — passed.
- `npm run validate:sqlite` — passed.
- `npm test` — passed.
- `npm run debug:check` — passed; an in-memory SQLite seed exposes selectable records and produces resolved props plus `RenderableNode`.
- `npm run debug:build` — passed with Vite 8.0.16.
- `npm run remotion:check` — passed.
- Browser smoke check — passed against the running local app:
  - seeded production/shot/`core.chat` selected;
  - frame 210 preview visible;
  - all six editors present;
  - resolved and RenderableNode inspectors present;
  - malformed JSON blocked before any save;
  - a valid config change saved to SQLite and re-resolved;
  - original config restored and preview remained visible.
- `git diff --check` — passed.

## Run commands

```bash
npm run db:init
npm run db:seed
npm run debug
```

Open:

```text
http://127.0.0.1:4173
```

The Vite UI uses port `4173`; the local debug API uses `4174`.

Additional checks:

```bash
npm run debug:check
npm run debug:build
```

## Notes

Technology: React 19 + Vite with plain CSS and no UI framework. `concurrently` starts the Vite dev server and a minimal Node HTTP API. The structure follows the suggested `debug-ui` / `debug-server` split.

SQLite access: the server opens `data/mockups-dev.sqlite`, lists production/shot/instance rows, decodes them through current schemas/helpers, and uses `SQLiteRepository` plus the existing resolver/module pipeline for calculated output. Saves update the selected screen instance, theme, device, and device-state records in one SQLite transaction.

Editable fields:

- `screen_instances.module_data_json`
- `screen_instances.module_config_json`
- `screen_instances.module_tokens_override_json`
- `themes.tokens_json`
- `devices.metrics_json`
- `device_states.state_json`

Read-only fields:

- resolved screen/module props;
- `RenderableNode` tree;
- current preview derived from that tree;
- warnings/validation feedback.

Validation: all editors require a JSON object before save. `core.chat` module data/config use `ChatModuleDataSchema` and `ChatModuleConfigSchema`; theme/device/state records are reconstructed and validated with their current Zod schemas. Invalid input is highlighted next to the relevant editor and never reaches SQLite. Server-side schema errors appear in the global error area.

Preview: the selected frame is resolved from the editable SQLite database. Chat output is rendered in React with `RemotionRenderableAdapter`, the same adapter used by the Remotion POC. Saving returns newly resolved props/tree and refreshes the preview. Inactive frames and unsupported screen renderers produce warnings rather than fabricating output.

Shortcuts/limitations:

- raw JSON textareas rather than a structured editor;
- one selected screen preview at a time rather than full multi-layer shot composition;
- only Chat currently has a visual screen renderer;
- no history/undo beyond reseeding the development database;
- preview inherits the existing logical-unit/physical-pixel typography calibration limitation;
- API is dev-only, unauthenticated, bound to localhost, and does not serve a production build;
- no asset browser, icon manager, font picker, Electron shell, or export controls.

Recommended next UX improvements: clearer dirty-state diffing per panel, collapsible JSON/inspector sections, schema-aware field controls, preview zoom, side-by-side light/dark comparison, and a small save history/revert mechanism. After visual workflow review, choose between calibration UX improvements, a font picker, an asset picker, or an Electron shell.
