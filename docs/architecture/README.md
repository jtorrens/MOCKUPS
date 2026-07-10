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
- `26_pc_parity_validation.md`: Mac/PC validation process.
- `schema_v1_consolidation_manifest.md`: active database schema and startup
  rules.
- `schema_v1_candidate_validation.md`: cutover validation record.

`00_project_vision.md`, `01_data_model.md`, `05_decisions_log.md` and
`15_target_system_architecture.md` remain supporting architecture references.
They must be read alongside the normative documents above when a change touches
their subject.

## Historical Material

`archive/react-legacy/` contains the archived TypeScript domain, SQLite and
icon-import implementation from the removed React runtime. `docs/exchange/`
contains handoffs, completed tasks, external reviews and historical status
records. Both are useful for visual or behavioral comparison but do not define
active implementation rules.

Older architecture notes not listed above are retained for context. Before
using one as a basis for a change, confirm that it does not conflict with the
current schema-v1, component, preview and shot/module contracts.
