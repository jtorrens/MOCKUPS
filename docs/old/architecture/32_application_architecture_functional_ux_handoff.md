# Application Architecture, Functional Flows and UX Audit Handoff

Status: current-system handoff for a subsequent architecture, functionality and
UX/UI simplification audit.

Application snapshot: Avalonia/Suki desktop editor `v0.317.46`, schema v1,
parameter animation v2. This document describes the implemented system after
the Lock Screen, structural Stack, Module Variant and owner-relative animation
phases. It is an orientation map, not a replacement for the normative contracts
linked below.

## 1. Purpose of this handoff

The next thread must study the application as one product rather than reviewing
one editor or component in isolation. Its task is to understand:

- the architecture and ownership boundaries;
- how reusable design data becomes concrete Production data;
- how the same model is presented through the current UX/UI;
- where the UI exposes necessary complexity and where it may expose accidental
  implementation complexity;
- which workflows can be simplified without weakening explicit contracts,
  stable identity, deterministic animation or preview purity.

The requested output of that audit should be an evidence-based simplification
proposal. It must distinguish:

1. terminology or presentation problems;
2. navigation and progressive-disclosure problems;
3. duplicated interaction patterns that can use an existing generic control;
4. genuine model complexity that the UI must preserve;
5. missing functionality;
6. architectural boundary violations, if any are found.

Do not begin by redesigning the data model. Trace several real flows end to end,
observe the running app and then decide whether a problem is in the model, its
projection into UI, or its wording.

## 2. Normative reading order

Read this handoff first, then use these documents for the authoritative detail:

1. `editor_shell_non_negotiables.md` — hard editor and preview boundaries.
2. `21_desktop_editor_base_routines_audit.md` — shared UI/common ownership.
3. `23_embedded_component_composition_contract.md` — recursive child Variants
   and local Overrides.
4. `24_desktop_preview_component_architecture.md` — resolver/renderable/renderer
   boundary.
5. `25_component_migration_status.md` — implemented component inventory.
6. `26_shot_module_instance_contract.md` — Production hierarchy and lifecycle.
7. `27_design_production_ux_separation.md` — workspace intent.
8. `29_animation_parameter_timeline_contract.md` — v2 tracks, owner timelines,
   duration and editor behavior.
9. `66_simplified_editor_retirement_contract.md` — retirement of the
   experimental projection and the current single-editor contract.
10. `31_structural_stacks_slots_and_module_instances.md` — Stack, State,
    forwarding, Module Variant and Module Instance integration.
11. `schema_v1_consolidation_manifest.md` — canonical SQLite boundary.
12. `component_behavior/*.md` — visual and behavioral contracts by component.

If an older architecture note conflicts with that set, the active index and
the documents above win.

## 3. The product in one diagram

```text
DESIGN SYSTEM
  Project resources
    Palette, Theme tokens, Fonts, Icon Themes, Devices, Actors, Assets
  Component class
    schema + resolver identity + editor layout
      -> Component Variants
           -> embedded child Variants + local Overrides
           -> optional Runtime Input Forwarding
  App
    -> Module class
         schema + resolver identity
           -> Module Variants
                structure + components + forwarded runtime contract

PRODUCTION
  Project -> Episode -> Shot
    Shot context: FPS, owner Actor, canvas/render references
      -> ordered Module Instances (Screens)
           explicit Module Variant
           real Runtime payload
           duration/transition
           owner-local v2 keyframes

FRAME PREPARATION
  Shot frame
    -> active Screen and Screen-local frame
    -> recursive owner timeline
    -> module resolver
    -> component resolvers
    -> component renderables
    -> generic token/asset/device translation
    -> fully resolved generic paint nodes
    -> HTML or raster Preview presentation
```

The critical rule is that arrows do not grant permission to the receiving layer
to reconstruct knowledge owned by the preceding layer.

## 4. Current desktop UX shell

The desktop window has two primary workspaces and three persistent columns:

```text
[ Design | Production ]

Navigation tree | Editor | Preview
```

### Design workspace

Design owns reusable definitions:

- Apps and Modules;
- component classes and their Variants;
- palette colors, Themes and tokens;
- Devices, Actors, production fonts and Icon Themes;
- Render Preset records;
- editor layouts, Runtime Input contracts, Test Values and usage links.

### Production workspace

