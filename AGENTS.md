# MOCKUPS agent working rules

These rules apply to every task in this repository.

## Mandatory reading

Before changing the Avalonia/Suki desktop editor, read completely:

- `docs/README.md`
- `docs/architecture/README.md`
- `docs/architecture/system_overview.md`
- `docs/architecture/data_persistence.md`
- `docs/architecture/design_system.md`
- `docs/architecture/production.md`
- `docs/architecture/editor_dictionary.md`
- `docs/architecture/composition_runtime.md`
- `docs/architecture/animation.md`
- `docs/architecture/preview_rendering.md`
- `docs/architecture/resources_assets.md`
- `docs/architecture/ux_ui.md`
- `docs/architecture/development_workflow.md`
- `docs/architecture/validation.md`

Read the current manifest, schema, owner implementation and tests relevant to
the task.

## Hard rule: historical archive is prohibited

`docs/old` is a sealed historical archive. Agents and contributors must not
open, search, read, quote, summarize, cite or use any file under `docs/old` for
design, architecture, implementation, debugging, validation or product
decisions unless the user explicitly authorizes that exact historical
consultation.

A general request to inspect, audit, fix, continue or read documentation is not
authorization. If current documentation is incomplete, derive the answer from
current code, schema, manifest, committed database and tests, then update the
canonical active document.

## Hard rule: `MainWindow` is shell-only

`spikes/desktop-editor-shell/MainWindow.axaml.cs` owns only:

- window initialization;
- three-panel composition;
- selected tree state and navigation wiring;
- generic editor-card composition from layout metadata;
- Preview host wiring;
- generic modal hosting and delegation;
- persisted window and panel visual state.

It must not contain editor-specific fields, collections, business rules,
pickers, dialogs, assets, fonts, icons, palette logic or one-off layout fixes.
Place special behavior in its owning editor. Extract behavior shared by more
than one editor into a common shell service.

## Hard rule: one semantic owner

Before adding a helper, inspect `spikes/desktop-editor-shell/Common` and the
existing shared editor surfaces. Reuse or extend the current owner.

Ownership is:

- table SQL and row mapping: focused repository;
- document shape: current document contract;
- scalar editing: dictionary;
- structured items: collection owner;
- cross-domain reads: focused data source or document store;
- time and frames: common owner timeline;
- Component/Module behavior: its resolver and renderable;
- final painting: generic renderer;
- application layout: shell.

Do not add a local exception for behavior that can occur in another owner.

## Hard rule: persistence startup is read-only

Opening and validating an existing database must not change its file, schema or
data. Startup never runs creation, migration, repair, normalization, retirement
or duration synchronization.

A persistence change is one explicit maintenance workflow. Update schema,
seeds, every affected current row and reference, committed parity database and
assets; validate the result; remove temporary migration code in the same
revision.

Normal readers accept only the resulting current contract. Do not add aliases,
fallback fields, coercions or repair paths.

## Hard rule: repositories have narrow ownership

Use the shared SQLite context and focused repositories described in
`docs/architecture/data_persistence.md`. `SpikeDatabase` is an orchestration
facade. Add no new SQL, connection construction, table mapping or write
synchronization to it when an owning repository exists.

Repositories contain no UI, Variant selection, Runtime Input forwarding,
timing, context inference, Preview resolution or migration behavior.

## Hard rule: current JSON and Variants are strict

Every persisted JSON column consumes its declared current root kind. Blank,
malformed, absent or wrong-root required documents fail explicitly.

Component and Module Variant arrays are required. Every Variant is a complete
named snapshot with stable id, `protected`, `locked` and object `config`.
Readers never filter malformed entries or replace a missing Variant config.

Composition stores full Component Variant references:

```text
componentClassId::variant::variantId
```

Short Variant ids and class-only composition references are invalid.
`Preset` is a separate term used by Render Presets.

## Hard rule: definitions use complete development scaffolding

App and Module definitions expose Rename only. Creating, duplicating or deleting
an Atom, Component Class, App or Module is an explicit development workflow that
supplies its complete identity, manifest route, current row, Default Variant,
dictionary contract, resolver, renderable, editor metadata, fixture, migration
and validation.

Module Variants are authored data. They may be created by cloning the active
complete Variant, duplicated and renamed. Delete is allowed only when a Variant
is unused, unlocked and not protected. The protected Default Variant may be
renamed and cannot be deleted.

