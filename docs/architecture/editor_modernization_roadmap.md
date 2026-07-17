# Editor modernization roadmap

This document defines the low-friction path for moving the desktop editor spike toward a more data-driven, composable architecture.

It complements `editor_shell_non_negotiables.md`. The non-negotiables define what must not be broken. This roadmap defines the order in which to remove current architectural pressure.

## Direction

The target architecture is:

```text
editor layout metadata
  -> field catalog
  -> FieldDefinition
  -> ValueKind
  -> dictionary control registry
  -> shared commit coordinator
  -> field value service
  -> repository/database
  -> resolver
  -> preview payload/frame model
```

The editor shell should organize and delegate. It should not know individual field storage rules, value-specific controls, or record-specific visual behavior.

Generic routines must be extracted before they spread. Shared algorithms belong in common/editor-shell services, not inside whichever editor, bridge, renderer, importer, or repository first needed them.

Before adding a private helper for parsing, normalization, paths, colors, numeric conversion, SVG processing, token mapping, metrics, or other cross-cutting behavior, check `spikes/desktop-editor-shell/Common` and `docs/architecture/21_desktop_editor_base_routines_audit.md`. If an analogous routine exists, reuse or extend it in common rather than creating another local variant.

## Phase 1: make rules explicit

Keep the operational rules in:

- `AGENTS.md`
- `docs/architecture/editor_shell_non_negotiables.md`
- this roadmap

Every architecture cleanup should preserve a usable editor after each small step. Do not start with broad database or UI rewrites.

## Phase 2: extract field catalogs

Move field definitions out of `MainWindow`.

Start with low-risk record classes:

- project fields;
- episode fields;
- palette fields.

Then continue with:

- device fields;
- actor fields;
- theme fields;
- font fields;
- status/navigation bar scalar fields;
- component class fields.

Each field descriptor should declare:

- stable field id;
- label;
- `ValueKind`;
- default value;
- options, if any;
- editability;
- storage target or JSON path;
- semantic hints such as pair labels or token family;
- future validation and animation metadata.

Do not let layout JSON remain the only metadata. Layout may order fields, but field catalogs define what fields mean.

## Phase 3: extract field value access

Move field read/write logic out of `MainWindow`.

Introduce a shared service with responsibilities like:

```text
GetFieldValue(node, fieldId)
CurrentStoredValue(node, fieldId)
ToStorageValue(node, fieldId, draftValue)
CommitFieldValue(node, fieldId, storageValue)
```

`MainWindow` may wire commits, but it must delegate storage rules.

## Phase 4: extract editor extras

Move record-specific editor behavior out of `MainWindow`.

Examples:

- actor avatar preview;
- palette navigation swatch and used marker;
- theme color pair labels;
- navigation subtitles by record type;
- collection editor dispatch.

Use small extension/decorator classes instead of adding more `node.Kind` branches to the shell.

## Phase 5: introduce dictionary control registry

Make `DictionaryFieldControl` a row host, not the factory for every editor type.

Target shape:

```text
DictionaryFieldControl
  label
  restore/default state
  changed marker
  value editor slot

DictionaryControlRegistry
  ValueKind -> IDictionaryValueEditor
```

Each value editor owns its internal layout, validation, commit gesture, picker trigger, and display invariants.

### Shared editor layout baseline

The desktop spike now has a metadata-driven organization layer that must be
extended rather than bypassed:

```text
card groupLayout
  + optional group presentation
  -> stacked | flatStack | verticalCards | separatedSections
  -> shared EditorSubcardLayoutHost / EditorGroupBlock
```

`verticalCards` is the code/metadata name for the vertical-tab treatment.
`flatStack` is reserved for repeated siblings that share the parent surface.
`separatedSections` is field content separated by labelled rules. Mixed cards,
such as Button, declare the presentation per group rather than branching on
component type.

Group-level compound presentation is also metadata-driven. A group containing
Light/Dark palette pairs may declare `pairLayout: sharedHeader`; the registered
`PaletteColorPair` control then owns equal columns, compact widths, ellipsis and
the removal of repeated row labels/borders.

Expansion, selected internal section and scroll restoration are session-only.
Do not add these values to persisted window state, and do not reopen a default
card automatically in a fresh process.

`verticalCards` now owns a session-only resizable navigation panel. It keeps a
visible vertical splitter and natural block height while navigation plus the
minimum content width fit. Only when that sum no longer fits does it switch to
horizontal tabs. Complex dictionary values use a separator and full-width
block layout with their label above the registered control.

## Phase 6: refine ValueKind

Split broad kinds into semantic kinds before adding local exceptions.

Examples:

