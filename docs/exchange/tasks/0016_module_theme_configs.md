# Codex Task 0016 — Module theme configs and inherited override UI

## Goal

Implement the next layer of the MOCKUPS design system:

```text
global theme tokens
  ↓
module theme config tokens
  ↓
screen instance module token overrides
```

This task should move module-specific design tokens, especially Chat tokens, out of the global theme token JSON and into module-scoped theme configuration.

It should also update the JSON editor/UI so inherited overrides are visible:

```text
if a lower-level token differs from its inherited parent value
  → show the field in amber
  → show a restore button
  → restore writes the inherited parent value into the local JSON, or removes the override if that is the cleaner representation
```

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
docs/exchange/responses/0012_chat_module_data_canonical_response.md
docs/exchange/responses/0014_core_app_shell_response.md
docs/exchange/responses/0015_json_tree_editor_response.md
```

Review current implementation:

```text
src/debug-ui/
src/debug-server/
src/domain/schemas/
src/domain/repository/
src/domain/resolvers/
src/persistence/sqlite/
src/visual/
```

## Architecture decision to implement

The design token hierarchy is:

```text
Theme
  → reusable global visual language

Module theme config
  → module-specific defaults for a given theme and module

Screen instance token overrides
  → local visual exceptions for one instance of one module
```

Example hierarchy for Chat:

```text
themes.tokens_json
  global typography, colors, surfaces, accent, light/dark modes

module_theme_configs.tokens_json where module_id = core.chat
  bubble radius, tail geometry, message spacing, chat header behavior,
  message typography mapping, chat-specific light/dark colors

screen_instances.module_tokens_override_json
  one-off overrides for this specific chat instance
```

## Important distinction

The global Theme should not be the place where users edit every internal Chat-specific design value.

For example, these should not be treated as global theme concerns:

```text
bubble radius
bubble tail shape/position
message spacing
message group spacing
chat media window mask
typing cursor style
received/sent bubble geometry
```

Those belong to the Chat module theme config.

The global Theme may still define base tokens used by Chat:

```text
typography.body
typography.caption
colors.textPrimary
colors.background
colors.accent
surfaces
light/dark mode base values
```

The Chat module theme config may reference or inherit those base tokens.

## Required decisions to document

Update `docs/architecture/05_decisions_log.md` with new accepted decisions, continuing numbering from the current last decision.

Add decisions covering:

```text
Module-specific design tokens live in module theme configs, not directly in global theme tokens.

Module theme configs are scoped by theme_id + module_id.

Module theme configs may inherit/reference global theme tokens.

Screen instance token overrides remain the place for per-shot/per-instance visual exceptions.

JSON editors should visualize inherited override state: overridden values appear amber and offer a restore action.
```

## Create/update architecture docs

Create or update a document:

```text
docs/architecture/10_module_theme_configs.md
```

It should explain:

- theme tokens vs module theme config tokens vs instance overrides.
- token resolution order.
- how light/dark mode applies across global theme and module theme config.
- how module configs may reference base theme tokens.
- how overrides are detected.
- how restore-to-parent should behave.
- why module-specific design values should be edited in the module context rather than the global Theme tab.

Also update if needed:

```text
docs/architecture/07_initial_data_schema.md
docs/architecture/08_visual_tokens_layout_contract.md
docs/architecture/09_foundational_module_contracts.md
```

## Required data model

Add a table/entity for module theme configs.

Suggested table:

```text
module_theme_configs
--------------------
id
production_id
theme_id
module_id
module_schema_version
name
tokens_json
metadata_json
```

Requirements:

- There can be one or more module theme configs per theme/module.
- For now, the seed can use one config for `theme_ios_light` + `core.chat`.
- The runtime should be able to resolve the appropriate module theme config for a selected screen instance's `theme_id` and `module_id`.
- If no module theme config exists, fail clearly or use an explicit empty default only if documented.

Use additive SQLite migration only. Do not destructively modify existing tables.

## Token resolution order

Implement or document clearly this order:

```text
1. global theme tokens
2. selected theme mode overrides
3. module theme config tokens for theme_id + module_id
4. module theme config selected mode overrides, if present
5. screen_instance.module_tokens_override_json
```

The resolved module tokens are then passed to the module/visual pipeline.

If the current implementation has a simpler merge, update it carefully.

## Chat token migration

Move Chat-specific defaults out of global theme tokens where practical.

Chat-specific defaults should live in `module_theme_configs.tokens_json`.

Examples:

```text
messageList.screenGutter
messageList.messageSpacing
messageList.groupSpacing
bubble.maxWidthRatio
bubble.paddingX
bubble.paddingY
bubble.radius
bubble.tail
chat header height/background/separator
typing cursor tokens
chat media message defaults
```

Global theme tokens should keep only shared values:

```text
typography.body
typography.caption
colors/surfaces/accent
mode base colors
shared icon mapping if currently global
```

Do not perform a huge visual redesign. Preserve current visual output as much as practical by moving values rather than changing them.

## UI requirements

Update the app shell to expose module theme configs.

Options:

- add a new tab: `Module Theme Configs`;
- or add a module-specific section under Themes.

Prefer a separate tab for clarity in this first pass.

The tab should allow selecting/editing:

```text
module_theme_configs.tokens_json
module_theme_configs.metadata_json
module_id
theme_id
name
module_schema_version
```

If create/delete is not implemented yet, editing the seeded record is enough.

## Inherited override UI requirement

The structured JSON editor should support inherited comparison.

For any JSON field that has a parent/inherited value for the same path:

```text
if local value !== inherited value
  → mark row amber
  → show "Restore inherited" button