Production owns concrete audiovisual intent:

- Project, Episode and Shot hierarchy;
- ordered Module Instances/Screens;
- real Runtime Values for each instance;
- Screen duration and transition declaration;
- parameter animation and shared Preview playhead.

The tree selection is the Preview context. Selecting a Shot previews the whole
Shot. Selecting a Module Instance previews that Screen. There is no second
Shot/Screen context combo because it created two competing navigation truths.
Preview transport and Animation transport share one project frame cursor.

### Editor column

The editor is metadata-driven. Its main surfaces are:

- header and breadcrumb;
- context strip showing class/Variant/Override context;
- generic cards;
- dictionary fields;
- structured collection editors;
- embedded-editor navigation;
- Runtime Values/Test Values and Animation;
- Usage.

Cards, internal tabs, collection expansion, split widths and scroll position are
session-only UI state. A fresh application session starts with cards closed.
These values are not domain data and must not enter SQLite or
`data/window-state.json`.

### Preview column

Design Preview combines the selected reusable definition with Test Values and
external context such as Device, Theme, mode and orientation. Production
Preview derives context from the selected Shot/Screen. The preview host owns
route, scaling, markers, reference overlay, playback and caching; it does not
own component semantics.

## 5. Canonical SQLite model and table interaction

The committed desktop database is `data/desktop-editor-spike.sqlite`. It has
schema `PRAGMA user_version = 1` and exactly the following domain tables.
Variants are intentionally stored inside owner metadata/config JSON; they are
not separate physical tables.

| Table | Owner and important payload | Main consumers and relationships |
| --- | --- | --- |
| `projects` | root identity, default FPS, media root, metadata | parent of all reusable resources and Episodes; supplies inherited FPS and asset root |
| `episodes` | Production grouping, order, notes | belongs to Project; owns ordered Shots |
| `shots` | order, version, FPS override, duration, owner Actor, render preset, canvas | belongs to Episode; owns ordered Module Instances and supplies Production context |
| `apps` | reusable app identity, record class, app type, config | belongs to Project; owns Modules; `System` is an App but does not own Actor wallpaper |
| `modules` | module class config, design preview/runtime declaration, Variant metadata | belongs to App; referenced by Module Instances |
| `module_instances` | explicit Module/Variant, order, duration, transition, `content_json`, `behavior_json`, v2 `animation_json` | belongs to Shot; concrete Screen and sole runtime module entity |
| `component_classes` | component type, record class, class config, design preview/runtime declaration, Variant metadata | belongs to Project; selected through full concrete Variant references |
| `editor_layouts` | `record_class_id -> layout_json` | defines generic card/group/field presentation for records and embedded contexts |
| `palette_colors` | stable token, hex value, neutral flag | project palette used by Theme and Actor mode values; unique token per Project |
| `themes` | Theme family, Icon Theme, Status/Nav Variant references, complete `tokens_json` | project visual system selected through Actor/preview context |
| `devices` | manufacturer/model/OS and physical/design metrics JSON | selected through Actor or Preview context; converts design units to final pixels |
| `actors` | identity, default Device/Theme, avatar and wallpaper metadata | Production context and explicit runtime references; never ambient inside child items |
| `production_fonts` | production family, category, directory and explicit files | Theme typography roles and deterministic measurement/rendering |
| `icon_themes` | token mapping and asset root | selected by Theme; resolves semantic icon tokens to committed SVG assets |
| `render_presets` | output dimensions/FPS/format/codec/color/quality/export JSON | referenced by Shots; data exists although Render Mode is not implemented |

The current parity snapshot contains one Project, three Episodes, one Shot,
three Apps, two Modules, two Module Instances, 26 component classes, two
Themes, 24 palette colors, eight Devices, three Actors, four production fonts,
six Icon Themes, seven Render Presets and 55 editor layouts. Counts describe the
current seed, not schema cardinality requirements.

### JSON ownership inside rows

- `config_json` is reusable design configuration.
- `design_preview_json` declares Runtime Inputs, collections, actions and Test
  Values for isolated design work.
- Component and Module Variants are complete snapshots stored in owner metadata.
- `content_json` is the real public Runtime payload of one Module Instance.
- `behavior_json` is reserved for instance behavior not represented as a public
  Runtime Input.