- `number.integer`;
- `number.decimal`;
- `pair.xy`;
- `pair.widthHeight`;
- `pair.lightDarkColor`;
- `token.paletteColor`;
- `token.themeColor`;
- `token.themeRadius`;
- `token.icon`;
- `token.iconList`;
- `path.directory`;
- `path.imageFile`.

Avoid inferring behavior from field id strings. If a pair needs `X/Y`, `W/H`, or `Light/Dark`, that belongs in metadata or the value kind.

## Phase 7: resolve before preview

Create an explicit resolver pipeline:

```text
editable data
  -> resolved data
  -> frame-specific data
  -> preview payload
```

The preview should consume resolved data. It should not know editor forms, draft controls, inheritance rules, or component override rules.

### Component class preview migration guardrail

Component class previews must not keep ad hoc legacy render branches in
`renderDesignPreviewHtml.tsx`. A component class may render only after it has a
component-specific resolver and renderable module that emit the shared generic
paint primitives, following the pattern established by `component.label`.

Until a component class is migrated, its design preview must use an obvious
unsupported placeholder. Do not reuse runtime `message_bubble_*` nodes,
component-specific module shortcuts, or plausible layout defaults to make an
unmigrated component look partially correct.

### Embedded component composition guardrail

Embedded components are recursive component slots, not copied field groups. The
authoritative contract is:

- `docs/architecture/23_embedded_component_composition_contract.md`

The reference implementation is `component.avatar` embedding `component.label`.

Future embedded components must preserve this route:

```text
parent component slot
  -> child base component config
  -> slot-local overrides
  -> child resolver contract
  -> component renderable module
  -> generic preview helpers
  -> web renderable primitives
```

Do not add child scalar fields directly to the parent field catalog. Do not
decide override state by comparing effective values with the base component.
Override state is stored state and only disappears when the override entry is
removed.

Composition must reference component variants, not parent component classes. The
parent class owns schema and variants; each concrete embedded/system/component
usage selects a variant by full reference:

```text
componentClassId::preset::presetId
```

The persisted reference still uses the internal `::preset::` delimiter and
`presetId` field names until a dedicated storage migration renames them. Those
names are compatibility details, not user-facing terminology.

Short preset ids are legacy migration input only. Saving a new variant must
clone the active selected variant config, never ambiguous "current class
values".

### Bubble component migration guardrail

When message bubble rendering is migrated to the new preview path, migrate the
bubble and all of its owned subcomponents together. Do not partially migrate the
bubble while leaving actor label, avatar, media, audio, video, icon button,
tail/chrome, or status subcomponents on legacy render paths.

The target route for each bubble-owned component is:

```text
component/module data
  -> component-specific resolver
  -> component renderable module
  -> generic preview helpers
  -> generic web renderer
```

The web renderer must not contain bubble-specific fallback branches that read
component config, resolve theme tokens, infer geometry, or preserve old
`message_bubble_*` behavior for migrated component classes. Any remaining
legacy `message_bubble_*` render types must be explicitly tied to unmigrated
runtime chat rendering and removed when that runtime path moves to the shared
resolver/renderable/helper contract.

## Phase 8: split repositories after field extraction

Do not start by splitting `SpikeDatabase`. First remove field and shell coupling.

Once field access is delegated, split database responsibilities into focused services/repositories:

- tree/project repository;
- field repository;
- theme repository;
- component class repository;
- collection repositories;
- preview payload/resolver service.

The first repository slice is governed by
`36_desktop_persistence_repository_contract.md`. It extracts the shared SQLite
context plus Editor Layout, Project/Episode and Render Preset repositories
behind the existing `SpikeDatabase` facade. Later slices must extend that
boundary instead of adding new table SQL back to the facade.

The next resource slice is governed by
`37_desktop_resource_repository_contract.md`. Palette, Device and Actor table
access moves behind focused repositories while device metrics and actor visual
interpretation remain common/domain behavior rather than persistence logic.

Cross-domain reference discovery is governed by
`38_explicit_reference_usage_contract.md`. The extracted Usage service owns one
typed set of exact reference edges shared by tree Used state, the Usage card
and deletion protection. Relational columns and JSON paths/contracts must be
declared; text-column scans, substring matching and label-based navigation are
not valid compatibility behavior.

Current workspace resource ownership is governed by
`39_design_production_resource_navigation_contract.md`. Production navigation
has one Episodes card and one Production Data card containing Actors, Devices,
Production Fonts and Render Presets. This is navigation ownership only; future
Project duplication must explicitly choose copy, current seeds or empty per
category and is not part of the current phase.

