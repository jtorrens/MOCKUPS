# MOCKUPS Desktop Editor: Active Architecture Handoff

## Purpose

This document is the self-contained continuation context for a new development
thread. It describes the current desktop editor architecture, the design
philosophy behind it, current implementation status, known defects and the
recommended roadmap.

It intentionally does **not** restate every historical decision. Older React,
Visual IR and prototype discussions are useful reference material only. The
current desktop editor has a different active execution model and must not be
pulled back toward those older routes.

## Read First

Before changing code, read these files in this order:

1. `AGENTS.md`
2. `docs/architecture/README.md`
3. `docs/architecture/editor_shell_non_negotiables.md`
4. `docs/architecture/23_embedded_component_composition_contract.md`
5. `docs/architecture/24_desktop_preview_component_architecture.md`
6. `docs/architecture/25_component_migration_status.md`
7. `docs/architecture/26_shot_module_instance_contract.md`
8. `docs/architecture/27_design_production_ux_separation.md`
9. This handoff

`README.md` is the active architecture index. If a historical document
conflicts with a document named there, the indexed active document wins.

## Repository State at Handoff

- Repository: `/Volumes/SD_02/PROYECTOS/MOCKUPS`
- Active branch: `main`
- Recent committed editor milestones:
  - `33723022 feat: centralize runtime input test values`
  - `29abc314 refactor: group runtime inputs by embedded component`
  - `cea58fbb feat: add runtime input and usage editor cards`
  - `3414b74b refactor: remove duplicated preview inputs surface`
  - `b40475b4 refactor: isolate preview input session`
  - `7f319fca refactor: use standard cards for conversation messages`
  - `c5cc7ff3 feat: use viewport catalog for device imports`
- Desktop window version at that point: `v0.317.12`
- The branch is ahead of `origin/main`; do not assume it has already been
  pushed.

### Important dirty-worktree warning

There are deliberate local changes that must be inspected and preserved before
making a new commit. They include:

- `data/desktop-editor-spike.sqlite`: current shared test/parity data.
- `artifacts/` and `data/window-state.json` are local generated/state files;
  they must not be committed by default.

Do not revert or overwrite any of these changes. Read and integrate them with
the active work before staging.

## Collaboration Rule

The user uses questions to discuss and refine concepts. A question is **not**
permission to inspect broadly, edit files or begin a new phase. Answer and
align first. Implement only after an explicit instruction such as
`implement`, `start`, `continue`, `apply` or equivalent.

When implementation is underway:

- provide short progress updates;
- increment the app window version for visible UI changes;
- use intermediate commits at meaningful, independently valid boundaries;
- state what the user can verify in the UI after a phase;
- do not stop running processes required for the requested work;
- do not commit unrelated dirty-worktree files.

## Product Philosophy

MOCKUPS is a production-oriented phone mockup system. It is not a generic
diagram editor and it is not a React renderer port.

The durable model is:

```text
Design system
  reusable component classes and variants
  reusable modules and app-level configuration
        ↓
Production
  project → episode → shot → ordered module instances
        ↓
Resolved web preview/render frame
```

The editor must let a designer work at the correct level:

- **Design** defines reusable visual behavior.
- **Production** chooses reusable design outputs and supplies concrete runtime
  content for a shot.
- Production must not silently mutate global reusable design.
- A reusable design change from Production should always be an explicit
  `Open in Design` action in a future refinement.

The preview is not an approximation drawn by Avalonia. The final truth is the
web renderer. Avalonia hosts the editor and displays the resolved web preview.

## Current Data Model

### Production hierarchy

```text
Project
  → Episode
    → Shot
      → ordered ModuleInstance slots
```

There is deliberately no separate Screen Instance entity. A Shot is one
continuous phone action. Its ordered module instances are the visible states
inside that action.

`ModuleInstance` owns:

- its module reference;
- concrete content JSON;
- per-instance behavior JSON;
- animation/keyframe JSON reserved for future work;
- duration and transition declaration;
- order within the Shot.

`ModuleInstance` does **not** own device, theme, actor or base FPS. Those are
resolved from the Shot and its owner actor. Project FPS is inherited; a future
shot override is allowed.

### Device Importer

The Devices-root `Add` action is routed through `EditorAddChildWorkflow`. The
external catalog is behind `IDeviceCatalogProvider`; the dialog and data mapper
do not know the concrete source. The active provider is
`LabsViewportsDeviceCatalogProvider`, which reads the MIT-licensed public
labs-viewports catalog.