- `animation_json` is strict schema v2 parameter animation.
- `metadata_json` stores stable supporting metadata such as the selected full
  Module Variant reference; it is not a compatibility dumping ground.

Normal startup validates the current schema. It does not mutate old shapes,
invent missing values or keep legacy readers. A schema/token/reference change
requires one explicit converter, updated seed and committed SQLite parity file,
followed by removal of the converter from ordinary startup.

## 6. Tokens, Themes and final visual values

### Palette

Palette tokens map stable project names such as `gray_100` or `blue_bright` to
hex values. Neutral palette entries are marked separately so the Theme's
neutral tint can adjust them consistently. Palette colors are raw project color
resources; they are not Theme semantic roles.

### Theme

A Theme maps semantic visual intent to mode-specific values. Current token
families include:

- `modes.light/dark.colors.*` and keyboard colors;
- `spacing.none/xs/s/m/l/xl/xxl`;
- `radii.none/xs/s/m/l/xl/xxl/full`;
- typography text, system and emoji production-font roles, weights, styles,
  sizes and line heights;
- icon sizes;
- shadows;
- cursor metrics;
- keyboard geometry;
- neutral tint;
- motion transitions for Fade, Slide, Swipe and Scale;
- Reflow duration/easing and button-pushed duration;
- `motion.naturalPace.*` multipliers.

Theme Status Bar and Navigation Bar values are explicit component Variant
references. Their component-owned colors and structure are not copied into
Theme tokens.

Status Bar and Navigation Bar item definitions are fixed, Variant-owned
structured collections governed by contract 67. Their scalar values use the
generic dictionary route on every Variant; item ids, labels and kinds remain
development-owned and the editor exposes no add/delete/reorder actions.

### Symbolic until the correct boundary

Component and module configs retain symbolic values:

```text
theme.spacing.m
theme.colors.textPrimary
gray_100
theme.typography.sizes.xl
semantic icon token
```

Resolvers use those values to make component decisions but do not turn them
into final CSS/pixels. Generic preview translation resolves:

- mode-specific Theme values;
- palette token to hex and neutral tint;
- alpha;
- typography and production font files;
- semantic icon through the selected Icon Theme;
- spacing/radius/size tokens;
- device design units to physical pixels.

The web renderer receives final values. It never sees Theme token names,
palette names, database rows or inheritance.

### Motion and Natural Pace

Theme transition tokens define duration, delay, easing and intensity. Reflow is
the shared motion used for changes in position or size of a stable element.
`BehaviorTiming` is a dictionary value with Fixed and Natural modes. Natural
duration is:

```text
semantic units × component/module base frames per unit × Theme Natural Pace
```

The component/module owns semantic units, base rhythm and deterministic internal
cadence. Theme owns only pace. For example Password uses four reference frames
per digit. The preview still receives resolved state for each requested frame;
there is no timer in HTML/CSS.

## 7. Actors, Device, Theme, avatar and wallpaper

Actor is a reusable project record with:

- display identity and initials;
- default Device;
- default Theme;
- avatar image/initial settings;
- Light/Dark avatar colors;
- one wallpaper contract with kind, opacity, per-mode background color and
  separate optional Light/Dark images.

Wallpaper belongs to Actor by default. `System` and Lock Screen do not own a
duplicate wallpaper. A module/app that has no explicit background may consume
the selected Actor wallpaper. If an image is absent for the active mode, the
mode's authored wallpaper background color is used; there is no cross-mode
image fallback.

There are two Actor roles that must not be confused:

1. the Shot owner Actor supplies external Production context such as default
   Device and Theme;
2. a component/module Runtime Input may explicitly reference an Actor as
   content, for example Lock Screen wallpaper owner, Conversation participant,
   Message sender, Audio avatar or Notification sender.

Those references may deliberately differ. Runtime forwarding never binds Actor
fields by type or label. A parent must forward or populate each declared Actor
field explicitly.

## 8. How an editable field enters the system

Every editable scalar follows one route:

```text
editor layout metadata
  -> FieldDefinition
  -> ValueKind
  -> registered dictionary control
  -> generic commit coordinator/value router
  -> repository/SQLite JSON field
  -> editor refresh and preview invalidation
```

`FieldDefinition` owns identity, label, type, options, numeric limits, units,
conditional enablement, runtime source and animation eligibility. `ValueKind`
owns the appropriate reusable control. Editors organize fields; they do not
construct raw text boxes, combos, toggles or pickers for domain values.

