# Desktop Editor UX/UI Accepted Improvements — Implementation Handoff

Date: 2026-07-12

Status: implementation authorized only when a future thread receives an explicit
`implement`, `start` or equivalent instruction. This handoff itself changes no
production code, seed data, committed database or architecture check.

## Objective

Implement the final accepted decisions from:

- `docs/audits/2026-07-12_desktop_editor_ux_ui_visual_audit.md`;
- revised proposals `06` through `10` under
  `docs/audits/assets/2026-07-12_desktop_editor_ux_ui_visual_audit/`.

The work improves comprehension, state feedback, accessibility and navigation
without changing the editor architecture, component contracts, preview semantics
or Production transport behavior.

## Required reading before implementation

Read completely:

- `AGENTS.md`;
- `docs/architecture/editor_shell_non_negotiables.md`;
- `docs/architecture/editor_modernization_roadmap.md`;
- `docs/architecture/24_desktop_preview_component_architecture.md`;
- `docs/architecture/25_component_migration_status.md`;
- `docs/architecture/26_desktop_preview_pipeline.md`;
- `docs/architecture/27_design_production_ux_separation.md`;
- `docs/architecture/28_static_preview_update_experience.md`;
- the final audit and this handoff.

Run `git pull`, preserve unrelated changes, restart the desktop application and
record the starting commit before testing.

## Non-negotiable boundaries

- `MainWindow` remains shell-only. New behavior belongs in shared shell
  primitives/controllers or the owning editor class.
- New context UI is metadata-driven. It must not branch on Bubble, Label, Audio,
  Screen or another concrete editor/component.
- Editable values remain on the `FieldDefinition` → dictionary-control route.
- Preview states remain generic. They must not add component knowledge to common
  preview helpers, bridge or web renderer.
- Production transport controls, separators, frame units and `Frame` / `Screen` /
  `Shot` scopes are out of scope for redesign.
- Do not use a global spinner to conceal D01.
- Do not introduce compatibility fallbacks or silently rewrite persisted data.
- If a phase affects committed desktop behavior/data/assets, include all required
  parity artifacts in the same commit. No data change is expected for these UX
  phases; stop and explain if one appears necessary.

## Final decisions

### D01

Diagnose the native editor blackout independently before animation. Establish
the cause and a no-black-frame acceptance invariant. A global spinner or blanket
loading overlay is not an accepted fix.

### D05

Provide generic non-renderable, loading and error states. Loading retains the
previous resolved preview and adds only a local progress layer. A non-renderable
state offers `Ver elementos renderizables` only when the shell has a deterministic
destination/action.

### D04

Label the area `Valores de prueba · temporales`. Use `Restablecer valores de
prueba` when reverting temporary values, or `Recargar predeterminados` only when
that is the real operation. `Guardar como valores predeterminados…` is disabled
when there are no differences and confirms destination and affected fields.

### D03

Create a shared metadata-driven primitive integrated with the existing
breadcrumb. Use typed identities such as `Componente: Bubble · Variante: Bubble`,
never ambiguous `Bubble / Bubble`. Include override count and dirty/saved state
without duplicating the breadcrumb/title.

### D02

Adopt the balanced hybrid navigation model. Cards represent first-level
functional sections; a shared hierarchical-row primitive represents apps,
components, modules, variants, Episodes, Shots, Screens and other navigable
objects. Use a fixed chevron/icon column, 16 px indentation per depth, states
beside the name and actions at the right. Row click selects, chevron expands,
`Open`/`Enter` changes editing context, `+` creates the named child and `…`
contains less-frequent or destructive actions. Actions may appear on hover,
selection or keyboard focus, but hover must never be their only access path and
their reserved column must prevent layout shift. Protected variants remain
selectable/openable; only prohibited actions are disabled individually.

### P01

Add a persistent Production breadcrumb and read-only inherited Device, Theme and
mode context. Do not replace, regroup or restyle the existing compact transport,
its separators, units or scope controls as part of this work.

### V01/T01

Perform one shared pass over tokens, measured contrast, visible action labels and
contextual tooltips/accessibility names. Use visible labels when space permits and
contextual tooltips for compact controls.

## Implementation phases and verifiable commits

Each phase should start from a green tree, be reviewable independently and end in
one focused commit unless the phase explicitly identifies a diagnosis gate.