The import pipeline is:

```text
catalog provider
→ DeviceCatalogDetails
→ DeviceImportMapper
→ DeviceImportDraft
→ SpikeDatabase.AddImportedDevice
```

The provider supplies both dimensions explicitly:

- `DesignWidth` / `DesignHeight`: browser viewport/design-space dimensions;
- `RenderWidth` / `RenderHeight`: physical screen/render dimensions.

`DeviceMetricRules.CreateMetricsJson` has an overload that accepts those four
dimensions and rejects non-uniform mappings. Do not revive heuristic
scale-only import data when an external catalog provides explicit viewports.
If a provider changes again, replace only the implementation of
`IDeviceCatalogProvider`; do not move source/API logic into the dialog, mapper,
database or `MainWindow`.

### Component classes and variants

A component class owns:

- schema identity and resolver identity;
- its reusable base configuration;
- a protected `Default` variant;
- a list of named variants.

A variant is a stored configuration snapshot. It is a conscious design choice,
not a computed inheritance coincidence. If a local override happens to equal a
later parent value, it remains an override until the user explicitly restores
it.

Component references always use a full concrete variant reference:

```text
componentClassId::preset::presetId
```

The word `preset` remains in persisted IDs for backward compatibility. The UI
and conceptual language use **variant**. Do not reintroduce short ambiguous
variant IDs except as migration input.

### Recursive embedded components

Components can embed other components recursively. Example:

```text
Conversation module
  → Bubble variant
    → Text Box variant
      → Surface / Cursor / Icon Row variants
    → Avatar variant
      → Label variant
```

An embedded component slot stores:

- the selected concrete child variant reference;
- explicit overrides for the child instance, if any;
- declared runtime inputs propagated from the child when applicable.

The parent component may import and compose its direct embedded children. The
generic bridge, common helpers and web renderer must never import components by
name or infer composition rules.

## Values and Input Semantics

There are three different origins. Do not collapse them:

| Origin | Meaning | Editor visibility |
|---|---|---|
| `Variant` | Stored as part of the current reusable component/module variant. | Visible in the normal editor. |
| `Runtime` | Supplied later by a screen/module instance or preview test data. | Public input contract and Test Values. |
| `Calculated` | Derived by the owning resolver. | Not editable. |

Runtime inputs propagate recursively through embedded components until a parent
intentionally converts one into its own variant value. Runtime inputs are not
preview-only concepts. The isolated preview merely supplies sample values using
the same contract that a production module instance will use later.

### Runtime Inputs / Test Values

Each component variant and module has a normal editor card with two tabs:

1. **Runtime API**: technical contract, grouped by own versus embedded origin.
2. **Test Values**: the same controls, grouped recursively by embedded
   component, used to drive the isolated web preview.

The current Test Values are stored in `design_preview_json.testValues`.
Ordinary test-value edits do not mutate variant config or production data.
`Save as defaults` is an explicit promotion command: it writes the current
test values into runtime defaults. It does not modify production data.

The old visual Inputs panel was removed from the preview pane. The internal
`ComponentPreviewInputSession` is now only a non-visual state/playback session
used to merge input values and request frame updates.

## Preview and Renderer Architecture

The active route is:

```text
Component or module config + runtime values + requested frame
  → owning resolver
  → owning component/module renderable module
  → common preview helpers
  → generic web renderer
```

### Resolver responsibility

The resolver owns component semantics:

- composition of direct embedded children;
- component-specific layout and z-order;
- variant config interpretation;
- state at the requested frame;
- animation state interpolation before the frame is passed on;
- selection of generic atoms such as surface, text, SVG, image, placement,
  shadow, relief and debug bounds.

Animation is frame data. The resolver supplies the fully resolved current
frame. The renderer must not run component timers, CSS animations or
component-specific interpolation.

### Bridge/common responsibility

Common preview code may only perform generic transformations:

- resolve theme token/palette colors for the selected mode;
- apply palette alpha and neutral tint;
- map design units to final device pixels;
- create generic boxes, surfaces, text, SVG, images, shadows and placement;
- validate and report unresolved values.

The bridge must not contain `if componentType == ...` layout or style branches.
If that seems necessary, move the rule to the owning resolver or parameterize a
generic helper.

### Web renderer responsibility

The web renderer paints resolved primitive nodes only. It must not know:

