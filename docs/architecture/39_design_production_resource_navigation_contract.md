# Design and Production Resource Navigation Contract

Status: normative.

This document fixes the current workspace ownership and left-navigation shape
for reusable definitions and concrete production resources. It specializes the
direction in document 27 and the contextual Usage rules in document 38 without
changing SQLite ownership or record identity.

## 1. Workspace meaning

`Design` owns authored definitions used to build the reusable visual system:

- Apps, Modules and Module Variants;
- Component Classes and Component Variants;
- Themes;
- Palette Colors;
- Icon Themes.

`Production` owns the assembly of a concrete production and the resources the
Shot workflow selects directly:

- Episodes, Shots and Screens/Module Instances;
- Actors;
- Devices;
- Production Fonts;
- Render Presets.

This is UI and functional ownership. Current rows remain explicitly owned by a
Project in SQLite. No global/system table or implicit cross-Project lookup is
introduced.

## 2. Production navigation

Production exposes two peer cards:

```text
Production
├─ Episodes
│  └─ Shots
│     └─ Screens
└─ Production data
   ├─ Actors
   ├─ Devices
   ├─ Production Fonts
   └─ Render Presets
```

`Production data` is one shared navigation card. Its four resource groups are
internal expandable sections using the standard hierarchical navigation rows;
they are not four top-level cards and do not introduce nested card elevation.
The Production Data card owns Actors, Devices, Production Fonts and Render Presets.
Selecting or navigating to a resource expands the owning card and group,
reveals the exact stable node and opens its editor.
Each internal group retains its standard explicit Add action.

Themes remain in Design even though Actors select a Theme. Render Presets remain
in Production Data even when the same authored values are commonly copied to
another Project.

## 3. Usage scope and destination

Usage scope belongs to the source owner of an edge, not to the resource being
inspected. Therefore:

- an Actor referencing a Device, Theme or Palette Color is Production Usage;
- a Theme referencing a Production Font remains Design Usage;
- a Shot referencing an Actor or Render Preset is Production Usage.

The Usage card and blocked-delete links activate the source scope, not the
target resource's workspace. Scope and destination come from typed edge data;
labels must never be reinterpreted to choose a workspace.

## 4. Future Project duplication

Design definitions and Production Data may be reused between Projects, but the
current model does not make them live global records. Future Project duplication
must present an explicit per-category choice such as:

```text
copy current records | regenerate from current seeds | create empty
```

That future workflow must:

- declare the policy per resource category instead of inferring it from names,
  types, order or position;
- create the target Project through an explicit maintenance/scaffolding flow;
- allocate target ids deliberately and maintain an explicit old-to-new id map;
- rewrite every copied foreign key, full Component Variant reference and full
  Module Variant reference through that map;
- preserve complete current JSON and Variant envelopes;
- fail on an unresolved reference or unavailable seed route;
- never fall back to records from another Project at normal startup or runtime.

This document records the future decision surface only. It does not authorize
or implement Project duplication.

## 5. Shell and persistence boundaries

The complete tree may retain structural grouping nodes, but workspace placement
comes only from shared navigation metadata. `MainWindow` coordinates the active
workspace, expansion and selection; it must not special-case Actor, Device,
Production Font or Render Preset.

Moving a resource between workspace surfaces does not migrate its table, ids or
stored documents. Repository ownership, read-only startup and explicit
migration rules remain governed by contracts 33, 36 and 37.

## 6. Enforcement and tests

Automated checks must preserve:

- `Episodes` and `Production Data` as the only Production top-level cards;
- the exact four ordered Production Data groups;
- Themes in Design;
- Actor-owned Usage edges classified as Production;
- workspace-aware Usage navigation to an Actor such as `actor_alex_b`;
- unchanged SQLite data for this navigation-only phase.
