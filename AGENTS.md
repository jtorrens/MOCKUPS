# MOCKUPS agent working rules

Before changing the Avalonia/Suki desktop editor spike, read and follow:

- `docs/architecture/editor_shell_non_negotiables.md`
- `docs/architecture/editor_modernization_roadmap.md`
- `docs/architecture/24_desktop_preview_component_architecture.md`
- `docs/architecture/25_component_migration_status.md`
- `docs/architecture/33_persistence_and_migration_contract.md`
- `docs/architecture/34_manifest_routing_payload_and_dictionary_contract.md`
- `docs/architecture/35_current_json_and_variant_contract.md`
- `docs/architecture/36_desktop_persistence_repository_contract.md`
- `docs/architecture/37_desktop_resource_repository_contract.md`
- `docs/architecture/38_explicit_reference_usage_contract.md`
- `docs/architecture/39_design_production_resource_navigation_contract.md`
- `docs/architecture/40_theme_persistence_and_context_contract.md`
- `docs/architecture/41_explicit_shot_production_context_contract.md`
- `docs/architecture/42_production_font_persistence_contract.md`
- `docs/architecture/43_icon_theme_persistence_and_asset_contract.md`
- `docs/architecture/44_app_module_definition_persistence_contract.md`
- `docs/architecture/45_editor_session_view_state_contract.md`
- `docs/architecture/46_component_class_definition_persistence_contract.md`
- `docs/architecture/47_module_instance_persistence_contract.md`
- `docs/architecture/48_shot_persistence_contract.md`
- `docs/architecture/49_component_definition_source_contract.md`
- `docs/architecture/50_module_definition_source_contract.md`
- `docs/architecture/51_preview_payload_data_boundary_contract.md`
- `docs/architecture/52_module_instance_timeline_data_boundary_contract.md`
- `docs/architecture/53_actor_preview_data_boundary_contract.md`
- `docs/architecture/54_production_shot_context_data_boundary_contract.md`
- `docs/architecture/55_runtime_input_options_data_boundary_contract.md`
- `docs/architecture/56_preview_visual_context_data_boundary_contract.md`
- `docs/architecture/57_production_preview_session_data_boundary_contract.md`
- `docs/architecture/58_component_preview_input_data_boundary_contract.md`
- `docs/architecture/59_module_instance_animation_document_boundary_contract.md`
- `docs/architecture/60_runtime_input_owner_document_boundary_contract.md`
- `docs/architecture/61_runtime_input_instance_document_boundary_contract.md`
- `docs/architecture/62_animation_keyframe_drag_interaction_contract.md`
- `docs/architecture/63_dictionary_field_context_data_boundary_contract.md`
- `docs/architecture/64_embedded_component_document_boundary_contract.md`
- `docs/architecture/65_editor_presentation_context_data_boundary_contract.md`
- `docs/architecture/66_simplified_editor_retirement_contract.md`
- `docs/architecture/67_system_bar_item_authoring_contract.md`
- `docs/architecture/68_architecture_ux_cleanup_and_scaffolding_plan.md`
- `docs/architecture/69_component_variant_storage_vocabulary_contract.md`
- `docs/architecture/70_conversation_message_actor_ownership_contract.md`
- `docs/architecture/71_active_code_retirement_contract.md`
- `docs/architecture/72_single_semantic_rule_ownership_contract.md`
- `docs/architecture/73_owner_validation_and_preview_document_boundary_contract.md`

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

## Hard rule: persistence startup is read-only

Opening an existing desktop database, constructing its repository and validating
its current contract must not modify the database file, its schema or its data.
All persistence work must follow
`docs/architecture/33_persistence_and_migration_contract.md`.

Normal application startup must never run `Ensure*`, `Normalize*`, `Retire*`,
schema repair, seed repair or duration synchronization routines. Database
creation and data migration are explicit maintenance workflows, never side
effects of opening the application. A migration must update the canonical
schema/seeds and committed parity database, validate the result, and remove its
temporary migration code in the same delivery.

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
It is keyed only by the exact editor layout `recordClassId`, with top-level
cards and internal sections restored by explicit stable ids rather than node
ids, labels or positions. Preview and Variant history must not serialize or
override this state. See
`docs/architecture/45_editor_session_view_state_contract.md`.

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

Animation editors show a Screen-local authoring scale while persisted collection keyframes remain relative to their stable owner; the shared Preview playhead remains absolute in Shot time internally. Contract-declared base/finite durations use the shared reference-duration lane. Retime is off when `targetDurationFrames` is absent; provisional right-side authoring margin is session-only and must never be persisted as duration or window state.