```

Restore behavior:

- For `module_theme_configs.tokens_json`, the parent is the resolved global theme tokens for compatible paths.
- For `screen_instances.module_tokens_override_json`, the parent is the resolved theme + module theme config value.
- Restoring should either:
  - remove the local override key if absence means inherit, or
  - set the local value to the parent value if removal is not practical.
- Prefer removal of the local override key when it cleanly represents inheritance.
- If removing would leave empty objects, optionally prune empty containers.

The UI should make clear:

```text
amber = local override differs from inherited parent
restore = return to inherited/default value
```

## Equality rules

Use deep equality for objects/arrays.

For primitives:

```text
string/number/boolean/null exact equality
```

For colors, do not implement advanced normalization yet. `#fff` and `#FFFFFF` may be treated as different unless there is already a helper.

## UI editor scope

Apply inherited override visualization at minimum to:

```text
screen_instances.module_tokens_override_json
module_theme_configs.tokens_json
```

If practical, also show inherited state in any theme mode override sections.

Do not overbuild.

## Backend/API requirements

The app API should provide enough data for inherited comparison:

For selected screen instance:

```text
local module_tokens_override_json
parent resolved module tokens
```

For selected module theme config:

```text
local module theme tokens
parent global theme tokens
```

This can be returned as an `inheritedJson` object alongside the editable record field, or through another simple mechanism.

Keep server-side validation authoritative.

## Validation requirements

All existing commands must still pass:

```text
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm run validate:sqlite
npm run app:check
npm run app:build
npm test
```

If Remotion scripts exist, keep them passing:

```text
npm run remotion:check
```

Update app smoke tests if practical:

- module theme configs tab exists.
- seeded Chat module theme config loads.
- screen instance override editor can show inherited comparison.
- changing an override marks it as amber.
- restore inherited returns value to parent or removes local override.
- preview still resolves.

Do not add brittle screenshot tests.

## Do not implement

Do not create or implement:

- full specialized Chat editor
- asset picker
- font picker
- Electron shell
- final export pipeline
- visual redesign
- destructive migration
- advanced token expression language
- advanced color normalization
- full schema-form generator

This task is module theme config + inherited override UI only.

## Documentation update

Update `PROJECT_STATUS.md` to reflect:

- structured JSON editor exists.
- module theme configs exist.
- Chat-specific design tokens live in module theme config.
- screen instance token overrides are visually marked when they override inherited values.
- restore inherited control exists for applicable JSON fields.
- no asset picker/font picker/Electron/export pipeline exists yet.

Set next recommended task to one of:

```text
Review module theme config and inherited override workflow visually, then choose: screen-instance creation flow, font picker, asset picker, or Electron shell.
```

## Update exchange response

Create this response file:

```text
docs/exchange/responses/0016_module_theme_configs_response.md
```

Use this format:

```md
# Codex Response 0016 — Module theme configs and inherited override UI

## Summary

## Files changed

## Questions / conflicts

## Tests

## Run commands

## Notes
```

## Notes requirements

In `## Notes`, include:

- how module_theme_configs are represented in SQLite/Zod/API.
- what Chat tokens moved from global theme into module theme config.
- how token resolution order works.
- how inherited comparison is provided to the UI.
- how amber override marking works.
- how restore inherited works.
- what remains unresolved or approximate.
- recommended next step.

## Architecture Question rule

If you find a conflict between:

- architecture docs
- current schemas
- current SQLite schema
- current repository implementation
- current resolver implementation
- current app shell
- current JSON editor
- accepted decisions already in the log

do not silently invent a new architecture.

Instead, stop and create an Architecture Question in the response file.

## Acceptance criteria

- `module_theme_configs` entity/table exists.
- Seed includes a Chat module theme config for the current example theme/module.
- Chat-specific design tokens are moved to module theme config where practical.
- Resolver uses global theme + module theme config + instance overrides.
- App shell exposes module theme configs for editing.
- JSON editor can receive inherited parent values.
- Overrides are shown in amber when local value differs from inherited value.
- Restore inherited button exists for applicable override rows.
- Restore inherited updates local JSON appropriately.
- Existing preview still works.
- Existing validation commands pass.
- npm test passes.
- PROJECT_STATUS.md is updated.
- Response file exists in docs/exchange/responses/.
- No asset picker, font picker, Electron shell, final export pipeline, or specialized Chat editor is added.