### Phase 1 — D01 independent diagnosis

Goal: reproduce and locate the blackout before changing UX surfaces.

Work:

1. Reproduce with repeated Design/Production switches, tree expand/collapse and
   selection changes.
2. Record whether the native visual tree is detached, hidden, invalidated or
   blocked while the WebView survives.
3. Measure duration and correlate it with layout refresh, preview request and
   window composition.
4. Check focus/selection continuity and macOS/Windows parity where available.
5. Write a focused diagnosis and proposed correction. Do not mask the symptom.

Gate: if the correction changes shared lifecycle behavior materially, stop for
review after the diagnosis. The accepted invariant is: previous native content
remains composed until replacement content is ready, with no full-window black
frame across 20 consecutive representative actions.

Commit plan:

- `docs: diagnose desktop editor navigation blackout` for diagnosis only;
- later, after approval, `fix: preserve editor surface during navigation` for the
  actual correction and focused tests.

Verification:

- recorded reproduction matrix before/after;
- 20-action no-black-frame manual run;
- focus and selected node preserved;
- `npm run test` and `npm run check:architecture` after a code fix.

### Phase 2 — D05 generic preview states

Goal: make non-renderable, loading and error states explicit without disturbing
preview/component boundaries.

Work:

- introduce or extend a shared preview-state model and shared panel surface;
- retain the last successful preview during loading;
- place progress only over the preview region;
- expose deterministic recovery/open actions only when supplied by generic
  context metadata;
- preserve error details, retry and editor state.

Commit: `feat: add contextual desktop preview states`

Verification:

- non-renderable owner with and without deterministic destination;
- loading while a previous preview exists and on first load;
- recoverable and non-recoverable error;
- narrow/normal widths, Light/Dark, keyboard focus and Escape behavior;
- architecture check confirms no component names in shared preview code;
- `npm run test` and `npm run check:architecture`.

### Phase 3 — D04 Test Values semantics

Goal: make temporary preview values and persistence conversion unmistakable.

Work:

- add the shared temporary-state label and explanatory text;
- inspect the actual reset operation before choosing `Restablecer valores de
  prueba` or `Recargar predeterminados`;
- compute whether Test Values differ from applicable defaults using existing
  generic contracts;
- disable Save when no differences exist;
- confirm destination variant and affected fields before conversion;
- preserve declared playback actions and their temporary state.

Commit: `feat: clarify temporary preview test values`

Verification:

- zero, one and multiple differences;
- reset/reload semantics match label exactly;
- Save disabled without differences and enabled with differences;
- confirmation lists destination and changed fields;
- switching context does not silently persist temporary values;
- representative scalar, reference and collection inputs;
- `npm run test` and `npm run check:architecture`.

### Phase 4 — D03 metadata context strip

Goal: integrate explicit typed identity and state with the existing breadcrumb.

Work:

- define one shared context-strip metadata contract;
- source component/module/editor/variant identity, override count and dirty/saved
  state from generic selection/layout metadata;
- compose with the current breadcrumb without duplicating location/title;
- support embedded paths and reduced width;
- provide full accessible text when visual truncation is required.

Commit: `feat: add metadata-driven editor context strip`

Verification matrix:

- App, component class, protected Default variant, ordinary variant, embedded
  component slot, module and Production Screen;
- saved, dirty, inherited and overridden states;
- normal and narrow editor panels;
- no concrete component checks/imports in the primitive;
- `MainWindow` remains orchestration-only;
- `npm run test` and `npm run check:architecture`.

### Phase 5 — D02 balanced hybrid navigation

Goal: distinguish functional sections from navigable objects, increase useful
density and make selection, expansion, opening, creation and options predictable.

Work:

1. Create one reusable metadata-driven hierarchical-row primitive in the shared
   editor-shell UI. It receives depth, expanded/children state, selection/focus,
   semantic icon, title, optional specific metadata, badges/status and declarative
   primary/context actions. It must not know concrete record or component types.
2. Keep existing shared cards for first-level functional sections. Do not use a
   complete nested card for every individual object or variant.
3. Use a fixed left chevron/icon column, 16 px indentation per depth and a
   reserved right action column. Start with approximately 48–52 px section
   headers, 40 px object rows and 36–40 px variant rows, then validate actual hit
   targets in Avalonia before compacting further.