Temporal ownership is uniform for every entity. Appearance, disappearance,
activation and selection are authored in the local time of the parent; the
entity's own fields and keyframes are authored relative to its first appearance.
Reordering or moving an entity recalculates effective frames without rewriting
stored local keyframes. Re-entry restarts parent-owned Enter/Exit Motion but does
not restart the entity's internal timeline. Stable ids, never indices, bind
owners and tracks. A selected Screen presents its own local authoring scale even
when Preview keeps one absolute Shot playhead internally.

Screen duration is also contract-owned. A module declares `calculated` when its
finite actions/collections determine the Screen extent, or `explicit` when the
Module Instance's persisted frame count is authoritative. Explicit duration
requires a declared positive default and is edited only on the instance;
keyframes and child composition must not extend it silently. The authoring `+`
horizon remains session-only in both policies.

Component inputs are runtime component inputs, not preview-only controls. The preview panel may provide sample values for isolated inspection, but screens/modules must later supply real values through the same declared input contract. Do not add component-specific input catalogs or animation behavior to the preview shell.

Component composition must reference concrete Component Variants, not parent Component Classes. Parent classes own schema, resolver identity and Variant lists; reusable visual instances store full Variant references in the form `componentClassId::variant::variantId`. Short Variant ids and retired Component Preset spellings are invalid current data. Saving a new Variant must clone the active selected Variant config, never ambiguous "current class values". `Preset` remains a distinct term used by Render Presets and reserved for future non-Variant reusable recipes.

If a change appears to require `if componentType == ...` behavior in the bridge or renderer, stop and move that responsibility to the component resolver or to a parameterized common helper.

## Hard rule: manifest, routing and payload contracts are strict

All Preview component/module identities, categories, entrypoints and declared
embedded dependencies come from
`src/desktop-preview/desktopPreviewManifest.json`. Registries route prepared
payloads by exact stable id and do nothing else: no forwarding, defaults,
merging, renderable construction or unsupported fallback surfaces.

Payload preparation and explicit Runtime Input forwarding belong to the shared
payload boundary before registry dispatch. `designPreviewJson` may become local
at an embedded boundary; the complete `runtimeContractJson` temporal-owner
envelope must remain unchanged through recursive composition. Every current
Runtime Input definition has an explicit canonical `valueKind`, and the
dictionary registry must exhaustively register every `ValueKind`. Missing or
unknown routes, payload documents, kinds and value kinds fail explicitly. See
`docs/architecture/34_manifest_routing_payload_and_dictionary_contract.md`.

Required serialized Preview documents are validated as current JSON objects at
the web payload boundary before registry dispatch. Blank, malformed, absent or
wrong-root required documents must fail and must never become `{}` in
`previewJsonHelpers`, a resolver, registry or renderer. Optional documents are
optional only when declared explicitly by the payload contract.

`DesignPreviewPayload.ThemeMode` is authoritative when it contains explicit
`light` or `dark`. The renderer may use session mode only when the payload has
no explicit effective mode; it must not parse Module `appearanceMode` or let a
session mode override an explicit payload mode. See
`docs/architecture/73_owner_validation_and_preview_document_boundary_contract.md`.

App and Module definition nodes expose Rename as their only lifecycle action.
Creating, duplicating or deleting either definition belongs to the explicit
development/scaffolding process that also supplies its manifest route,
resolver, renderable, contract and migration. Module Variants remain authored
data: they may be created by cloning the active complete Variant, duplicated
and renamed; deletion is allowed only when the Variant is unused, unlocked and
not protected. The protected default Variant may be renamed but never deleted.
All these operations preserve stable ids and full Variant references.

## Hard rule: current JSON and Variant envelopes are strict

Every persisted JSON column is consumed as its declared current root kind.
Blank, malformed or wrong-root documents fail; normal readers and write paths
must not turn them into `{}`, `[]` or a plausible default. Component and Module
Variant arrays are required current data. Every Variant is a complete named
snapshot with an explicit stable id, `protected`, `locked` and object `config`.
Readers must reject malformed entries instead of filtering them, must not infer
lock/protection from the `default` id and must not fall back from a missing
Variant config to class config. Variant creation may construct a new complete
snapshot explicitly; editing current data may not repair an incomplete one.
See `docs/architecture/35_current_json_and_variant_contract.md`.

## Hard rule: desktop repositories have explicit ownership

Desktop persistence uses the shared SQLite context and focused repositories
defined by `docs/architecture/36_desktop_persistence_repository_contract.md`.
`SpikeDatabase` is a compatibility facade and orchestration boundary; new SQL,
connection-string construction, write synchronization or table-specific row
mapping must not be added to it when an owning repository exists. Repositories
consume only the current validated model and must not introduce repair,
normalization, migration or fallback behavior.