- SQLite or database records;
- component class names or variant names;
- inheritance;
- component defaults;
- theme-token names;
- palette-token names;
- per-component layout/business rules.

The active architecture checker enforces these boundaries:

```bash
npm run check:architecture
```

Run it before closing every preview/component phase.

### Color modes

The design payload carries resolved color values for all named modes, not only
hardcoded `light` and `dark`. This keeps future alternate modes possible
without redesigning the payload. Geometry remains resolved separately.

### Web preview controls

The design preview pane owns only unresolved external context:

- Device
- Theme
- Mode
- Orientation
- scale (`Fit`, `1:1`, `2:1`, `3:1`, `4:1`)
- debug marks

The default preview tab is Design. Marks are off by default. Context can be
locked so an embedded component can be edited while remaining visible in its
parent context. History of deliberate design contexts is persisted separately.

## Editor Shell Rules

### MainWindow is shell-only

`spikes/desktop-editor-shell/MainWindow.axaml.cs` may coordinate shell layout,
selection, preview wiring and modal hosting. It must never accumulate
table-specific controls, dialogs, pickers, SVG/media/font behavior or business
rules. Extract editor-specific behavior into an editor class; extract reusable
behavior into common/shared code.

### Dictionary-only scalar fields

Every editable scalar field follows:

```text
editor layout metadata
→ FieldDefinition
→ ValueKind
→ DictionaryFieldControl / registered dictionary control
→ generic commit path
→ database
```

Do not create raw editor TextBox, ComboBox, CheckBox, numeric picker or
domain-specific picker for an editable scalar value. Extend `ValueKind` and the
dictionary control route first.

### Common UI only

Unless explicitly requested otherwise, use shared cards, controls, density
helpers and layout helpers. Do not add one-off expanders, local card chrome or
local scroll fixes. New common visual behavior should be extracted first.

### Card/Tree language

- Editor groups use the shared standard card system.
- Groups with a single conceptual section do not add a second decorative card
  background/elevation.
- Embedded runtime-input groups are recursive standard expandable cards and
  are exclusive among siblings.
- Usage is different: it is a classic collapsible tree **inside** one standard
  Usage card. Branches use chevrons; leaves use explicit icon actions, not
  branch chevrons.
- Expand/collapse timing is intentionally fast. No slow Suki reveal effects.

### Spacing and numeric rules

- Visual padding and gap always use `theme.spacing.*` tokens.
- Raw numeric padding fields are not allowed for component/editor visual
  spacing.
- For X/Y spacing use a spacing-token pair.
- Slider steps snap to their declared step; text input can hold an intermediate
  precise value.

### Variants and protection

- Every component class has a protected `Default` variant.
- Classes are internal schemas, not user-duplicable objects. A class can be
  renamed for identification, but its raw configuration is not directly
  editable.
- Variants can be locked. A locked variant can be duplicated or renamed but
  cannot be changed or deleted. Default is protected.
- Variant history stores recent session snapshots. It is not a persistent
  version-control system.
- Future requested feature: add `Promote variant to Default` next to Save
  variant, with confirmation. It copies variant config into Default but does
  not delete the source variant.

## Current Implemented Components

The current component layer is structurally migrated and rendered through the
resolver → renderable → generic web route:

### Atoms

- `surface`
- `cursor`
- `label`
- `avatar`
- `button icon`
- `icon row`
- `icon bar`

### System and composition components

- `status bar`
- `navigation bar`
- `keyboard`
- `text box`
- `text input bar`
- `media` (image/video capable)
- `audio`
- `bubble`

### Component capabilities already present

- recursive embedded component variants and instance overrides;
- generic placement/alignment object with edge/center semantics and offsets;
- surfaces with border, shadow, relief and optional tail;
- typography type with family, weight, style, size and line height;
- color/palette pair and alpha support;
- icon SVG replacement/editing and per-icon action overrides;
- status/nav system variants assigned from themes;
- component design inputs and generic preview actions;
- motion tokens with translate/scale/fade combinations and play-once preview;
- media normal/fullframe states and icon-bar zones;
- text box growth/wrap/scroll modes;
- Bubble incoming/system/outgoing tails, write-on, optional actor label/avatar,
  media, status text/icons and state-specific palette colors.

### Functional caveat

Some components are structurally complete but will need richer behavior when
real production modules demand it. Do not add speculative fields merely
because a generic UI control exists. Add fields through the component contract
when a concrete module use case requires them.

## Current Modules and Production