## Hard rule: editable fields go through the dictionary

Every editable scalar value follows:

```text
editor layout metadata
→ FieldDefinition
→ ValueKind
→ registered dictionary control
→ generic commit path
→ owning document or repository
```

Do not create raw text, numeric, option, boolean, color, font, icon or file
controls inside an editor for dictionary data. Add or extend the `ValueKind`
and registered control first.

Structured collection editors own list operations. Their scalar item fields
still use dictionary definitions and controls. Pair labels are explicit
metadata and must travel unchanged through Runtime Input and embedded binding
projections.

Padding and gaps use `theme.spacing.*` tokens. X/Y spacing uses a spacing-token
pair.

## Hard rule: use common UI surfaces

Use shared cards, controls, actions and layout helpers. Do not introduce a local
expander, card chrome, selector, icon or button when a common equivalent exists.

Forward uses the shared compact right-pointing indicator and standard
active/inactive semantics.

Reusable editor organization is metadata-driven:

- `flatStack`;
- `verticalCards`;
- `separatedSections`;
- per-group `presentation`;
- `pairLayout: sharedHeader`.

Do not infer presentation from hierarchy depth, record class, label or position.
Dictionary controls own their compound visuals.

Preview utility tabs remain one horizontal row at the supported 1040 px minimum
and 1440 px default window widths. The shell reserves the policy-owned Preview
minimum; Preview Setup reflows by measured width through the shared layout
policy. Do not encode the supported layout as an unchecked four-column XAML
assumption.

## Hard rule: editor view memory is session-only

Card expansion, internal selection and scroll are keyed by exact layout
`recordClassId` and explicit stable card/section ids. They persist while moving
between records and returning to an editor during the current session.

A new session starts with cards closed. Do not write this state to
`data/window-state.json`. Preview or Variant history must not override it.

## Hard rule: Component boundaries are explicit

Every embedded boundary stores an exact Variant and local Overrides.

- A fixed boundary shows Variant, class navigation and Overrides, never a
  Component selector.
- A polymorphic boundary shows Component selection only when its declared
  selector explicitly contains `*`.
- A new fixed boundary requires one exact class and crosses into its protected
  Default Variant.
- A new polymorphic boundary remains unselected until the user chooses a class,
  then crosses into that class's protected Default Variant.

Zero or multiple fixed matches fail. Never choose by first option, name, type,
order or position. Use the shared compact navigation and Overrides actions.

An editable Runtime Input that owns both a Variant and local Overrides uses the
exact `ComponentVariantSlot` value:

```json
{
  "variantReference": "componentClassId::variant::variantId",
  "overrides": {}
}
```

Do not store a string and manufacture Overrides later.

## Hard rule: Text Box Icon Rows preserve exact ownership

Text Box owns two complete Icon Row slots and their gap to text. Icon Row owns
ordered stable items, row gap, orientation and sizes. Each item stores a full
Button Variant reference and explicit local `buttonOverrides`.

Icon Row items are fixed Button boundaries and do not expose a Component
selector. Icon Row selection, items, gap and orientation are Variant data, not
Runtime/Test Values. Text Input Bar forwards only explicit runtime text. Bubble
and Text Input Bar customize Text Box through local Overrides.

## Hard rule: Runtime Inputs and forwarding are explicit

Runtime Inputs are product inputs. Design Test Values and Production Screen
payloads use the same declarations but have different owners.

Forwarding is declared by stable source and target ids before registry
dispatch. A registry, resolver, bridge or renderer never forwards or merges
values by matching names, types, shapes or positions.

At an embedded boundary, local `designPreviewJson` may change while the complete
`runtimeContractJson` temporal-owner envelope remains intact.

## Hard rule: animation timing is contract-owned

Persist parameter animation only as version 2 stable `fieldId`/`targetId`
tracks. Keyframes are relative to their temporal owner. Parent time owns
appearance, disappearance, activation and selection; an entity's own fields are
relative to its first appearance.

Moving or reordering recalculates effective frames without rewriting local
keyframes. Re-entry restarts parent-owned Enter/Exit Motion and does not restart
the entity's internal timeline. Never bind owners or tracks by index.

The common owner timeline owns frame origins, completion dependencies, finite
durations, non-sequencing fields, retime and conversion between Screen-local
authoring and the absolute Shot playhead.