Theme persistence and Production Theme lookup are governed by
`40_theme_persistence_and_context_contract.md`. Theme row access and lifecycle
writes move to `ThemeRepository`; Module Instance Theme lookup is isolated as a
cross-domain service and no longer returns an empty document for missing
context.

Explicit Shot Production context and parity cleanup are governed by
`41_explicit_shot_production_context_contract.md`. A Screen requires an exact
Shot owner Actor and Actor Theme before creation; those references cannot be
cleared while Screens exist. The canonical parity project retains only
`episode_001 / shot_001`, and project-ordered Theme fallback is retired.

Production Font persistence is governed by
`42_production_font_persistence_contract.md`. `ProductionFontRepository` owns
the complete current `production_fonts` row and its explicit writes, while
filesystem import, asset deletion, font-face interpretation and tree/UI
presentation remain outside persistence.

Icon Theme persistence and asset separation are governed by
`43_icon_theme_persistence_and_asset_contract.md`. `IconThemeRepository` owns
current `icon_themes` rows and explicit writes; manifests, SVGs, provider
scripts, safe asset paths and token interpretation remain outside persistence.
Token reads require an explicit stored SVG filename and never repair mappings.

App and Module definition persistence is governed by
`44_app_module_definition_persistence_contract.md`. `AppModuleRepository` owns
their current rows and document writes while App/Module field semantics,
complete Module Variant authoring, Runtime forwarding, Module Instances and
Preview resolution remain in their existing domain owners.

Editor working-point continuity is governed by
`45_editor_session_view_state_contract.md`. Top-level card expansion, internal
navigation and clamped scroll are retained only in memory by exact editor
layout `recordClassId`; stable card/section ids replace node ids or positions,
and Preview/Variant history cannot persist or override that state.

Component Class definition persistence is governed by
`46_component_class_definition_persistence_contract.md`.
`ComponentClassRepository` owns strict current rows, ordered definition reads
and prepared complete document writes while component field semantics,
Variants, embedded composition, forwarding and Preview resolution remain in
their existing owners.

Module Instance persistence is governed by
`47_module_instance_persistence_contract.md`.
`ModuleInstanceRepository` now owns complete current Screen rows, ordered
reads, strict object documents and prepared lifecycle/document writes. Runtime
forwarding, Variant application, structured collection edits, owner-relative
animation, duration calculation, Shot synchronization and Preview resolution
remain in their existing domain coordinators.

Shot persistence is governed by `48_shot_persistence_contract.md`.
`ShotRepository` now owns complete current Production rows, strict documents,
direct writes and complete Shot copies for Shot/Episode lifecycle operations.
Exact Actor/Theme context, Module selection, Screen timing, duration
aggregation, render identity and Preview context remain in their domain
coordinators.

Component definition sources are governed by
`49_component_definition_source_contract.md`. The disconnected desktop runtime
Component seed/default catalog is retired; the current manifest, committed
Component Class documents and complete Variants, owner implementation and
editor metadata remain the only active authorities. Future Component/Atom
scaffolding must be an explicit complete development workflow.

Module definition sources are governed by
`50_module_definition_source_contract.md`. The disconnected Conversation
config/runtime-contract factory is retired; current manifest routes, committed
complete Module documents/Variants and owner implementations remain the active
authorities. Future Module scaffolding must be an explicit complete
development workflow.

Preview payload data access is governed by
`51_preview_payload_data_boundary_contract.md`. A typed data source now owns
the payload route's database/context reads while `DesignPreviewPayloadFactory`
retains forwarding, effective runtime envelopes and absolute Shot to local
Screen frame selection. This is the first Preview/resolver-data slice in the
final extraction item of contract 36.

Timeline data access is governed by
`52_module_instance_timeline_data_boundary_contract.md`. The common Module
Instance/Shot timeline now consumes typed current documents and ordered stable
Screen ids rather than the complete database facade; all duration, origin and
keyframe projection formulas remain in `ModuleInstanceTimeline`.

Actor Preview data access is governed by
`53_actor_preview_data_boundary_contract.md`. One typed source now supplies
exact current Actor context and avatar/mode values to Runtime record references
and the inline avatar preview. Actor payload and visual interpretation remain
in their factories, outside repositories, the bridge and the renderer.

Production Shot context data access is governed by
`54_production_shot_context_data_boundary_contract.md`. The explicit
Shot-to-Actor-to-Device/Theme route now enters navigation and Preview through a
typed source, while context validity, errors and navigation availability remain
in `ProductionShotContextService`.

Runtime Input option data access is governed by
`55_runtime_input_options_data_boundary_contract.md`. Actor, Palette and full
Component preset options now enter Runtime Input and animation dictionary
definitions through one typed source, while `ValueKind` mapping and declared
dynamic-list presentation remain in their generic factories.

