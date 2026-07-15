# MOCKUPS agent working rules

Before changing the Avalonia/Suki desktop editor spike, read and follow:

- `docs/architecture/editor_shell_non_negotiables.md`
- `docs/architecture/editor_modernization_roadmap.md`
- `docs/architecture/24_desktop_preview_component_architecture.md`
- `docs/architecture/25_component_migration_status.md`

## Hard rule: `MainWindow` is shell-only

`spikes/desktop-editor-shell/MainWindow.axaml.cs` must not contain editor-specific implementation.

It may contain only shell/orchestration responsibilities:

- window initialization;
- three-panel composition;
- selected tree node state;
- navigation tree refresh/selection wiring;
- editor card composition from generic layout metadata;
- preview panel wiring;
- generic modal hosting/delegation;
- persisted window/panel visual state.

It must not contain:

- editor-specific field construction;
- editor-specific collection rows;
- table-specific business rules;
- domain-specific pickers or dialogs;
- SVG/icon/media/font/palette logic specific to one editor;
- one-off layout fixes for a specific editor.

If an editor needs special behavior, put it in that editor's own class. If the behavior can be reused, extract it to a shared editor-shell class. `MainWindow` should only instantiate or delegate.

## Hard rule: check common before adding helpers

Before creating any routine that could be generic, check `spikes/desktop-editor-shell/Common` and the base-routines audit for an existing equivalent.

If an analogous helper already exists, reuse or extend it there. If the new behavior is reusable by more than one editor, resolver, bridge, renderer, importer, or repository, put it in common/shared code first instead of adding a local private helper.

## Hard rule: commit parity data and assets

When a change affects desktop editor behavior, preview output, icons, fonts, media references, or seeded component/theme data, include the corresponding parity files in the same commit:

- `data/desktop-editor-spike.sqlite`;
- changed files under `assets/FOQN_S2`;
- changed files under `assets/system/system_icons`.

Do not leave the desktop DB or required assets as local-only changes when the user asks for a working branch/push.

## Hard rule: editable fields go through the dictionary

Every editable scalar field must be defined by `FieldDefinition` and rendered through the dictionary/control path.

The expected route is:

```text
editor layout metadata
→ FieldDefinition
→ ValueKind
→ DictionaryFieldControl / registered dictionary control
→ generic commit path
→ repository/database
```

Do not create raw `TextBox`, `ComboBox`, `CheckBox`, numeric inputs, color pickers, font pickers, or icon pickers inside an editor for a value that should be a dictionary field.

If a needed control does not exist, add or extend the dictionary value kind/control first.

Collection editors are allowed for structured lists, but simple fields inside those collections must still use dictionary definitions and dictionary controls.

## Hard rule: use common UI surfaces

Unless the user explicitly asks for a special treatment, new editor UI must use
the existing shared cards, controls and layout helpers. Do not introduce local
expanders, custom card chrome or one-off controls when an equivalent common
surface exists. Extract a shared control before adding a reusable visual pattern.

## Hard rule: editor organization is metadata-driven and session-only

Reusable editor organization must be declared through shared layout metadata,
never inferred from hierarchy depth, record class, card label or a concrete
editor. Use the established shared presentations:

- `flatStack` for repeated siblings that inherit the parent surface and use
  separators instead of nested elevation;
- `verticalCards` for vertical internal navigation with one selected child
  content surface;
- `separatedSections` for continuous field content divided by labelled rules;
- per-group `presentation` when one card intentionally mixes organizations;
- `pairLayout: sharedHeader` for groups of compound Light/Dark dictionary
  values that share one column header.

Dictionary controls own compound-control visuals. In particular,
`PaletteColorPair` owns its two-column Light/Dark layout, compact sizing,
ellipsis and border treatment; an editor must not restyle individual rows.

Editor card expansion, internal selection and editor scroll position are
session-only state. A new application session starts with every editor card
closed. This state must not be written to `data/window-state.json`.

## Hard rule: padding uses spacing tokens

Padding and gap fields must use `theme.spacing.*` tokens. Do not add raw numeric padding fields for component/editor values that represent visual spacing. For X/Y spacing, use a spacing-token pair.

## Hard rule: no component-specific knowledge across preview boundaries

Component-specific decisions must stay inside that component's resolver/contract.

The bridge may only translate standard resolved atoms into final preview values:

- theme tokens, palette colors, alpha and neutral tint resolution;
- device/design units to final pixels;
- generic placement, boxes, text, images, SVGs, surfaces and shadows;
- generic validation/error reporting for unresolved values.

The bridge must not contain branches or layout rules for a specific component class such as label, avatar, button icon, audio, video, bubble, status bar, or navigation bar. If a component needs custom composition, create or extend that component resolver so it emits the standard atoms the bridge already understands.

There must not be a central preview bridge that grows component-specific functions or rules. Component classes and system bars use their own resolver/renderable modules and are selected only through an explicit registry. Registries may name components only to route to their owning module; they must not contain component layout, style, defaults, token resolution, or renderable construction logic. As components are migrated, remove central bridge code by moving component composition into component resolver/renderable modules and passing only standard atoms through generic helpers.

Each migrated component must keep this shape:

```text
component contract/resolver
→ component renderable module
→ common preview helpers
→ generic web renderer
```