Palette, Device and Actor persistence additionally follows
`docs/architecture/37_desktop_resource_repository_contract.md`. Their
repositories own table SQL, row mapping, explicit lifecycle persistence and
stored-document writes. Device/Actor interpretation remains in common/domain
services and must not move into the repository, tree or UI shell.

Component Class definition persistence follows
`docs/architecture/46_component_class_definition_persistence_contract.md`.
`ComponentClassRepository` owns current row SQL/mapping and prepared complete
document writes. Field paths, Variants, embedded composition, forwarding,
payloads, resolvers and renderables remain in their domain owners. Definition
creation/retirement remains an explicit development/scaffolding workflow.

Module Instance persistence follows
`docs/architecture/47_module_instance_persistence_contract.md`.
`ModuleInstanceRepository` owns current Screen rows, strict object documents
and prepared row writes. Variant selection, Runtime forwarding, structured
collections, owner-relative animation, duration policy, Production Theme
context, payload preparation and Preview remain outside persistence.

Shot persistence follows `docs/architecture/48_shot_persistence_contract.md`.
`ShotRepository` owns complete current Shot rows and prepared row writes.
Exact Actor/Theme context, Module/Variant selection, Screen timing, Shot
duration aggregation, effective Device, payload preparation and Preview remain
outside persistence. Shot and Episode duplication must preserve every current
Shot column and generate new stable ids.

Component definition sources follow
`docs/architecture/49_component_definition_source_contract.md`. The retired
runtime Component seed/default catalog must not return. Current manifest,
committed Component rows, complete Variants, owner implementation and editor
metadata are the authorities; future Component/Atom scaffolding is an explicit
development workflow, never normal startup or a generic editor Add action.

Module definition sources follow
`docs/architecture/50_module_definition_source_contract.md`. Dormant
hard-coded Module config/runtime-contract factories must not compete with the
current manifest, committed complete Module Variants and owner implementation.
Future Module scaffolding is an explicit complete development workflow, never
normal startup or a generic editor Add action.

Preview payload data access follows
`docs/architecture/51_preview_payload_data_boundary_contract.md`.
`DesignPreviewPayloadDataSource` is the payload factory's only database
boundary and contains no SQL. The factory retains forwarding, effective runtime
envelopes and Shot-to-Screen frame selection; resolvers, bridge and renderer
retain their established semantic and generic boundaries.

Timeline data access follows
`docs/architecture/52_module_instance_timeline_data_boundary_contract.md`.
`ModuleInstanceTimelineDataSource` supplies current documents and ordered stable
Screen ids without SQL; `ModuleInstanceTimeline` alone owns duration, Screen
origin and keyframe projections and must not accept `SpikeDatabase`.

Theme persistence and Production Theme context additionally follow
`docs/architecture/40_theme_persistence_and_context_contract.md`.
`ThemeRepository` owns Theme row SQL and lifecycle writes; the facade and tree
delegate. Module Instance Theme context is a separate cross-domain service and
must resolve a complete current document or fail explicitly.

Shot Production context additionally follows
`docs/architecture/41_explicit_shot_production_context_contract.md`. A Module
Instance requires an exact `Shot -> owner Actor -> Actor default Theme` route.
Do not infer context from App, Module, Variant, name, type, order or position.
Shot creation requires an explicit Actor selection, `owner_actor_id` is a
non-empty restricted foreign key, and the Shot editor never offers `None`.
The committed parity project retains only `episode_001 / shot_001`; lifecycle
tests use disposable database copies rather than adding test Shots to it.

Reference discovery, tree `Used` state, Usage presentation and deletion
protection additionally follow
`docs/architecture/38_explicit_reference_usage_contract.md`. They must consume
one typed edge set produced from exact relational declarations and
owner-declared JSON paths/contracts. Never scan text columns or arbitrary JSON,
match substrings, or infer navigation/scope from source labels.

Design/Production resource placement additionally follows
`docs/architecture/39_design_production_resource_navigation_contract.md`.
Production exposes Episodes plus one Production Data card containing Actors,
Devices, Production Fonts and Render Presets. Workspace changes must not move
SQLite ownership, invent global records or introduce cross-Project fallback.

Conversation message Actor ownership additionally follows
`docs/architecture/70_conversation_message_actor_ownership_contract.md`.
Incoming messages require an explicit same-Project Actor; outgoing messages
persist no duplicated Actor and resolve the exact Shot owner only in the
Production payload; system messages have an optional explicit same-Project
Actor. Direction changes that clear an Actor must be one atomic prepared
collection write. Sample Actors remain Design fixtures and must never repair a
persisted Production message.

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