4. Formalize shared actions/metadata for Select, Expand, Open, Add and Options.
   Keep actions available on selection and keyboard focus as well as hover.
5. Keep protected rows selectable/openable and derive disabled state per action,
   not per row. Keep state near the name and destructive/rare actions inside
   `…`.
6. Expose created-object nouns on every Add tooltip and accessible name. Reserve
   action width so revealing controls never moves the title or badges.
7. Migrate in controlled steps: Component Classes first; validate hierarchy,
   density, variants and focus; then Apps/resources; finally Episode/Shot/Screen
   Production navigation using exactly the same primitive and grammar.
8. Leave search, filters, drag-and-drop, selection multiple and destructive
   keyboard shortcuts for a later phase. `Enter` may open; double-click can mirror
   it as a convenience but must never be required.

Commit: `feat: standardize desktop navigation actions`

Verification matrix:

- protected and ordinary variants;
- expandable group, selectable leaf and row that opens another context;
- Add enabled/disabled with explicit created-object name;
- Options available independently of prohibited mutation;
- keyboard navigation, focus ring, tooltips, hover/selected parity and narrow width;
- no horizontal movement when secondary actions appear;
- approximately twice the current visible object count without reducing clarity
  or interactive targets below the accepted size;
- cards remain section containers while individual objects and variants render as
  hierarchical rows;
- Design and Production navigation use the same action grammar;
- `npm run test` and `npm run check:architecture`.

### Phase 6 — Accepted portion of P01

Goal: strengthen Production location and inherited context without touching
transport.

Work:

- add persistent `Episode › Shot › Screen` breadcrumb from Production context;
- show inherited Device, Theme and mode as read-only context;
- use `Screen` vocabulary and avoid internal record names;
- leave the existing transport subtree unchanged unless a compile-only wiring
  adjustment is unavoidable and behavior/visual output remains identical.

Commit: `feat: expose production context and inheritance`

Verification:

- populated, empty and missing-Screen Shots;
- context updates when Episode/Shot/Screen changes;
- inherited values are not editable Design-style selectors;
- screenshot/diff confirms transport controls, separators, units and scopes are
  unchanged;
- frame navigation behavior remains identical;
- `npm run test` and `npm run check:architecture`.

### Phase 7 — Joint V01/T01 accessibility polish

Goal: validate and improve contrast, tokens, labels and compact tooltips as one
shared-system pass.

Work:

- inventory the actual shared foreground/background/icon/focus/disabled tokens;
- measure contrast in Light and Dark before changing values;
- adjust shared tokens rather than local editor colors;
- add visible noun-scoped labels where space permits;
- add contextual tooltips and accessible names for compact controls;
- verify disabled, selected, hover and focus states independently of color alone.

Commit: `fix: improve editor contrast labels and tooltips`

Verification:

- documented before/after contrast measurements against the agreed AA target;
- Light/Dark and normal/narrow widths;
- keyboard-only journey and screen-reader names for representative controls;
- no local one-off color or tooltip rules where a shared primitive applies;
- `npm run test` and `npm run check:architecture`.

## Cross-phase verification

After every implementation commit:

1. inspect `git diff --check` and the exact staged scope;
2. run the smallest focused checks for the changed primitive;
3. run `npm run check:architecture`;
4. run `npm run test` before closing the phase;
5. manually exercise both Design and Production at normal and reduced widths;
6. verify Light and Dark for any visual/token change;
7. confirm `data/desktop-editor-spike.sqlite` and assets are unchanged unless a
   reviewed parity requirement explicitly justified them.

Do not combine phases merely to reduce commit count. A later phase may depend on
an earlier shared primitive, but its behavioral acceptance must remain independently
reviewable.

## Stop conditions

Stop and report instead of improvising if:

- D01 cannot be reproduced or its fix would require a global loading mask;
- D05 appears to require component-specific preview branches;
- D04 requires new persistence semantics or a data migration;
- D03/D02 would place editor-specific construction in `MainWindow`;
- P01 requires altering transport behavior, units or visual grouping;
- token changes cannot meet contrast goals without materially changing the visual
  identity;
- any phase unexpectedly modifies the committed desktop database or parity assets.

## Completion criteria

The implementation series is complete only when all seven phases have an accepted
result, every commit is independently reviewable, the full validation path passes,
the architecture boundaries remain green, and the audit decision table can link to
the corresponding implementation commit(s).
