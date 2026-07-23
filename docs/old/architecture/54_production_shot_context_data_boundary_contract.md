# Production Shot Context Data Boundary Contract

Status: normative.

This document governs the desktop read boundary used to present and validate
the inherited Production context of a Shot and its Screens. It extends
contracts 26, 33, 36, 40, 41, 51 and 53 without changing Shot, Actor, Device or
Theme persistence.

## 1. Objective

Production context has separate read and policy owners:

```text
validated current Shot, Actor, Device and Theme data
→ ProductionShotContextDataSource
→ explicit current ids, names and Theme mode
→ ProductionShotContextService
→ context validity, navigation availability and Preview context metadata
```

The data source reads the exact stored route. The service explains whether the
route is usable. Neither responsibility belongs to `MainWindow`, the Preview
bridge or the renderer.

## 2. Data-source ownership

`ProductionShotContextDataSource` may supply only:

- the Shot's explicit `owner_actor_id`;
- the selected Actor's exact Project, display name, default Device id and
  default Theme id through `ActorPreviewDataSource`;
- the referenced Device display name;
- the referenced Theme display name and current explicit `defaultMode` value.

It returns narrow immutable values. It must not choose an Actor, Device, Theme
or mode; catch a missing reference and substitute another record; construct UI
controls; decide whether a tree node is enabled; execute SQL; repair current
data; or infer context from App, Module, Variant, labels, types or ordering.

The source composes the existing current facade/domain routes during the
repository transition. It is not a repository.

## 3. Context-service ownership

`ProductionShotContextService` owns:

- validation that a Shot has an owner Actor;
- validation that the Actor exists and declares both default references;
- validation that the referenced Device and Theme resolve;
- precise invalid-context messages for the Production context strip and
  Preview error surface;
- whether a Shot may expose its Screen children;
- whether a Screen navigation node is enabled under its Shot.

The service consumes only `ProductionShotContextDataSource`. It must not accept
`SpikeDatabase`, query repositories or duplicate the stored reference route.
The context strip remains a presentation-only consumer of the resolved context.

## 4. Preserved behavior

- Shot ownership remains an explicit stable Actor id and can never be blank in
  current persisted data.
- Actor default Device and Theme remain explicit stable ids.
- Module Instances inherit external Production context only from their Shot.
- A Module's explicit appearance mode may override presentation of the
  inherited Theme mode; the context service does not author that override.
- Invalid context blocks the affected Preview/Screen navigation and remains a
  visible error; no fallback Theme or Device is selected.
- Payloads, Runtime Inputs, Overrides, Variants, keyframes and durations are not
  modified by reading context.
- `MainWindow` only instantiates and delegates the service for shell navigation.

## 5. Enforcement and tests

Architecture enforcement must verify:

- `ProductionShotContextService` contains no `SpikeDatabase` dependency;
- `ProductionShotContextDataSource` is its declared database boundary and
  contains no SQL;
- the data source composes the typed Actor context boundary;
- both navigation and Preview construct the same service/data-source route;
- this contract is linked from `AGENTS.md` and the architecture index.

A disposable-database test must compare the resolved Actor, Device, Theme and
mode with their exact current records, exercise Shot/Screen navigation
availability and prove the read leaves every database byte unchanged.

## 6. Out of scope

This phase does not change the Production context strip, breadcrumb layout,
selection behavior, Actor replacement, Theme inheritance, Module appearance
mode, tables, parity data, assets, Preview payloads or rendering.

## 7. Forbidden shortcuts

- selecting the first Actor, Device or Theme in a Project;
- deriving context from Module, App, Variant, name, family or position;
- returning `inherit`, Light or Dark for a missing Theme mode;
- swallowing a missing reference and enabling the Screen;
- moving tree or Preview presentation into the data source;
- rewriting Shot, Actor or Module Instance data while resolving context;
- adding Production-context branches to the bridge or renderer.
