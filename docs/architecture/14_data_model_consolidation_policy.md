# Data model consolidation policy

## Objective

This project now treats the current SQLite schema, JSON data model, seeds,
component classes, palette/theme tokens, module configs and module instances as
one explicit contract.

The runtime should not permanently support every experimental shape that existed
during development. Legacy compatibility belongs in migrations, normalization
tools and audits, not in normal resolution/rendering code.

## Main rule

```text
schema/data model → seed → UI → resolver → migration → audit → fallback removal
```

A data-model change is incomplete until that full chain is closed.

## What counts as a data-model change

Any change is a data-model change if it modifies:

- a SQL table, column, relation, index or constraint;
- the shape of a JSON field, such as `themes.tokens_json`,
  `module_theme_configs.tokens_json`, `component_classes.tokens_json`,
  `module_instances.content_json`, `module_instances.behavior_json` or
  `actors.metadata_json`;
- where a value lives in the inheritance stack: palette, theme, app, module,
  component class, module instance, content, behavior or animation;
- whether a field stores a free value, a palette token, a theme token, a
  component-class reference or an override;
- the UI control used to author that field;
- resolver behavior or render-time interpretation of that field.

## Strict procedure for future changes

Every future data-model change must include a short before/after note.

Example:

```text
Before:
chatBubbles.status.textColor = "gray_050"

After:
chatBubbles.status.textColorToken = "colors.status.secondary"
```

Then it must answer these questions:

1. Where does the value live?
   - `palette_colors`
   - `themes.tokens_json`
   - `apps.config_json`
   - `module_theme_configs.tokens_json`
   - `component_classes.tokens_json`
   - `module_instances.content_json`
   - `module_instances.behavior_json`
   - `module_instances.animation_json`
2. Is the value global, production-specific, theme-specific, app-specific,
   module-specific, component-specific, instance-specific, content or behavior?
3. Is it a design-space unit that must be scaled to device pixels?
4. Is it mode-specific (`light`/`dark`) or a single design value?
5. Does it require an editor descriptor?
6. Does it require a migration or normalization of existing rows?
7. Does the current-model audit need a new rule?
8. Which temporary fallback, if any, can be removed after migration?

## Fallback policy

Allowed fallbacks:

1. Official defaults
   - These live in seeds, component-class defaults or documented theme/module
     defaults.
2. Defensive visual fallbacks
   - Atomic visual modules may use a minimal fallback to avoid crashing on an
     already-resolved renderable node. They must not interpret legacy data
     models.
3. Temporary migration fallbacks
   - These must be explicitly marked:

```ts
// TEMP_MIGRATION_FALLBACK:
// Remove after audit:current-model passes without legacy <name>.
```

Disallowed fallbacks:

- Runtime branches that silently support both old and new JSON shapes forever.
- Invisible mode-level copies that override root editor values.
- Resolver defaults that hide missing required data.
- UI hints that expose a field without a matching descriptor/schema/runtime path.

## Resolver and visual-module boundary

Domain resolvers should be strict. They should receive current-model data,
resolve tokens and inheritance, scale design units, and produce validated
resolved props.

Visual modules may be defensive, but they should not know historical data
formats. If legacy data reaches a visual module, the resolver/audit contract has
already failed.

## Baseline checks

The current baseline is SQLite `user_version = 32` plus the current JSON
contracts for:

- `themes.tokens_json`
- `apps.config_json`
- `module_theme_configs.tokens_json`
- `component_classes.tokens_json`
- `module_instances.content_json`
- `module_instances.behavior_json`
- `module_instances.animation_json`
- `status_bars.config_json`
- `navigation_bars.config_json`
- `actors.metadata_json`

The legacy chat SQL tables (`conversations`, `conversation_participants`,
`messages`) may still physically exist for now, but they must not contain rows
used by the canonical chat flow.

## Required checklist for future model changes