### Conversation module

Conversation is the first active module. Its reusable module config selects
variants for:

- Status Bar
- Navigation Bar
- Keyboard
- Text Input Bar
- Bubble
- Header Avatar

The module itself composes header/wallpaper/frame behavior. It is allowed to
use direct generic atoms for its own layout; it should reuse component variants
for reusable visual pieces.

### Conversation module instance

A Conversation module instance persists concrete ordered messages in
`module_instances.content_json`. Each message currently has:

- type;
- direction (`incoming`, `outgoing`, `system`);
- actor reference;
- text;
- delay after previous message in frames;
- write-on duration in frames;
- status text;
- delivery status.

Conversation durations will ultimately be derived from the complete sequence:

```text
head frames
+ delays
+ write-on / later message events
+ tail frames
```

The initial editor has add/delete/edit messages. A local uncommitted change is
currently converting each message row into a shared collapsible exclusive card.
Review and validate that change before committing it.

### Shot module slots

The Shot editor already lists ordered module-instance slots, allows adding
Conversation instances, deleting/reordering/opening them, and stores an
initial `{ "type": "cut" }` transition declaration.

Future non-cut transitions should be derived using the old React timeline only
as behavioral reference. They must overlap resolved module-instance frames;
they must not create another screen-instance data layer.

## Design/Production UX Status

Implemented and validated:

1. Root Design / Production workspace switch.
2. Separate navigation scope for each workspace.
3. Production project selector and episode/shot/module navigation.
4. Runtime Inputs / Test Values editor card.
5. Test value persistence and explicit `Save as defaults` promotion.
6. Preview no longer contains a duplicated Inputs editor.
7. Full Usage card with Design/Production branches and navigation leaves.

Usage details currently work as follows:

- Component variants show direct embedded-component usage, Theme usage where
  applicable, and module configuration usage.
- A Bubble variant selected by Conversation is expected to show Conversation
  as Design usage.
- Modules show Module Instance references as Production usage.
- Category branches use folder icons and connector lines; a leaf has its own
  type icon plus a right-side edit/open icon. Only the icon is clickable.

## Known Issues and Deliberate Deferrals

### Must be fixed in a focused pass

1. **Preview message text mismatch**
   - Test Values can contain more text/emoji than the web preview displays.
   - It is most visible in final wrapped text/emoji segments.
   - Do not hide it with fallbacks or truncation.
   - Audit generic text measurement, font loading, emoji/grapheme segmentation,
     wrapping and clipping together. Bubble must not receive a local exception.

2. **Scroll after expanding nested cards**
   - `DeferredBringIntoView` was generalized, but long nested Runtime Input
     cards can still fail to reveal their final content.
   - Treat it as one scroll-host/layout lifecycle problem. Do not add editor-
     specific offsets or scroll hacks.
   - Inspect actual visual ancestry and the appropriate scroll host after the
     layout has settled; ensure full card bounds are visible when viewport size
     permits it.

3. **Conversation message card change**
   - There is an uncommitted conversion from local bordered rows to standard
     exclusive cards.
   - Validate message deletion, editing, scroll behavior and card state before
     committing. It was interrupted before user review.

### Deliberate deferrals

- Native macOS emoji-keyboard insertion in Avalonia text input is not yet
  reliable. Pasted emoji render. Keep this as a separate platform-input issue.
- Web font family switching has been made functional with caching, but font and
  emoji loading should be audited later for final render parity.
- Video preview buffering/preload works sufficiently for design inspection but
  still needs a dedicated performance pass before real-time shot preview.
- Transition overlap/timeline mechanics are intentionally not implemented
  beyond cut slots.
- Screen Module remains intentionally outside current scope because it carries
  shot-specific payload/animation semantics.
- The old React source is archived visual/behavioral reference, never an
  active implementation dependency.

## Recommended Roadmap

The following order minimizes risk and avoids reintroducing legacy routes.

### Phase 0: Stabilize current dirty state

1. Inspect the parallel Device Importer changes and ensure they remain behind
   `IDeviceCatalogProvider` / mapper / database boundaries.
2. Validate or discard the local Conversation message-card UI change.
3. Commit only coherent scopes. Include DB/assets only when they are intentional
   parity changes. Never include `artifacts/` or `data/window-state.json`.
4. Push `main` after each completed validated scope.

### Phase 1: Close current UX defects