Common preview helpers must not import concrete component resolvers/renderables or contain concrete component names. Embedded component imports are allowed only when the parent component explicitly owns that child slot.

Run `npm run check:architecture` before closing any preview/component migration phase. The check must fail if component-specific names or imports leak into central preview files, common helpers, or undeclared component dependencies.

The web renderer is even stricter: it paints the final resolved nodes. It must not know inheritance, class config, component defaults, theme token names, palette tokens, database records, or per-component business/layout rules. If the renderer needs a new visual primitive, add a generic primitive and feed it fully resolved style/data.

Animation is also frame data. Resolvers own the component state for the requested frame, and the bridge may translate that resolved frame into final pixels. The web preview/render layer must not run its own timers, CSS animations, countdowns, or component-specific interpolation. For web preview, an animated component is just a succession of resolved frames.

## Hard rule: animation timing is contract-owned and generic

Persist parameter animation only as v2 `fieldId`/`targetId` keyframe tracks. Frame origins, completion dependencies, finite action durations, non-sequencing fields and retime must come from runtime contract metadata and the common owner timeline; editors must not reproduce those formulas.

Reusable behavioral timing uses the dictionary `BehaviorTiming` value kind. Fixed mode resolves authored frames. Natural mode resolves semantic units × the module-owned base rate × a `theme.motion.naturalPace.*` multiplier. The module resolver owns deterministic cadence inside that final duration; the bridge and renderer receive only the resolved frame state.

Animation editors show a Shot-wide authoring scale while persisted collection keyframes remain relative to their stable owner. Contract-declared base/finite durations use the shared reference-duration lane. Retime is off when `targetDurationFrames` is absent; provisional right-side authoring margin is session-only and must never be persisted as duration or window state.

Component inputs are runtime component inputs, not preview-only controls. The preview panel may provide sample values for isolated inspection, but screens/modules must later supply real values through the same declared input contract. Do not add component-specific input catalogs or animation behavior to the preview shell.

Component composition must reference concrete component presets, not parent component classes. Parent classes own schema, resolver identity and preset lists; reusable visual instances store full preset references in the form `componentClassId::preset::presetId`. Short preset ids are legacy migration input only. Saving a new preset must clone the active selected preset config, never ambiguous "current class values".

If a change appears to require `if componentType == ...` behavior in the bridge or renderer, stop and move that responsibility to the component resolver or to a parameterized common helper.

## Data migrations, not compatibility fallbacks

When a persisted schema, token vocabulary, contract field, or identifier changes,
make one explicit migration of the affected seeded data and committed desktop
database. Update every reference and parity artifact in the same change, then
remove the retired value. Do not retain aliases, silent coercions, or hidden
compatibility paths for old values. Any exception requires explicit user
direction.

A migration must be self-contained and temporary: convert the seed and committed
database, validate the resulting new contract, and remove the migration routine
in the same delivery. Normal startup, normalization, resolvers and editors must
know only the current schema. They must not keep reading, deleting, translating
or supplying defaults for retired fields after the committed data has migrated.

## When in doubt

Stop and extract. Do not add a local exception to make one editor work.

## Collaboration rule: questions are discussion, not execution

When the user asks a question, answer it and use the turn to refine the
concept, constraints or alternatives. Do not inspect broadly, edit files, run
implementation commands or start a new phase merely because an answer suggests
one. Begin implementation only when the user gives an explicit instruction to
execute, such as "implement", "start", "continue", "apply" or equivalent.

## Collaboration rule: confirm designs before implementation

When the user proposes, changes or discusses a design, data model, interaction
or behavior mechanism, do not implement it immediately. First return a brief,
concrete summary of how the proposal has been interpreted, including the
important ownership and behavior boundaries. Wait for the user's explicit
confirmation before editing files or running implementation commands, even when
the proposal sounds imperative. Once confirmed, execute the agreed design
without repeating the confirmation step for routine implementation details.

## Collaboration rule: serialize code-writing threads

This repository is normally maintained by one person, so only one thread may
modify tracked project code or parity data in the shared checkout at a time.
Before another code-writing thread starts, the active thread must:

- stop the desktop editor and any other process that can keep writing project
  files, especially `data/desktop-editor-spike.sqlite`;
- run the checks appropriate to its change;
- commit and push all intended project and parity changes;
- verify that the working tree is clean; and
- report the branch name and final commit so the next thread can verify and
  continue from that exact remote state.

The next code-writing thread must fetch the remote state, confirm the expected
branch/commit and a clean working tree before editing. Parallel threads are
allowed only for read-only investigation or work that is fully isolated from
tracked project files. If parallel code changes are explicitly required, each
thread must use its own worktree and branch and the changes must be integrated
and validated sequentially.

When a completed phase is intended to become the version used on other PCs,
integrate it into `main`, push `main`, switch the local checkout to `main` and
verify that local `main` and `origin/main` identify the same commit.

## Delivery rule: handoff checklist, current app and local commits

After every implemented update, the final handoff must include:

- a concise summary of what changed;
- a concrete list of manual checks for the user;
- confirmation that the latest validated build of the desktop app has been
  opened for review, or an explicit reason why it could not be opened.

When an implemented change is substantial enough to form a coherent revision,
prepare it as an actual local git commit after the relevant checks pass. Include
all required parity data and assets in that commit and verify the working tree is
clean. Do not push that commit until the user explicitly requests a push.
