# Codex Response 0014 — Core app shell with data tabs, autosave and preview

## Summary

Implemented the first practical MOCKUPS app shell by evolving the existing local debug UI rather than creating a separate workflow.

The new shell uses:

- a left panel with core entity tabs and record editors.
- a persistent right panel with production/shot/screen context, frame control, Chat preview, warnings, resolved props/module input, and RenderableNode inspectors.
- debounced autosave for scalar fields and valid JSON object fields.
- app-core API endpoints over the existing SQLite/debug-server path.

Chat remains the reference preview module and still renders through the current resolver → visual module → RenderableNode → Remotion adapter path.

## Files changed

- `src/debug-server/debugService.ts`
  - Added app table definitions for required core tabs and optional easy tabs.
  - Added app record listing, app state loading, validated record patching, JSON decode/encode, and Zod-backed row validation.
  - Preserved the existing debug payload/preview path.
- `src/debug-server/server.ts`
  - Added local/dev app-core endpoints:
    - `GET /api/app`
    - `PATCH /api/app/record`
    - `GET /api/app/preview`
- `src/debug-server/checkDebugService.ts`
  - Expanded validation to cover required tabs, selectable seeded context, invalid JSON rejection, valid JSON persistence, and preview re-resolution.
- `src/debug-ui/api/client.ts`
  - Reworked the client around app state, app records, patch updates, and preview payloads.
- `src/debug-ui/App.tsx`
  - Replaced the single debug calibration screen with the two-panel core app shell.
- `src/debug-ui/components/EntityTabs.tsx`
  - Added tab and record selection UI for app tables.
- `src/debug-ui/components/RecordEditor.tsx`
  - Added generic scalar/JSON record editor with per-field debounced autosave and save state.
- `src/debug-ui/components/AppPreviewPanel.tsx`
  - Added persistent preview/output panel with selection context, frame input/slider, warnings, Chat preview, and read-only inspectors.
- `src/debug-ui/styles.css`
  - Added shell, tab browser, editor, autosave state, and preview panel styles.
- `package.json`
  - Added `app`, `app:check`, and `app:build` aliases.
- `MOCKUPS Debug.command`
  - Updated launcher copy/command to use the new app shell entry point.
- `PROJECT_STATUS.md`
  - Updated current status, completed work, and next recommended slices.

## Questions / conflicts

- No architecture conflicts found.
- Creation/deletion is intentionally deferred. The task allowed this if not easy.
- Raw JSON editors remain in place for JSON columns, as requested.
- Preview support remains Chat-focused. Unsupported/inactive screen instances surface warnings instead of fabricating output.
- The shell is still local/dev-only; no Electron shell, export workflow, asset manager, font picker, or final production editor was added.

## Tests

Passed:

```text
npm run typecheck
npm run app:check
npm run app:build
npm test
npm run remotion:check
git diff --check
```

Visual smoke passed with Chromium headless against the local app:

```text
✓ Core app shell loaded at http://127.0.0.1:4173/
✓ Required and optional tabs are present
✓ Persistent preview, resolved props and RenderableNode inspectors are present
✓ Invalid JSON is shown and not saved
✓ Valid JSON autosaves and refreshes resolved preview output
✓ Smoke edits restored original module_config_json
```

Screenshots:

```text
/private/tmp/mockups-core-app-shell.png
/private/tmp/mockups-core-app-shell-invalid-json.png
```

## Run commands

Start the local app shell:

```bash
npm run app
```

Check app service behavior:

```bash
npm run app:check
```

Build the app shell:

```bash
npm run app:build
```

The updated macOS launcher also starts the shell:

```text
MOCKUPS Debug.command
```

## Notes

- Implemented all required tabs:
  - Productions
  - Shots
  - Screen Instances
  - Actors
  - Themes
  - Devices
  - Device States
  - Media Assets
  - Render Presets
- Also implemented optional tabs:
  - Apps
  - Animation Presets
  - Screen Templates
- Autosave validates JSON client-side before sending. Invalid JSON never reaches persistence.
- The backend validates patched rows with available Zod schemas before writing to SQLite.
- Chat module JSON remains canonical in `screen_instances.module_data_json`.
- Chat config remains canonical in `screen_instances.module_config_json`.
- Local visual overrides remain canonical in `screen_instances.module_tokens_override_json`.
- RenderableNode remains calculated output and read-only.
- SQLite remains behind the local API/repository boundary and does not leak into visual modules.

Recommended next slice: add a simple create flow for screen instances or polish the calibration UX now that the shell shape exists.