Modules declare `calculated` or `explicit` Screen duration. Explicit duration
requires a positive default and is edited only on the instance. The authoring
`+` horizon is session-only.

Reusable timing uses `BehaviorTiming`. Fixed mode resolves frames. Natural mode
resolves semantic units × Module base rate ×
`theme.motion.naturalPace.*`. The owner resolves frame state; bridge and
renderer never animate independently.

## Hard rule: Preview boundaries stay generic

All Preview identities, categories, entrypoints and declared embedded
dependencies come from
`src/desktop-preview/desktopPreviewManifest.json`.

Registries route prepared payloads by exact stable id and do nothing else.
Payload preparation owns context and explicit forwarding before dispatch.

Each Component keeps:

```text
Component contract/resolver
→ Component renderable
→ common Preview helpers
→ generic web renderer
```

Common helpers do not import concrete Component owners or contain concrete
Component names. A parent imports a child only for a declared slot it owns.

The bridge translates only resolved generic tokens, units, placement, boxes,
text, images, SVGs, surfaces and shadows. The renderer paints final nodes and
knows no database, Variants, tokens, defaults, forwarding, timing or
Component-specific layout.

If a change appears to require `if componentType == ...` in a bridge, common
helper or renderer, move the rule to the owning resolver or a parameterized
generic primitive.

Run `npm run check:architecture` before closing a Preview or Component phase.

## Hard rule: Production context is exact

A Screen requires:

```text
Screen → Shot → Shot owner Actor → Actor default Theme
```

Do not infer context from App, Module, Variant, name, type, order or position.
Shot creation requires an explicit Actor; the editor never offers an empty
owner. A Shot Actor can later be changed to another same-Project Actor.

Reference Usage consumes typed relational and owner-declared JSON edges. Never
scan arbitrary text or JSON for references.

Production navigation contains Episodes plus one Production Data card with
Actors, Devices, Production Fonts and Render Presets. Resources never fall back
across Projects.

Conversation messages own Actor independently:

- incoming requires an explicit same-Project Actor;
- outgoing stores no Actor and resolves the Shot owner in the Production
  payload;
- system may optionally reference a same-Project Actor.

Direction changes that clear an Actor are one atomic prepared write.

## Hard rule: resource and asset parity

When desktop behavior, Preview output, icons, fonts, media, wallpaper or seeded
data changes, commit the corresponding artifacts in the same revision:

- `data/desktop-editor-spike.sqlite`;
- changed files under `assets/FOQN_S2`;
- changed files under `assets/system/system_icons`.

Font, icon and media references resolve from their Project assets and fail
explicitly when missing. Keep resource-specific logic out of repositories,
trees and `MainWindow`.

Keyboard owns its continuous pressed-popup geometry, single exterior shadow,
single glyph and horizontal containment. Do not split it into independently
rendered popup and key parts or move edge handling into generic Preview layers.

## Hard rule: standard desktop input behavior

Configure editor text inputs through the shared behavior. Preserve native
mouse, touch and keyboard selection, adapt primary Pen drag at that common
boundary and select complete numeric values on double click.

Do not add editor-local selection handlers or per-field click interception.

## Collaboration: discussion before implementation

Questions are discussion, not authorization to inspect broadly or implement.
When the user proposes or changes a design, data model, interaction or behavior,
first summarize the interpreted ownership and behavior boundaries. Wait for
explicit confirmation before editing.

## Collaboration: serialize code-writing tasks

Only one task may modify tracked project code or parity data in the shared
checkout at a time. Before handing off:

1. stop the desktop editor and every process that may write the parity database;
2. run applicable checks;
3. commit and push all intended changes when requested;
4. verify a clean worktree;
5. report branch and final commit.

The next writing task fetches and verifies that exact state before editing.
Parallel work is read-only or uses isolated worktrees and branches.

## Delivery

After every implemented update, report:

- concise changes;
- concrete manual checks;
- confirmation that the latest validated app is open, or why launch is not
  applicable;
- local commit and branch;
- worktree state.

Create a local commit for every coherent validated revision. Do not push until
the user explicitly asks.

When a version is intended for other computers, integrate it into `main`, push
`main`, switch the local checkout to `main` and verify local `main` equals
`origin/main`.