1. Fix generic nested-card scroll reveal.
2. Fix generic text measurement/wrap/clip parity for emoji and final lines.
3. Re-test Runtime Inputs/Test Values, Bubble, Text Box and Conversation after
   both fixes because they share the path.
4. Commit as a UI/runtime-preview stability phase.

### Phase 2: Finish Conversation production authoring

1. Normalize Conversation message editor cards using common UI.
2. Add remaining concrete message properties only as actual production needs
   demand: media reference, actor, delivery changes, timing/event sequence.
3. Move duration computation from manual instance duration toward the declared
   sequence of head/delay/events/tail frames.
4. Keep action/event animation resolver-owned and frame-addressable.
5. Add a Shot-local frame navigator for concrete module-instance preview.

### Phase 3: Production preview resolution

1. Resolve shot owner actor → device/theme context.
2. Resolve a requested shot frame into active ordered module instances.
3. For cut transitions, show exactly one active module frame.
4. Feed real module-instance runtime inputs through the existing contracts.
5. Use the same web renderer as component design preview; never make an
   Avalonia approximation.

### Phase 4: Timeline transitions

1. Extend transition JSON with type/duration.
2. Compute overlap between adjacent module slots.
3. Ask each active module resolver for the requested frame.
4. Emit generic resolved transition atoms to the web renderer.
5. Start with crossfade/slide only after cut behavior is stable.

### Phase 5: Component behavior expansion only from real needs

Potential work, gated by actual Conversation/other-module requirements:

- richer natural write-on plans: pauses, hesitation, grouped graphemes;
- media/image/video variants and their specific icon bars;
- keyboard interaction content generated from full text/current grapheme;
- text input behaviors beyond current layout foundation;
- additional module types built from the same runtime contract.

### Phase 6: Schema freeze and final consolidation

When the current active model is stable:

1. create a clean schema-v1 baseline DB from the current model;
2. migrate current valid data once into it;
3. isolate old migration/normalization code outside active startup paths;
4. remove development-only fallbacks that can hide malformed data;
5. keep explicit diagnostic colors/messages rather than plausible fallbacks;
6. validate Mac/PC parity with the documented process.

Do not start Phase 6 while component/module contracts are still actively
changing.

## Development and Validation

### Required checks

Run before completing a preview/component/editor phase:

```bash
npm test
```

This currently runs:

- desktop preview bundle build;
- TypeScript typecheck;
- architecture boundary check;
- Avalonia desktop build.

The build may report the known `SQLitePCLRaw.lib.e_sqlite3` advisory. Record it
but do not confuse it with a functional test failure.

### Manual UI checks by phase

- Component variant: select/edit/restore/lock; embedded child navigation;
  test preview context lock.
- Runtime inputs: own and nested groups; Test Values persistence; action
  buttons; `Save as defaults`.
- Usage: both branches; folder collapse; leaf icon navigation; Bubble →
  Conversation and Conversation → module instances.
- Conversation instance: add/delete/reorder messages, frame values, actor
  selections and preview content.
- Preview: device/theme/mode/orientation, Fit/1:1 scale, marks, context lock,
  video/media frame updates and text/emoji rendering.

### Commit discipline

- Keep commits narrowly coherent and meaningful.
- Include `data/desktop-editor-spike.sqlite`, `assets/FOQN_S2` and
  `assets/system/system_icons` whenever behavior/seeds/assets intentionally
  require parity.
- Do not stage user-local generated state.
- Never use destructive git reset/checkouts to clean a shared dirty worktree.

## Historical Reference

Use `/Volumes/SD_02/PROYECTOS/MOCKUPS_REACT` and
`archive/react-legacy/` only when a concrete visual or behavioral question
requires comparison. Typical valid uses:

- exact legacy text wrap/write-on behavior;
- media/fullscreen interaction reference;
- keyboard emoji placement behavior;
- shot transition/timeline algorithm reference.

Before porting any idea, translate it to the current contracts. Do not port a
React renderer, React state model or legacy bridge route directly.

## Definition of a Good Future Change

A change is aligned when it is possible to describe it as:

```text
new/updated reusable component or module contract
→ owner resolver emits resolved generic atoms for a frame
→ generic web renderer paints those atoms
→ editor exposes scalar values through FieldDefinition/ValueKind
→ production supplies concrete runtime inputs without redefining design
```

If it instead requires a component-name branch in the bridge/renderer, a raw
editor control, a MainWindow special case, a hidden fallback or a second
preview-only input model, stop and redesign the boundary first.
