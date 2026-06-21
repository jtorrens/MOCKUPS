# Architecture decisions

## D001 — Production is the root entity

Status: accepted

Production is the root scope for resources, reusable presets, actors, devices, themes, assets, data, and shots.

Implications:
- All reusable resources should belong to a production.
- Shots always belong to a production.

## D002 — Shot is the central render unit

Status: accepted

Rendering starts from a production and shot, not from a particular screen data type.

Implications:
- The primary render operation is `renderShot(productionId, shotId, frame)`.
- A shot may compose multiple screen instances.

## D003 — Chat is only one screen type

Status: accepted

Chat uses the same screen-instance and module model as lock screens, notifications, calls, home screens, custom apps, and future types.

Implications:
- Chat-specific entities must not define the root architecture.
- New screen types should not require restructuring shots.

## D004 — SQL for stable relationships, JSON for flexible config

Status: accepted

SQL stores stable, queryable identities and relationships. JSON stores evolving visual props, tokens, metrics, payloads, transforms, and module configuration.

Implications:
- Integrity-critical links use SQL foreign keys.
- Module-specific configuration can evolve without excessive schema churn.

## D005 — Visual modules do not access the database directly

Status: accepted

Visual modules consume resolved inputs and remain independent of persistence.

Implications:
- Modules do not query SQL or fetch production data.
- Modules can be previewed and tested with fixtures.

## D006 — Resolvers create resolved props

Status: accepted

Resolvers combine relational data and JSON configuration into self-contained props for visual modules.

Implications:
- Data loading and default/override precedence live outside modules.
- Preview and final render can share identical resolved inputs.

## D007 — ShotBuilder composes screens but does not draw them

Status: accepted

ShotBuilder handles timing, transforms, layer order, and composition; visual modules draw screen UI.

Implications:
- Screen-level visual details remain encapsulated in modules.
- Composition logic remains independent of screen type.

## D008 — Modules receive props + frame and return renderables

Status: accepted

Every visual module follows a frame-addressable input/output contract.

Implications:
- Modules can animate without hidden global state.
- Renderer adapters can consume a consistent renderable result.

## D009 — The renderer should be frame-based and deterministic

Status: accepted

The same resolved data and frame must produce the same visual output.

Implications:
- Rendering must not depend on wall-clock time or uncontrolled external state.
- Preview and final output should share rendering logic.