Compound controls such as Placement, Motion, Typography, palette pairs,
Theme-token pairs, `BehaviorTiming`, component Variant references and structured
collections own their own responsive layout and visual invariants.

Structured collections use the shared collection editor for add, duplicate,
delete, reorder, expansion and nested fields. The collection may appear in
Variant design, Test Values or a Module Instance; only its owner/commit path
changes.

## 9. From Atom to reusable Component

### Atom

An Atom is a component class categorized as a low-level reusable primitive. It
still uses the complete component architecture:

1. create/seed a `component_classes` row with stable id, component type and
   record class;
2. define scalar fields through the field catalog and `editor_layouts`;
3. add/extend dictionary controls only when the value kind is genuinely
   reusable;
4. define one protected `Default` Variant;
5. declare Runtime Inputs and collections in `design_preview_json`;
6. implement its component contract/resolver;
7. implement its component renderable using generic atoms;
8. register only the route to that owner module;
9. test isolated Preview with Test Values;
10. document the behavior sheet and commit seed DB/assets.

Examples include Label, Surface, Badge, Component Stack and Collection Stack.
Being an Atom does not permit special editor or renderer logic.

### Component class and Variant

The class owns schema, resolver identity, editor layout and Variant list. A
Variant is a named complete config snapshot. Storage, code and product language
all use Variant vocabulary.

Full component Variant references are always:

```text
componentClassId::variant::variantId
```

Short ids and parent class references are not valid new composition values.
Only `Default` is system-protected. User Variants may be renamed, duplicated,
locked or deleted when usage permits.

### Embedded child component

A parent embeds a child role through a slot:

```text
parent Variant
  -> concrete child Variant
  -> slot-local Overrides
  -> child Runtime bindings
```

The embedded editor uses the child's normal layout, dictionary controls and
commit path. Override opens that real editor context; it does not create a
parent-specific imitation. Inheritance/restore removes the local leaf override
and reveals the selected child Variant value.

The parent resolver may import a concrete child only when the parent explicitly
owns that slot. Shared helpers and registries may not import arbitrary concrete
components.

## 10. Runtime Inputs, Variant boundaries and Forward

A child Runtime Input becomes Variant-owned when it crosses a new composition
boundary unless the parent explicitly keeps/promotes it as Runtime. This makes
each reusable parent deterministic instead of leaking every nested input.

```text
child Runtime Input
  -> parent Variant value (default at new boundary)
  -> optional Forward triangle
  -> stable parent Runtime Input
```

Only fields the child already declares Runtime are eligible. Activating Forward:

- retains the current Variant value as the runtime default;
- makes the Variant field read-only;
- exposes an editable runtime label without changing technical id/key;
- preserves type, options, action and animation metadata;
- permits another explicit promotion at a later boundary.

Removing a forwarding link that has downstream values/usages requires
confirmation. Changing or deleting a structured item with forwarded descendants
also requires Accept/Cancel; it must not silently strand runtime values.

### Test Values versus real Runtime Values

Design Test Values are session fixtures for isolated Preview. They persist only
for the application session unless explicitly saved as defaults. Re-entering an
editor restores the session's Test Values rather than regenerating defaults.

A Module Instance uses the same effective contract and nested presentation, but
its Runtime Values are real persistent `content_json`. Test Play/Restore action
controls become animatable fields at the Production boundary; Production does
not persist simulated button presses.

## 11. Structural composition

### Ordinary embedded slots

Use for a fixed known child role: Surface, Label, Avatar, Status Bar, Navigation
Bar, Icon Bar, and similar. The parent chooses one concrete child Variant and
may Override it.

### Component Stack

Use when semantic positions are fixed but the visible component in a position
can change:

```text
Component Stack
  ordered Slot
    ordered State
      concrete component Variant or None
      Overrides + Runtime bindings
      Placement
      Enter/Exit Motion
      Replace or Overlay
```

The first State is Initial by collection order. Slots and States have editable
presentation names but stable ids. State selection is independent per Slot.
Replace swaps the visible base; Overlay adds without removing it; None + Replace
clears the Slot. State Placement is relative to the frame assigned to its Slot.

