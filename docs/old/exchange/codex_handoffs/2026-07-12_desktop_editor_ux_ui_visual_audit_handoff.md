# Desktop Editor UX/UI Visual Audit Handoff

Date: 2026-07-12

## Objective

Audit the desktop editor from the user's point of view before beginning the
animation phase. Extend the completed consistency audit with a practical review
of mental models, metaphors, discoverability, hierarchy, interaction flow and
visual clarity.

This is a diagnostic and design-proposal phase. Do not modify production code,
seed data, the committed desktop database or architecture checks. Produce an
evidence-backed report and concrete visual proposals that can be reviewed before
any implementation is authorized.

## Starting point

- Start from `main` at commit `60843760` or a later explicitly confirmed commit.
- Run `git pull` and restart the desktop application before testing, so cached
  layouts and preview processes do not distort the findings.
- Preserve unrelated local files and changes. Temporary audit data must be kept
  outside committed production data and removed when no longer needed.

## Required reading

Read these documents completely before beginning:

- `AGENTS.md`
- `docs/architecture/editor_shell_non_negotiables.md`
- `docs/architecture/editor_modernization_roadmap.md`
- `docs/architecture/24_desktop_preview_component_architecture.md`
- `docs/architecture/25_component_migration_status.md`
- `docs/architecture/26_desktop_preview_pipeline.md`
- `docs/architecture/27_time_units_contract.md`
- `docs/architecture/27_design_production_ux_separation.md`
- `docs/architecture/28_static_preview_update_experience.md`
- `docs/exchange/codex_handoffs/2026-07-12_editor_ui_inheritance_consistency_audit_handoff.md`
- `docs/audits/2026-07-12_editor_ui_inheritance_consistency_phase_a.md`
- `docs/audits/2026-07-12_editor_ownership_tokens_preview_parity_phase_b.md`
- `docs/audits/2026-07-12_editor_robustness_enforcement_phase_c.md`

The earlier audit established technical consistency and ownership. Do not repeat
it mechanically. Use it as the verified baseline and concentrate on whether a
user can understand and operate the resulting product.

## Product principles to preserve

- Design and Production are separate workspaces with shared infrastructure but
  different context and authority.
- Design inspects classes, variants, modules and Test Values.
- Production operates real Shots, Screens and their runtime payloads. Avoid
  technical terms such as `ModuleInstance` in user-facing language.
- The editor is metadata-driven. A visual recommendation must not require local
  one-off controls or component-specific shell behavior.
- `MainWindow` remains shell-only and component behavior stays behind its owning
  resolver/contract.
- Existing UI metaphors should be improved consistently, not replaced in one
  isolated editor.
- Visual proposals are proposals, not new canonical rules, until reviewed.

## Core audit questions

For every important workflow ask:

1. Does the user know where they are and what object they are editing?
2. Is it clear which values are inherited, overridden, runtime, calculated or
   saved as defaults?
3. Is the next useful action visible and does its icon/metaphor predict the
   result?
4. Can the user distinguish navigation from editing, selection from activation,
   and temporary Test Values from persisted configuration?
5. Does the interface reveal complexity progressively, or expose internal
   structure before it is useful?
6. Are success, disabled, empty, loading, changed, error and playback states
   unmistakable?
7. Can a first-time user recover from a wrong turn without knowing the data
   model?
8. Does repeated use remain fast and economical for an expert?

## Workflows to inspect

### Design workspace

- Navigate from the tree to an atom, component and module.
- Select and create variants/presets.
- Understand the editor breadcrumb and recent-context selector.
- Distinguish the parent-preset edit icon from local override `...` editing.
- Enter and leave nested embedded component editors.
- Read inherited and amber override states; reset/inherit values.
- Edit collections, select an item, insert, reorder and delete it.
- Pick icons, media, fonts, devices, component presets and Theme references.
- Use Runtime Inputs, Test Values, Save as defaults and declared actions.
- Use Fit, markers, canonical width and reference comparison controls.
- Understand preview feedback, errors and transient playback.

### Production workspace

- Navigate Production → Episode → Shot → Screen.
- Select the active Screen and understand its context within the Shot.
- Edit runtime values, repeated messages and attachments.
- Use Shot/Screen context switching, slider and transport controls.
- Distinguish previous/next frame, Screen boundary and Shot boundary actions.
- Understand inherited Device, Theme and display mode without Design selectors.
- Verify that labels use the user-facing Screen vocabulary.
- Recover from empty Shots, missing media, disabled actions and preview errors.

### Cross-cutting editors and dialogs

