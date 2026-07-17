# Active Architecture Index

This index identifies the documents that govern current work on the desktop
editor. If an older document conflicts with one listed here, the document in
this index wins.

## Normative Documents

- `editor_shell_non_negotiables.md`: editor boundaries and non-negotiable rules.
- `editor_modernization_roadmap.md`: cleanup and migration order.
- `21_desktop_editor_base_routines_audit.md`: shared-routine ownership.
- `23_embedded_component_composition_contract.md`: recursive component slots,
  variants and overrides.
- `24_desktop_preview_component_architecture.md`: component-to-web-preview
  boundary.
- `25_component_migration_status.md`: current component route and remaining
  functional work.
- `26_shot_module_instance_contract.md`: Project -> Episode -> Shot -> Module
  Instance model.
- `31_structural_stacks_slots_and_module_instances.md`: Component Stack and
  Collection Stack types, slot/State semantics, Module Variant ownership and
  their effective runtime/animation contract inside Module Instances.
- `32_application_architecture_functional_ux_handoff.md`: end-to-end map of the
  current application, tables, resources, authoring/Production flows, preview
  boundaries and the brief for the next architecture/functionality/UX audit.
- `33_persistence_and_migration_contract.md`: ownership and mandatory workflows
  for SQLite creation, read-only startup validation, explicit one-shot
  migrations, current record creation and committed database parity.
- `34_manifest_routing_payload_and_dictionary_contract.md`: canonical Preview
  manifest, route-only registries, explicit payload/forwarding ownership,
  recursive temporal context and exhaustive dictionary/Runtime Input kinds.
- `35_current_json_and_variant_contract.md`: strict persisted JSON roots and
  complete Component/Module Variant envelopes without reader or writer repair.
- `36_desktop_persistence_repository_contract.md`: shared SQLite context,
  focused repository ownership and the compatibility-facade boundary for the
  staged split of `SpikeDatabase`.
- `37_desktop_resource_repository_contract.md`: persistence ownership for
  Palette, Device and Actor resources, with domain interpretation kept outside
  repositories and Usage inference explicitly prohibited.
- `38_explicit_reference_usage_contract.md`: one typed, exact reference-edge
  projection for tree Used state, Usage navigation and deletion protection,
  based only on declared relational and JSON owner contracts.
- `39_design_production_resource_navigation_contract.md`: current workspace
  ownership and navigation for Design definitions, Episodes and the grouped
  Production Data resources, plus constraints for future Project duplication.
- `40_theme_persistence_and_context_contract.md`: Theme row repository
  ownership, strict current Theme documents and the separate Module Instance
  Production Theme-context boundary.
- `41_explicit_shot_production_context_contract.md`: exact Shot owner
  Actor/Theme context, protected Module Instance creation/editing and the
  intentional one-Shot canonical parity project.
- `42_production_font_persistence_contract.md`: focused ownership of
  `production_fonts` current rows while file import, asset lifecycle and
  Preview font-face interpretation remain outside persistence.
- `43_icon_theme_persistence_and_asset_contract.md`: focused ownership of
  `icon_themes` current rows, strict token file references and separation of
  SQLite persistence from manifests, SVGs and generation scripts.
- `44_app_module_definition_persistence_contract.md`: focused ownership of App
  and Module definition rows/current documents while configuration, Variants,
  Runtime forwarding and Module Instances remain in their domain owners.
- `45_editor_session_view_state_contract.md`: process-local continuity of
  editor cards, internal navigation and clamped scroll by exact layout class,
  with stable UI ids and no persistence through window or history state.
- `46_component_class_definition_persistence_contract.md`: focused ownership
  of Component Class current rows and prepared document writes while field
  semantics, Variants, embedded composition and Preview remain domain-owned.
- `47_module_instance_persistence_contract.md`: focused ownership of complete
  Screen/Module Instance rows and prepared writes while Variants, Runtime
  forwarding, structured collections, temporal ownership and Preview remain
  domain-owned.