Container Start/End gaps are separate from each Slot's gap with its predecessor.
Fill distributes across the assigned parent frame; Fit Content sizes from
children. Fixed and Reflow gaps are generic layout decisions.

### Collection Stack

Use when runtime item count/order varies:

- Flow: ordered layout with normal gaps and Fill/Fit Content;
- Stacked: layered Fit Content with direction, offset, scale and opacity ratios.

Items own stable ids, one child Variant, Overrides, Runtime Inputs, Present and
Presence Motion. Removal completes Exit/Presence Motion first; surviving items
then reposition through Theme Reflow.

### Domain wrapper

Notifications wraps Collection Stack and supplies Notification children. This
lets users edit notification semantics and common style instead of generic child
selectors. The wrapper does not move notification state into Collection Stack.

## 12. Modules and Module Variants

An App groups Modules. The `System` App currently owns Lock Screen; Chat owns
Conversation. App identity does not imply wallpaper ownership or automatically
bind context.

A Module class owns shared schema, runtime declarations and resolver identity.
A Module Variant owns a complete reusable screen composition, including:

- system bar Variants;
- component/Stack Variant;
- slots, States and child Variants;
- placements, gaps and motions;
- Overrides;
- selected forwarding decisions.

Module Variant references are explicit:

```text
moduleId::variant::variantId
```

Actor, Device, Theme or OS never infer a Module Variant. Platform-specific Lock
Screens are separate explicit Variants if their structure differs.

The effective public runtime contract is computed before editing or preview:

```text
module shared Runtime declarations
  + selected Module Variant
  + recursive Forward metadata
  = effective Module Instance contract
```

## 13. Production: Episode, Shot and Module Instance

Production hierarchy is deliberately direct:

```text
Project -> Episode -> Shot -> ordered Module Instances
```

There is no separate Screen Instance layer. One Module Instance is the named
Screen placed on the Shot timeline.

Adding one opens the shared modal and requires:

1. Module;
2. concrete Module Variant;
3. tree name, automatically proposed as `Module · Variant` until the user edits
   it.

The instance receives only Runtime fields declared by the effective contract.
It does not copy the Module Variant design structure. Rename, duplicate and
delete act on the concrete instance, not its Module or Variant. Variant changes
explicitly prune runtime values and tracks no longer present in the new
contract.

Module Instances are ordered. Current Shot transitions are Cut. Calculated
duration modules such as Conversation derive their finite extent. Explicit
duration modules such as Lock Screen expose an authored Screen frame count.
Shot duration is the ordered Screen timeline result.

## 14. Parameter animation and recursive time ownership

Animation persists only strict v2 tracks:

```text
(fieldId, optional stable targetId)
  -> ordered keyframes
       id, owner-local frame, typed value, interpolation, enabled
```

Every activated track gets mandatory enabled KF0. KF0 cannot be deleted or
dragged. There is at most one keyframe at one frame per track.

### Universal ownership rule

```text
an entity enters/exits in parent time;
its own keyframes are relative to its first appearance.
```

Resolution recurses:

```text
Shot frame
  -> Screen local frame
    -> Slot local activation
      -> State owner frame
        -> component/action local frame
```

Moving a Screen, reordering an item or changing a parent's timing changes the
effective absolute frame but never rewrites child keyframes. A State re-entry
restarts its parent-owned Enter Motion but not its first-appearance internal
timeline. Collection items likewise retain their first-appearance origin.

### Timeline editor

The authoring slider displays Screen coordinates while persistence remains
owner-local. The shared preview and animation cursors stay synchronized.
Nonzero mini-timeline keyframes can be dragged:

- normal drag snaps to five Screen frames;
- `Alt` drag snaps to every Screen frame;
- existing keyframes have a stronger detent;
- occupied/invalid destinations do not commit;
- `Escape` restores;
- Preview follows during drag;
- the database writes once on valid release after conversion to owner-local
  time.

No editor duplicates duration/origin formulas. `RuntimeAnimationFrameOrigin`,
the runtime contract and the owner timeline supply them.

### Frame resolution, never timers

Transitions, State changes, Password entry, calculated text, media state and
Reflow are resolved for each requested frame before Preview. The bridge and
renderer receive final state for that frame. They do not run timers, CSS
animation, countdowns or component-specific interpolation.

## 15. End-to-end Preview pipeline and strict boundaries