```text
[ ] editor/card field inventory completed before implementation
[ ] before/after documented
[ ] data ownership level decided
[ ] SQL/Zod/type contract updated
[ ] seed/default updated
[ ] editor descriptor/UI updated
[ ] editor card fields audited against FieldDefinition → ValueRegistry → control registry
[ ] resolver updated
[ ] design-unit scaling updated if needed
[ ] migration/normalization added if existing data needs conversion
[ ] audit:current-model updated
[ ] temporary fallback removed or explicitly marked
[ ] validation scripts pass
```

No data-model change should leave old and new formats coexisting indefinitely.

## Destructive-step checkpoint rule

Before any destructive or write-enabled consolidation step, create a checkpoint
commit.

This includes:

- running a normalizer or migration with `--write`;
- deleting legacy fields, fallbacks, rows, columns or tables;
- tightening schemas so old data would fail validation;
- changing seed/reset behavior in a way that discards old shapes;
- mass-renaming palette/theme/component tokens;
- replacing IDs or references in existing data.

The checkpoint commit must be clean enough to return to immediately with normal
Git tools. If there are unrelated local changes, either commit them separately
or stop and ask for direction before continuing.

The only exception is a read-only audit phase. Audits that merely inspect data do
not require a new checkpoint commit.

## Reusable consolidation runbook

Use this runbook whenever the data model has evolved enough that runtime
fallbacks, migration helpers or duplicated JSON shapes start accumulating again.

### Phase 0 — stabilize current work

```bash
npm run typecheck
npm run check:architecture
npm run validate:resolver
npm run validate:sqlite
git status -sb
```

Start consolidation only from a known, committed baseline unless the task is
explicitly to inspect uncommitted work.

### Phase 1 — audit only

Run:

```bash
npm run audit:current-model
```

Rules:

- The audit must not write to the database.
- The first implementation may report known failures.
- Every reported failure should become either:
  - a migration/normalization task;
  - a deliberate documented exception;
  - or a rule adjustment if the audit is too strict.

### Phase 2 — document the actual baseline

Update this policy or a companion baseline document with the current shapes for
the affected tables/JSON fields.

For each field, document:

- owner level;
- storage path;
- mode-specific or single value;
- design-space or render-space unit;
- allowed token/reference type;
- UI editor;
- field definition / value kind / editor control;
- resolver that consumes it.

For each editor card touched during consolidation, include the field dictionary
audit:

```text
field path → FieldDefinition id → ValueRegistry kind → control registry control
```

Hidden/internal fields must be listed as intentionally hidden instead of simply
missing from the audit.

### Phase 3 — normalize/migrate useful data

Before running any write-enabled normalization, commit the audit/policy/script
baseline first.

Create or update a script such as:

```bash
npm run db:normalize-current-model
```

Rules:

- Convert only safe, deterministic cases automatically.
- Report ambiguous cases.
- Do not delete useful production data without explicit review.
- Prefer idempotent migrations.
- Run against a copy or transaction when possible.

### Phase 4 — make seeds canonical

Run:

```bash
npm run db:reset
npm run audit:current-model
```

The seed path must produce current-model data directly. A migration that fixes
seeded data is a smell unless it represents an intentional historical migration
test.

### Phase 5 — remove runtime fallbacks

After the audit passes:

- commit the migrated current-model data/scripts before deleting fallbacks;
- remove temporary compatibility branches from resolvers;
- remove editor normalizers that hide old fields;
- remove duplicated mode/root values;
- tighten Zod schemas where practical;
- keep only official defaults and defensive visual fallbacks.

### Phase 6 — close with validation

```bash
npm run typecheck
npm run validate:resolver
npm run check:architecture
npm run validate:sqlite
npm run audit:current-model
git diff --check
```

Then commit with a message that names the consolidation scope.

## Current scripts

```bash
npm run audit:current-model
```

Audits both:

- an isolated seeded in-memory SQLite database;
- the development SQLite database at `data/mockups-dev.sqlite`, if present.

The script is read-only for the development database.
