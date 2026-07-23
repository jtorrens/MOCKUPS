# MOCKUPS current architecture

Status: normative.

This directory contains the complete active documentation set. Each rule has
one canonical owner. When implementation changes an architectural or
functional rule, update its owning document in the same revision.

## Mandatory documents

- `system_overview.md`: application purpose, domains, layers and dependency
  direction.
- `data_persistence.md`: SQLite ownership, repositories, JSON roots,
  validation and explicit maintenance.
- `design_system.md`: Apps, Modules, Atoms, Components, Themes and complete
  Variants.
- `production.md`: Episodes, Shots, Screens, production context, payload and
  message ownership.
- `editor_dictionary.md`: metadata-driven editors, `FieldDefinition`,
  `ValueKind`, cards, controls and session state.
- `composition_runtime.md`: embedded Components, slots, Overrides, forwarding,
  stacks and structured collections.
- `animation.md`: temporal ownership, durations, keyframes, frame clocks and
  playback authoring.
- `preview_rendering.md`: payload preparation, resolver, renderable, bridge and
  renderer boundaries.
- `resources_assets.md`: palette, Themes, Actors, Devices, fonts, icon themes,
  wallpaper and media assets.
- `ux_ui.md`: current Design/Production navigation and interaction model.
- `development_workflow.md`: definition lifecycle, scaffolding boundaries,
  migrations and contribution procedure.
- `validation.md`: architecture checks, automated tests, parity database and
  manual review.

## Authority rule

The documents above describe only the current system. Phase reports, audits,
handoffs and superseded specifications are stored under `docs/old` and are
subject to the prohibition in `docs/README.md` and `AGENTS.md`.

An active document must not link to or derive a decision from `docs/old`.