```text
SQLite/current editor values
  -> C# payload factory
  -> explicit component/module registry route
  -> owning TypeScript contract/resolver
  -> owning renderable composition
  -> generic preview helpers
  -> token/asset/device translation
  -> generic final paint tree
  -> generic HTML adapter
  -> WebView or raster Preview host
```

### Layer boundary matrix

| Layer | May know | Must not know/do |
| --- | --- | --- |
| editor/catalog | FieldDefinition, layout metadata, Variants, Overrides, Runtime contracts | final component geometry, CSS, token-to-hex resolution |
| payload factory | selected records, context, current frame, explicit refs | component visual business rules |
| registry | concrete id -> owning module route | defaults, layout, token resolution, renderable construction |
| component/module resolver | its own semantics, composition, design units, symbolic tokens | SQLite, Avalonia controls, HTML/CSS, plausible missing-value fallback |
| component renderable | owned child composition into generic nodes | editor state, database, renderer behavior |
| generic helpers/bridge | palette/Theme/alpha, assets, fonts, device pixels, generic placement/motion | branches or field names for concrete components |
| web renderer | final boxes/text/images/SVG/surface/shadow/transform nodes | Variants, inheritance, tokens, database, timers, component names |
| Preview host | route, cache, playback, scaling, overlays, WebView/raster presentation | component state calculation |

If a feature seems to require `if componentType == ...` in a bridge, renderer or
`MainWindow`, its ownership is wrong. Move the decision into the component
resolver/renderable or parameterize a genuinely generic primitive.

### Fonts, icons and assets

Production text uses explicit Theme font ids for text, System text and emoji.
The resolver/measurement path loads committed files, shapes and wraps with the
same fonts used by HTML. Missing required files are errors; host-system font
fallback is forbidden.

Semantic icons resolve through the selected Icon Theme to committed SVGs.
Images, wallpaper and media use project-relative paths under committed assets.
Desktop asset interning and WebView blob URLs are transport optimizations, not
domain contracts.

## 16. Current component/module coverage

The current seed includes reusable classes for Audio, Avatar, Badge, Bubble,
Button, Code Indicator, Collection Stack, Component Stack, Cursor, Draw
Password, Face Recognition, Fingerprint, Icon Bar, Icon Row, Keyboard, Keypad,
Label, Media, Navigation Bar, Notification, Notifications, Password, Status Bar,
Surface, Text Box and Text Input Bar.

Current Modules are Conversation and Lock Screen. Their detailed implementation
status and remaining issues live in `25_component_migration_status.md` and the
individual behavior sheets. The audit should sample both:

- Conversation exercises a serial calculated collection timeline;
- Lock Screen exercises explicit duration, Module Variants, parallel Stack
  slots, State switching and forwarded nested runtime/action fields.

## 17. Generic UX patterns that must be assessed

The next audit should observe, not infer, these flows in the running app:

1. class node -> remembered concrete Variant;
2. Variant selection, duplication, rename, lock and usage-blocked deletion;
3. embedded Variant row -> open child Variant or local Overrides -> breadcrumb
   return with tab and scroll restoration;
4. empty/filled Forward triangle and read-only Variant field;
5. structured collection add/duplicate/reorder/delete and nested navigation;
   for the fixed Status/Navigation item collections, verify instead that those
   structural actions are absent while values remain editable per Variant;
6. Component Stack Slot/State naming and Replace/Overlay understanding;
7. Runtime Test Values persistence during a session;
8. action combo/switch with compact Play/Restore;
9. Module Test Values versus persistent Module Instance Runtime Values;
10. animation activation, KF0, keyframe authoring, drag and shared Preview cursor;
11. Shot/Screen tree selection as Preview context;
12. responsive field contraction, vertical tabs and compact-card layout;
13. Usage navigation and destructive confirmation.

Questions for each flow:

- Does the user understand which entity will be modified?
- Is reusable Variant data clearly separated from Runtime content?
- Is inheritance versus Override visible without excessive chrome?
- Are stable-id concepts exposed only when needed?
- Can a novice complete the common path without opening advanced structure?
- Can an expert still reach every explicit contract without hidden magic?
- Does returning from a nested editor preserve cognitive and scroll context?
- Are disabled, inherited, forwarded and animated states visually distinct?
- Are Component Variant labels consistent across UI, storage and code?

## 18. Simplification opportunities to investigate

