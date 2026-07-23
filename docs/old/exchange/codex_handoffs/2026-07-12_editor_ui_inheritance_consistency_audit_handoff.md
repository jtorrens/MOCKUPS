# Desktop Editor UI, Inheritance and Contract Consistency Audit Handoff

Date: 2026-07-12

## Objective

Audit the desktop editor horizontally before Module Instance work expands. The goal is to find inconsistencies in UI language, inheritance, embedded navigation, ownership, persistence, tokens and preview parity across every editor, component and module.

This is an audit-first task. Do not perform broad fixes while discovering findings. Produce an evidence-backed report, classify findings, and only implement corrections explicitly approved afterward.

## Required reading

Read these documents completely before inspecting or changing anything:

- `AGENTS.md`
- `docs/architecture/editor_shell_non_negotiables.md`
- `docs/architecture/editor_modernization_roadmap.md`
- `docs/architecture/24_desktop_preview_component_architecture.md`
- `docs/architecture/25_component_migration_status.md`
- `docs/architecture/26_desktop_preview_pipeline.md`
- `docs/architecture/27_time_units_contract.md`

## Current architectural baseline

- `MainWindow` is shell-only.
- Editable scalar values go through `FieldDefinition`, `ValueKind` and registered dictionary controls.
- Parent components/modules own child-slot composition; renderers receive resolved atoms.
- Embedded component navigation uses:
  - edit icon: open the selected parent preset;
  - `···`: edit local overrides/composition in context.
- Persisted schema or vocabulary changes use explicit migrations. There are no compatibility fallbacks.
- Icon consumers persist semantic `iconToken` values. The active Theme resolves them through `Theme → References → Icon Set`.
- Frames are editorial timing, seconds are audio/video timing, and milliseconds are motion/fades/blink.
- Component Test Values represent the same declared input contract that parents and instances use.
- Static module composition must not leak into Module Instance runtime inputs.

## Important repository state

The preceding thread completed a substantial Conversation/component phase but may not yet have committed it when this handoff is opened. Before auditing:

1. Check `git status -sb` and recent commits.
2. Do not discard or overwrite uncommitted user/other-thread changes.
3. Coordinate with the originating thread if the working tree is still active.
4. Restart the desktop app after updating so cached layouts and preview processes do not hide migrations.

## Audit scope

### 1. UI language and information architecture

Check all cards, groups, fields, buttons, tooltips, dialogs and empty states for:

- consistent capitalization and terminology;
- singular/plural consistency;
- correct use of Component, Module, Instance, Variant, Preset, Input, Runtime, Calculated and Override;
- meaningful labels instead of internal JSON/schema terminology;
- consistent field order across analogous editors;
- units shown wherever applicable;
- descriptions that explain impact without duplicating labels;
- sibling cards/groups using the standard shared UI chrome and exclusivity rules.

### 2. Inheritance and overrides

For every inheritable field and embedded component:

- inherited value is displayed correctly;
- a consciously edited local value remains an override even if it later equals its parent;
- untouched values do not appear highlighted;
- amber/highlight state reflects effective local overrides only;
- reset/inherit removes the persisted override rather than writing the parent value;
- changing the parent refreshes inherited children;
- reopening and restarting preserve override state;
- nested overrides resolve through every slot in the breadcrumb path;
- Save/Promote as defaults has the intended authority and does not silently mutate unrelated variants.

### 3. Embedded navigation

For each embedded slot:

- the parent-preset icon opens the selected concrete preset;
- `···` opens the local override context;
- breadcrumbs name owner, slot, component class and preset correctly;
- nested `···` navigation works recursively;
- the embedded page exposes both owner-owned composition settings and child visual overrides where appropriate;
- no button is displayed if its action is unsupported or a no-op;
- returning from embedded contexts restores the expected owner/editor state.

### 4. Data ownership and contracts

Classify every editable property as one of:

- component class schema/default;
- component preset/variant;
- parent-owned embedded override;
- module-owned composition;
- Module Instance input;
- runtime input;
- calculated/internal value;
- Theme token/reference.

Flag fields exposed at the wrong level. In particular verify:

- fixed Conversation header Icon Rows are owned by the module, not its instances;
- Avatar subtitle remains a legitimate runtime value where required;
- calculated Labels receive final text from the owning resolver;
- Label and renderer do not know calculation sources;
- Icon Row slots preserve icon, label, content mode, state, Button preset and overrides;
- repeated child components use stable collection items and independent payloads.