- `48_shot_persistence_contract.md`: focused ownership of complete Shot rows,
  strict documents and lifecycle copies while Production context, Screen
  timing, duration aggregation and Preview remain domain-owned.
- `49_component_definition_source_contract.md`: canonical current Component
  definition authorities, retirement of the disconnected runtime defaults
  catalog and constraints for future explicit development scaffolding.
- `50_module_definition_source_contract.md`: canonical current Module
  definition authorities, retirement of the disconnected Conversation factory
  and constraints for future explicit development scaffolding.
- `51_preview_payload_data_boundary_contract.md`: typed desktop data access for
  Preview payload construction, separated from forwarding, temporal-envelope
  and Shot-to-Screen selection semantics owned by the payload factory.
- `52_module_instance_timeline_data_boundary_contract.md`: typed read access
  for current Screen/Shot timeline inputs, separated from contract-owned
  duration, owner-origin and keyframe projection formulas.
- `53_actor_preview_data_boundary_contract.md`: typed read access for current
  Actor Preview inputs, separated from Runtime Actor resolution and inline
  avatar presentation semantics.
- `54_production_shot_context_data_boundary_contract.md`: typed read access for
  the explicit Shot owner Actor, Device, Theme and mode route, separated from
  context validity and navigation policy.
- `55_runtime_input_options_data_boundary_contract.md`: typed option lookup for
  Runtime Input dictionary definitions and declared dynamic lists, separated
  from `ValueKind` mapping and collection presentation.
- `56_preview_visual_context_data_boundary_contract.md`: typed Device/Theme
  options, Project media root and resolved common Device metrics for Preview,
  separated from shell selection and generic web presentation.
- `57_production_preview_session_data_boundary_contract.md`: typed Shot fps,
  Screen owner and selected Module Variant data for Production Preview,
  composed with the common ordered timeline source.
- `58_component_preview_input_data_boundary_contract.md`: typed Project fps,
  complete Component Variant config and effective embedded action contracts for
  session-only Preview Test Values.
- `59_module_instance_animation_document_boundary_contract.md`: typed current
  Screen animation documents and explicit complete v2 writes, separated from
  owner-relative animation authoring and common timeline formulas.
- `27_design_production_ux_separation.md`: UX direction for separating design
  system work from shot-oriented production work.
- `26_pc_parity_validation.md`: Mac/PC validation process.
- `schema_v1_consolidation_manifest.md`: active database schema and startup
  rules.

`00_project_vision.md`, `01_data_model.md`, `05_decisions_log.md` and
`15_target_system_architecture.md` remain supporting architecture references.
They must be read alongside the normative documents above when a change touches
their subject. They describe intent and accepted decisions, but never override
the schema-v1, component, preview or shot/module contracts.

## Historical Material

`archive/react-legacy/` contains the archived TypeScript domain, SQLite and
icon-import implementation from the removed React runtime. `docs/exchange/`
contains handoffs, completed tasks, external reviews and historical status
records. Both are useful for visual or behavioral comparison but do not define
active implementation rules.

The following root-level notes are historical reference only and must not be
treated as implementation instructions: `02_render_architecture.md`,
`03_visual_modules.md`, `04_shot_builder.md`, `07_initial_data_schema.md`,
`08_visual_tokens_layout_contract.md`, `09_foundational_module_contracts.md`,
`10_module_theme_configs.md`, `11_ui_css_layers.md`,
`12_editor_encapsulation_contract.md`, `13_keyboard_text_input_audit.md`,
`14_data_model_consolidation_policy.md`, `16_theme_editor_dictionary_audit.md`,
`22_runtime_fallback_audit.md`, `editor_architecture_diagnosis.md`,
`editor_architecture_second_review_questions.md`,
`editor_icon_theme_script_prompt.md`,
`icon_theme_generator_implementation_plan.md` and
`icon_theme_set_script_contract.md`. `schema_v1_candidate_validation.md` is the
historical cutover validation record; it is evidence, not an active workflow.

They remain in place because historical handoffs link to them. Before using one
for visual or behavioral comparison, reconcile it with the active contracts
above. New implementation rules belong in a normative document, never in this
historical set.