These are audit hypotheses, not approved changes:

- progressive disclosure between common content edits and structural/style
  authoring;
- clearer ownership language for class, Variant, embedded Override, Test Value
  and instance Runtime Value;
- reducing repeated headers while preserving context and navigation;
- making Stack Slot/State concepts legible without exposing storage vocabulary;
- unifying action and animation affordances across root and collection scopes;
- determining whether component selection + Variant selection needs a more
  compact two-stage control in narrow panels;
- checking whether General/Runtime/Animation grouping can be more predictable;
- surfacing Theme/Actor/Device provenance without adding ambient inheritance;
- improving Usage impact review before destructive forwarding/Variant changes;
- deciding which advanced fields belong in Complete only.

Do not simplify by:

- copying child fields into parents;
- making Runtime forwarding automatic;
- inferring Variants from Actor/Device/OS;
- replacing stable ids with labels or positions;
- storing editor/session presentation in domain data;
- adding component-specific UI to `MainWindow`;
- letting the renderer resolve tokens, inheritance or animation;
- adding compatibility fallbacks for incomplete data.

## 19. Explicitly unfinished product areas

### Render Mode has not been entered

The database contains Render Presets and Preview supports HTML and raster
presentation routes, but the product has not yet entered implementation of a
dedicated Render Mode/export workflow. There is no completed render queue,
render-job lifecycle, final export UX, approval-to-render pipeline or portable
package workflow. Raster Preview is a Preview route, not proof that Render Mode
is complete.

The next architecture/UX audit may identify requirements or integration risks,
but must label them future Render work rather than current regressions.

### Simplified Editor has been retired

The Simplified/Complete projection was removed after the audit determined that
maintaining a separately curated field subset did not justify its preparation
cost. The complete metadata-driven editor is the sole authoring surface.
Contract 66 removes projection/capture behavior and makes the retired layout
metadata invalid current data.

Future progressive disclosure may reorganize the complete editor, but it must
be proposed independently and must not reintroduce copied fields, automatic
capture or a parallel schema/commit route.

### Other known future areas

- non-Cut transitions between Module Instances;
- completed Production approval/versioning workflow;
- final render/export and on-set package design;
- evidence-based progressive disclosure inside the complete editor, if needed;
- performance work only where measurements justify it.

## 20. Suggested audit procedure for the next thread

1. Verify `main`, commit hash and clean worktree before any code changes.
2. Read the normative documents in section 2.
3. Inventory tree, cards, generic controls, tables and preview registries from
   current code rather than historical React material.
4. Run the app and record the twelve flows in section 17 with screenshots or a
   structured observation log.
5. Trace at least these scenarios end to end:
   - change a Label Variant field;
   - Override that Label when embedded;
   - Forward its runtime text through a parent;
   - build/switch a Component Stack State;
   - edit the corresponding Lock Screen Module Instance runtime payload;
   - author and drag a State or Password keyframe;
   - preview the complete Shot;
   - change Actor wallpaper/Theme context and verify provenance.
6. Map every observed UI problem to one ownership layer.
7. Produce a prioritized proposal with low-risk presentation changes separated
   from contract/data changes.
8. For every proposed simplification, state which invariants and automated
   checks protect it.

## 21. Validation and key source map

Before accepting architecture changes run:

```text
npm test
npm run check:architecture
npm run desktop:db:validate
```

Key implementation locations:

- shell/orchestration: `spikes/desktop-editor-shell/MainWindow.axaml*`;
- editor generic controls: `spikes/desktop-editor-shell/EditorShell/`;
- shared algorithms: `spikes/desktop-editor-shell/Common/`;
- schema, validation and current repositories: `spikes/desktop-editor-shell/Data/`;
- committed parity data: `data/desktop-editor-spike.sqlite`;
- production assets: `assets/FOQN_S2` and `assets/system/system_icons`;
- preview contracts/resolvers/renderables: `src/desktop-preview/`;
- final generic visual vocabulary: `src/visual/renderable/`;
- architecture enforcement: `scripts/checkDesktopPreviewArchitecture.ts`;
- frame-contract tests: `tests/animation/` and
  `spikes/desktop-editor-shell-animation-tests/`.

The audit is successful only if its proposed UX is simpler while preserving the
same explicit data ownership, reusable composition, deterministic frame
resolution and pure final renderer.