Preview visual-context data access is governed by
`56_preview_visual_context_data_boundary_contract.md`. Device/Theme options,
the Project media root and resolved Device frame metrics now enter the Preview
controller through one typed source. The web Preview consumes a common metrics
DTO and no longer references `SpikeDatabase`.

Production Preview session data access is governed by
`57_production_preview_session_data_boundary_contract.md`. Shot fps, owning
Shot ids and selected Module Variant configs now enter the controller through a
typed source, while ordered Screen ids reuse the timeline data source. The
Preview controller no longer retains a database handle.

Isolated Component Preview input data access is governed by
`58_component_preview_input_data_boundary_contract.md`. Project fps, complete
Component Variant configs and effective embedded action contracts now enter the
Test Values session through a typed source. The session no longer retains a
database handle, and the action interpreter is persistence-independent.

Module Instance animation document access is governed by
`59_module_instance_animation_document_boundary_contract.md`. One typed store
now composes the shared timeline source, loads the exact selected Variant and
Screen documents and delegates complete v2 animation writes. The animation
editor retains authoring semantics without retaining a database handle.

Runtime Input owner document access is governed by
`60_runtime_input_owner_document_boundary_contract.md`. Module, Module Variant,
Component Variant and Screen documents plus concrete embedded Component sources
now enter the Runtime Inputs editor through a typed store. Instance
scalar/collection mutations remain the next separate extraction slice.

Runtime Input instance document mutations are governed by
`61_runtime_input_instance_document_boundary_contract.md`. Persisted Screen
scalar, stable collection and complete animation writes now pass through a
typed store that composes the animation document boundary. The Runtime Inputs
editor no longer retains a general database handle.

Animation keyframe drag feedback is governed by
`62_animation_keyframe_drag_interaction_contract.md`. The animation surface now
distinguishes its own synchronous Preview frame publication from external
playhead changes, preserving pointer capture without introducing another clock
or changing owner-relative persistence.

Shared dictionary persisted context is governed by
`63_dictionary_field_context_data_boundary_contract.md`. Theme and Icon Theme
context, Palette options and complete Component Variant Runtime documents now
enter the generic dictionary service through one typed read source. The service
retains UI composition without retaining or calling the general database
facade.

Embedded Component field documents are governed by
`64_embedded_component_document_boundary_contract.md`. Structural
`EditorEmbeddedContext` values no longer call persistence; one typed store now
serves exact Variant names, inherited field reads and explicit Design or
Runtime local Override commits to both breadcrumbs and embedded field access.

Shared editor presentation context is governed by
`65_editor_presentation_context_data_boundary_contract.md`. File pickers and
post-commit tree presentation now receive exact Project, Theme and Production
Font values through one typed read source while retaining their filesystem and
field-specific UI behavior outside persistence.

## Guardrails

Reject new changes that add:

- field definitions directly inside `MainWindow`;
- field persistence directly inside `MainWindow`;
- `TextBox`, `ComboBox`, `ToggleSwitch`, numeric inputs, color pickers, font pickers, or icon pickers for scalar values outside the dictionary route;
- record-specific navigation rendering directly inside `MainWindow`;
- field behavior inferred from string suffixes when metadata can declare it;
- preview logic that reads editor controls or form state;
- another value control path parallel to `ValueKind`.
- reusable SVG, theme-token, color, JSON-path, numeric parsing, import mapping, or device metric routines inside a single module instead of a common/shared class.
- development-only runtime fallbacks with plausible values. Missing current-model data must be migrated or fail visibly; defensive render fallbacks must use obvious diagnostics such as `debug_red` or unsupported placeholders.
- component-specific preview/render imports outside the owning component module, an explicit registry, or a declared embedded-component relationship. Run `npm run check:architecture` before closing preview/component migration phases.

Allowed custom editor chrome:

- cards;
- section headers;
- tree rows;
- toolbar buttons;
- add/delete/reorder collection row chrome;
- preview shell;
- modal frame.

Even inside collection rows, scalar fields must use `FieldDefinition` and dictionary controls.

## Size alarms

These are not formatting goals. They are early warning limits.

- `MainWindow.axaml.cs` should trend toward 600-800 lines.
- `DictionaryFieldControl.cs` should trend toward 250-350 lines.
- `SpikeDatabase.cs` should not grow during editor cleanup work; it should only shrink or be split.

If a change makes one of these files materially larger, extract first.

## Temporary exceptions

Temporary exceptions must be explicit and searchable:

```text
TODO(editor-architecture): explain why this exists and which phase removes it.
```

Do not add silent compatibility fallbacks or one-off local fixes for editor-specific behavior.