- Theme, Device, Actor, Icon Set, Font and media workflows.
- Search, selection and cancellation in modal pickers.
- Creation, deletion, confirmation and protected/default records.
- Navigation history and returning to the previous editing context.
- Resizing the three-panel layout and working at realistic window widths.

## Visual and interaction review

Evaluate at minimum:

- information hierarchy and progressive disclosure;
- card nesting, exclusive siblings, separators, borders and elevation;
- typography hierarchy, label clarity and terminology;
- spacing, alignment, density and scan paths;
- icon consistency, familiar metaphors, tooltips and hit areas;
- affordances for selectors, expandable areas, actions and navigation;
- active, selected, hover, focus, disabled, dirty and inherited states;
- contrast and readability in both desktop Light and Dark modes;
- empty, loading, error and no-results states;
- modal focus, keyboard navigation, Escape/cancel and prevention of traps;
- long values, narrow panels, wrapping, truncation and scroll behavior;
- whether color is being asked to communicate too many unrelated meanings.

Explicitly review the current metaphors for:

- edit-parent icon versus `...` local overrides;
- amber override indication and reset/inherit;
- `+`, lock, history/context dropdown and external navigation;
- cards and subordinate cards;
- Browse, Source and Pick actions;
- Test Values versus Save as defaults;
- Design versus Production;
- Shot versus Screen transport and context;
- play/action buttons versus persistent switches.

## Evidence requirements

Each finding must include:

- workflow and exact reproduction path;
- current screenshot or tightly scoped crop;
- user expectation and observed behavior;
- why the issue matters to comprehension, confidence or speed;
- affected user type: new, occasional, expert or all;
- severity and frequency;
- concrete recommendation;
- acceptance criteria that can later be tested;
- whether it is a quick visual correction, shared-system change or product
  decision.

Do not classify personal taste as a defect. Separate:

- verified usability problem;
- consistency problem;
- reasonable improvement;
- exploratory alternative requiring product judgment.

Use severity levels:

- P0: prevents or corrupts a critical workflow;
- P1: likely user error, lost work, inaccessible action or unusable workflow;
- P2: recurring confusion, misleading metaphor or substantial inefficiency;
- P3: polish, density, wording or low-risk visual inconsistency.

## Concrete visual proposals

The audit must go beyond prose where a visual change is proposed.

For every P1 and material P2 finding, provide one of:

- an annotated current screenshot;
- a before/after composition;
- a low- or medium-fidelity wireframe;
- a compact interaction/state sequence.

Save supporting assets under:

`docs/audits/assets/2026-07-12_desktop_editor_ux_ui_visual_audit/`

Use the editor's existing visual language and actual content whenever possible.
Do not invent a wholesale redesign unless the report first demonstrates why the
current model cannot be repaired. Label exploratory mockups clearly and identify
which shared UI primitive or metadata concept they would affect.

Visual proposals should show, where relevant:

- normal, selected, hover/focus, disabled and error states;
- narrow and normal panel widths;
- both Design and Production contexts;
- enough surrounding UI to understand hierarchy, not just an isolated control.

## Deliverable

Create:

`docs/audits/2026-07-12_desktop_editor_ux_ui_visual_audit.md`

The report must contain:

1. executive summary;
2. user mental model and principal workflows;
3. tested surface/journey matrix;
4. strengths worth preserving;
5. findings ordered by severity;
6. annotated evidence and visual proposals;
7. terminology and metaphor review;
8. prioritized recommendations in three horizons:
   - quick wins;
   - shared structural improvements;
   - future explorations;
9. dependencies and risks for implementation;
10. explicit list of inspected areas with no finding;
11. a proposed implementation sequence, but no implementation.

Finish with a short decision table so findings can be accepted, rejected,
deferred or sent back for another visual iteration independently.

## Working rules

- The audit may run and operate the desktop app directly.
- Temporary local test data is allowed only if it cannot affect committed data;
  document it and clean it up.
- Do not edit production source, seeds, `data/desktop-editor-spike.sqlite` or
  architecture tests.
- Do not add automated checks during this phase. Recommend them in the report
  when a durable rule is identified.
- Commit only the report and its visual evidence after the user reviews the
  audit, unless the user explicitly authorizes an earlier documentation commit.
- Stop and report separately if a P0/P1 functional defect blocks the audit; do
  not silently fix it.

## Suggested execution order

1. Build a journey and screen inventory from the running product.
2. Perform the Design walkthrough and collect evidence.
3. Perform the Production walkthrough and collect evidence.
4. Review cross-cutting dialogs, pickers, states and resizing.
5. Cluster findings by mental model rather than by source file.
6. Produce concrete visual alternatives for the highest-value findings.
7. Deliver the report for discussion before any code changes.
