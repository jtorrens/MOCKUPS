# Codex Response 0017 — Project browser and editor contracts

Implemented a UI direction pass after task 0016. This was not a formal numbered task file, but it captures the current state for ChatGPT/Codex handoff.

## Summary

The local app shell now separates the editable workspace into:

- `Project`: narrative hierarchy panels for Productions, Episodes, Shots, and Screens.
- `Library`: reusable resources such as actors, themes, module theme configs, devices, device states, media assets, render presets, apps, animation presets, and screen templates.

The Project hierarchy is no longer represented as flat table tabs. Productions contain episodes, episodes contain shots, and shots contain screen instances.

## Implemented

- Added a compact Project/Library browser in the left app shell.
- Added collapsible hierarchy panels:
  - Productions
  - Episodes for the selected production
  - Shots for the selected episode
  - Screens for the selected shot
- Added safe create actions for:
  - productions
  - episodes under the selected production
  - shots under the selected episode
- Added a `POST /api/app/record` endpoint for supported hierarchy record creation.
- Added backend defaults for newly created records:
  - productions get empty settings/metadata JSON;
  - episodes get the next `sort_order`;
  - shots get production/episode links, next `sort_order`, conservative duration/fps defaults, and first available actor/render preset when present.
- Kept duplicate/delete controls visible as future direction but disabled until deep-copy and cascade policy are defined.
- Preserved the right preview panel and existing validated autosave.

## Editor contract work

- Added module-scoped editor hint contracts under `src/debug-ui/module-editor-hints/`.
- Registered `core.chat@1` as the first module hint contract.
- The generic JSON tree now uses module hints for labels, widgets, and collapsed row summaries, while remaining a fallback editor for unknown modules.
- The generic tree hides internal JSON key names in normal tree mode; raw JSON remains available for inspection/debug.
- Chat message hints now treat media as an optional attachment that may coexist with text.

## Architecture updates

- Updated `PROJECT_STATUS.md`.
- Updated:
  - `docs/architecture/00_project_vision.md`
  - `docs/architecture/04_shot_builder.md`
  - `docs/architecture/05_decisions_log.md`
  - `docs/architecture/09_foundational_module_contracts.md`
  - `docs/architecture/10_module_theme_configs.md`
- Added decisions:
  - D035 — App navigation separates Project hierarchy from Library resources.
  - D036 — Module editor hints are registered by module and schema version.

## Validation

Ran successfully:

```bash
npm run typecheck
npm run app:check
npm run app:build
npm test
git diff --check
```

Also ran a local browser smoke check for the Project/Library browser.

## Notes / next likely work

This is a useful base, not final UI. Tomorrow's likely iteration areas:

- screen-instance creation flow under selected shot;
- duplicate/delete policy for productions/episodes/shots;
- specialized Chat content editor for WhatsApp-like messages, including media bubbles/captions;
- font picker;
- asset picker;
- Electron shell.

Deep duplication should not be implemented casually: production/episode duplication needs a clear policy for child records, internal references, asset reuse, IDs, themes, devices, and module JSON references.
