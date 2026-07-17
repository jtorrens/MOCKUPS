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
