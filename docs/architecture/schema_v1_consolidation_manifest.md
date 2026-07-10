# Schema v1 Consolidation Manifest

Status: proposed canonical baseline after checkpoint
`pre-schema-v1-consolidation` / `42e4a047`.

This document defines the desktop editor data model that will be copied into a
new database and then become schema v1. It is intentionally independent from
the historical React persistence path.

## Scope

Schema v1 is the authoritative database for the Avalonia editor, the current
component resolvers and the desktop web preview. It is not an additive
migration of the old model.

The canonical hierarchy is:

```text
Project
  -> Episode
    -> Shot
      -> ordered ModuleInstance slots
```

`ModuleInstance` is the only concrete module runtime entity. There is no
Screen Instance layer in schema v1.

## Canonical Tables

The new database contains only the tables currently used by the desktop editor:

```text
projects
episodes
shots
apps
modules
module_instances
palette_colors
devices
actors
production_fonts
icon_themes
render_presets
component_classes
themes
editor_layouts
```

`module_instances` stores module reference, duration, transition declaration,
content, behavior, animation and metadata. It does not store actor, device,
theme, mode or device state. Those values resolve from shot/project context.
Shots store a nullable `fps_override`; a null value inherits `projects.default_fps`.

Schema v1 begins with `PRAGMA user_version = 1`. The existing desktop DB has
`user_version = 0`; its column-normalization history must not become the new
runtime startup path.

## Data Preserved By The Converter

The one-time converter must preserve all rows and stable ids in the canonical
tables, plus all current JSON payloads after normalizing them to the shapes
below:

- component variants use full `componentClassId::preset::presetId` references;
- `media` replaces the historical `video` component type;
- icon bar slots use current inline/full-screen slot names;
- motion uses `fade`, never the earlier `opacity` key;
- status and navigation are `component_classes` variants, not separate records;
- theme status/navigation references point to component variants;
- system-bar colors live in their own component variants, not in theme tokens;
- render preset references use the current ids;
- module instances use the direct Shot -> ModuleInstance relationship.
- project media roots are project-relative whenever the source root is inside
  the project, so the DB remains portable between Mac, PC and packaged copies.

The converter must report and stop on ambiguous references. It must not invent
plausible data for missing required current-model values.

## Compatibility To Extract From Startup

These operations are useful only while importing an older desktop DB. They move
into the offline schema-v1 converter and must not run during normal startup:

| Source compatibility | Canonical v1 result |
| --- | --- |
| `screen_instances` rows | `module_instances` rows; retain content, behavior and animation; discard duplicated context |
| `video` component class/config | `media` class/config and its variants |
| short or legacy preset fields | full component variant slot reference |
| old media icon row slots | current inline/full-screen icon bar slots |
| motion `opacity` | motion `fade` |
| theme system-bar tokens | status/navigation component variant fields |
| vertical legacy render preset id | current render preset reference |
| additive `Ensure*Columns` history | tables created complete from the v1 schema |

## Runtime Rules After Cutover

The editor startup path may:

- check the v1 schema version and required tables;
- seed an empty development database directly in v1 shape;
- validate current data and display explicit diagnostic errors;
- use UI parsing safeguards while the user is editing;
- use visibly diagnostic rendering for an already-resolved invalid paint node.

It may not:

- alter table shape or normalize historical JSON during ordinary startup;
- read legacy tables, old field names or short preset ids;
- supply plausible fallback values for missing current-model data;
- preserve old status/navigation records alongside component variants.

## Documentation Classification

The documentation reorganization is a later file-move phase. The intended
classification is already fixed here so that current work has one reference.

### Normative After v1

- editor shell non-negotiables;
- editor modernization roadmap;
- embedded component composition contract;
- desktop preview component architecture;
- component migration status;
- shot/module-instance contract;
- PC parity validation;
- this consolidation manifest;
- current data-model and target-system documents once rewritten to the direct
  Shot -> ModuleInstance model.

### Historical Reference

- React runtime/resolver/schema documents;
- Visual IR proposals and handoffs;
- old Screen Instance, Screen Template and module-theme-config decisions;
- completed audits, exchange handoffs and superseded implementation plans;
- `PROJECT_STATUS.md`, once its useful current-state material is extracted.

Historical material remains versioned and readable but will move under a
clearly named archive path. It must not be linked as a normative source from
the active architecture index.

## Audit Findings At The Checkpoint

Desktop DB inventory at the checkpoint:

```text
projects: 1                 modules: 1
episodes: 3                 module_instances: 1
shots: 5                    component_classes: 15
apps: 2                     themes: 2
actors: 3                   devices: 6
icon_themes: 6              production_fonts: 4
palette_colors: 24          status_bars: 0
navigation_bars: 0
```

The empty `status_bars` and `navigation_bars` tables are legacy physical
tables. They are omitted from schema v1.

The separate TypeScript `audit:current-model` currently fails while replaying
its historical v41 migration because of a duplicate component-class unique
constraint. That route is outside the desktop v1 scope and is evidence for
keeping React historical persistence isolated rather than reviving it as a
desktop dependency.

## Acceptance Criteria For The Next Phase

The schema-v1 generator is accepted only when it creates a parallel database
that has:

- exactly the canonical table set above;
- `user_version = 1`;
- matching canonical row counts and ids;
- no legacy physical tables;
- no startup migration required to open it;
- valid component variant references and JSON contracts;
- matching assets available at their committed paths.