### 5. Dictionary-control coverage

For every editable field:

- verify a `FieldDefinition` exists;
- verify the correct `ValueKind` is used;
- verify the registered dictionary control is used;
- detect raw or one-off controls that duplicate shared behavior;
- verify compound controls propagate their nested definitions, units, options and commit semantics;
- verify icon, token, typography, motion, placement, collection and component-preset controls behave consistently.

### 6. Persistence and migration behavior

Test representative fields in every editor:

- edit and navigate away/back;
- restart the application;
- duplicate a preset;
- modify the default preset;
- use Save as defaults where offered;
- confirm transient Test Values do not persist automatically;
- confirm saved Test Value defaults do persist;
- confirm retired identifiers are absent from seeds, layouts and `data/desktop-editor-spike.sqlite`;
- confirm migrations are idempotent and do not reintroduce deleted fields.

### 7. Tokens and Theme references

Audit:

- spacing fields use `theme.spacing.*`;
- radii use the canonical none/xs/s/m/l/xl/xxl/full vocabulary;
- typography inherits the Theme family unless the component explicitly owns a system-font role;
- colors come from the correct Theme category;
- Surface owns opacity and visual state treatment where intended;
- motion uses Theme motion tokens and correct units;
- every icon consumer persists a semantic token, never an SVG path/file;
- picker, editor preview, design preview and renderer resolve icons through the active Theme Icon Set;
- changing Theme updates colors, fonts, bars and icon set coherently.

### 8. Preview parity

For representative atoms, components and modules compare:

- isolated component preview;
- embedded component preview;
- module design preview;
- Module Instance/shot preview where available.

The same contract/payload must resolve identically. Flag preview-only catalogs, component-specific bridge logic, renderer timers, special-case typography, duplicated layout calculations or different fallback behavior.

### 9. Collections and actions

Verify:

- insert, delete, reorder and selection;
- stable unique IDs;
- insertion position is unambiguous;
- empty-state creation works;
- sibling cards follow exclusivity rules;
- multiple children of the same component type remain independent;
- actions reset to their prior state after their declared duration;
- repeated triggers and cancellation are safe;
- visibility conditions and action units are declarative and correct.

### 10. Performance and robustness

Stress:

- repeated Theme/Icon Set changes;
- repeated searches in icon/device pickers;
- rapid navigation into/out of nested embedded editors;
- repeated preview actions;
- opening editors with large collections;
- restarting after migrations.

Look for synchronous database/file work, repeated SVG parsing, WebViews used for thumbnails, uncancelled refreshes, duplicated event subscriptions and stale cached layouts.

### 11. Accessibility and interaction consistency

Check:

- keyboard focus and navigation;
- Esc/cancel behavior in dialogs;
- no trapping context menus on editor inputs;
- disabled controls are visibly and functionally disabled;
- buttons have tooltips and adequate hit areas;
- selection/override states are not conveyed only by subtle color;
- text remains readable in Light and Dark desktop modes.

### 12. Architecture enforcement

Run and extend checks where a durable invariant is discovered:

- `npm run desktop:schema-v1:validate`
- `npm test`
- `git diff --check`

Confirm no component-specific behavior leaks into shell, central bridge, common preview helpers or generic renderer.

## Suggested audit phases

### Phase A — UI, inheritance and navigation

Inventory fields/cards and manually verify labels, highlights, reset semantics, embedded navigation and persistence.

### Phase B — ownership, tokens and preview parity

Trace representative values from database/layout metadata through resolver and renderer. Verify runtime/static/calculated boundaries and Theme reference resolution.

### Phase C — robustness and enforcement

Stress interactions, reproduce performance failures, check migrations, and propose architecture tests for recurring classes of defects.

## Required deliverable

Create a report under `docs/audits/` with:

- executive summary;
- scope and tested matrix;
- findings ordered by severity;
- exact reproduction path;
- affected files/data records;
- violated rule or architectural boundary;
- recommended correction;
- whether the issue is safe to fix mechanically or requires product judgment;
- proposed automated check where applicable;
- explicit list of audited areas with no findings.

Use severity levels:

- P0: data loss, corruption or unusable editor;
- P1: broken contract, crash, incorrect persistence or major architecture violation;
- P2: inconsistent behavior, misleading inheritance/navigation or preview mismatch;
- P3: wording, polish or low-risk visual inconsistency.

Do not combine unrelated fixes into the audit commit. The report should be independently reviewable before implementation begins.

