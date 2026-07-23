# Preview Payload Data Boundary Contract

Status: normative.

This document governs the desktop data boundary used to construct Design and
Production Preview payloads. It extends contracts 24, 26, 34, 36, 47 and 48
without changing payload or persistence shapes.

## 1. Objective

Preview payload construction has two explicit desktop owners:

```text
validated current database
→ DesignPreviewPayloadDataSource
→ prepared typed Preview source records
→ DesignPreviewPayloadFactory
→ complete DesignPreviewPayload
→ web Preview resolver/renderable route
```

The data source reads exact current context. The factory constructs the
effective payload. Neither responsibility belongs to `MainWindow`, the bridge
or the renderer.

## 2. Data-source ownership

`DesignPreviewPayloadDataSource` is the only `SpikeDatabase` dependency of the
payload factory route. It owns:

- exact Theme selection for Design or Production context;
- Shot owner Actor Theme and Device lookup;
- Theme tokens, palette maps, icon theme, media root and Production Font faces;
- loading current Component Class or selected Component Variant documents;
- validating full embedded Component Variant references before Preview;
- loading current Module or selected Module Variant plus its App context;
- loading the effective Module Instance Variant/runtime documents and Shot
  frame rate/owner Actor;
- presenting ordered Shot slots with contract-owned effective durations;
- resolving Actor record references into the established Preview input shape.

It returns narrow immutable source records. It must not return a general
database handle, expose SQL, repair current data, choose a fallback owner or
construct final renderables.

The service composes existing facade/domain operations during the repository
transition. It does not become a repository and must not duplicate table SQL.

## 3. Factory ownership

`DesignPreviewPayloadFactory` owns:

- routing the selected renderable tree-node kind to its payload shape;
- applying generic Runtime Input forwarding;
- separating isolated Design Test Values from persisted Production content;
- materializing one complete effective `runtimeContractJson` envelope;
- resolving action durations from owner config and Theme motion tokens;
- selecting the active ordered Screen for a Shot frame;
- converting the absolute Shot playhead to the selected Screen-local frame;
- publishing that root frame as both `payload.localFrame` and
  `instance.context.screenFrame` before recursive composition;
- attaching prepared Actor values to their explicit runtime references;
- producing the complete payload consumed by the Preview route.

The factory must not accept `SpikeDatabase`, open connections, query
repositories or know SQL. It consumes only the typed data source.

## 4. Preserved boundaries

- Forwarding stays explicit and data-driven.
- Crossing a Component or Module boundary still uses a full concrete Variant
  reference and an explicit Default Variant for new authored boundaries.
- Test Values remain session-only and never become Module Instance payload by
  previewing or pressing Play.
- Module Instance content and animation remain their persisted Production
  authorities.
- Shot selection keeps one absolute playhead; owner-local keyframes remain
  relative to stable owners.
- `instance.context.screenFrame` remains the exact root Screen clock while
  nested renderable boundaries may explicitly rebase `payload.localFrame`.
- The complete runtime owner envelope survives recursive composition.
- Resolvers retain Component/Module semantics and requested-frame state.
- The bridge and renderer remain generic and receive fully prepared data.
- `MainWindow` remains shell-only.

## 5. DTO and resource rules

Payload source records contain only values required by payload construction.
Adding a field requires a concrete payload consumer; a catch-all settings or
database property is forbidden.

`ProductionFontFace` is a top-level Preview resource DTO, not a nested
`SpikeDatabase` implementation type. The Production Font repository still owns
stored rows only; filesystem interpretation and face construction remain
outside persistence as required by contract 42.

Actor record values are delegated to the narrower boundary in contract 53.
`DesignPreviewPayloadDataSource` may compose `ActorPreviewDataSource`; it must
not duplicate Actor field reads or absorb Actor mode, Palette, initials or
media interpretation.

Production Preview controller reads outside payload construction follow
contract 57. They must not be folded into `DesignPreviewPayloadFactory`:
transport fps, Screen ownership, ordered slots and appearance context remain
separate session/timeline inputs around the complete payload boundary.

Isolated Test Values and action reads follow contract 58. Project fps and
embedded Component Variant contracts enter their session through a separate
typed source; they must not be folded into payload construction or persisted as
Module Instance runtime content.

## 6. Enforcement and tests

Architecture enforcement must verify:

- the factory contains no `SpikeDatabase` dependency;
- the data source is the declared database boundary and contains no SQL;
- full Component Variant references are validated in the data source;
- the factory still owns forwarding, effective runtime envelopes and Shot to
  Screen-local frame translation;
- the controller reuses one data-source instance for Preview requests;
- this contract is linked from `AGENTS.md` and the architecture index.

Existing component, Module, Module Instance, Shot, forwarding, animation and
Preview integration tests must run through the new boundary. Strict database
validation and the byte-level hash proof remain required.

## 7. Out of scope

This phase does not:

- change the TypeScript payload shape or registry contract;
- change tables, JSON documents, Variant references or parity data;
- move owner semantics into the data source;
- finish every future resolver-data extraction from other editor services;
- implement Render Mode, export or scaffolding.

## 8. Forbidden shortcuts

- exposing `SpikeDatabase` through a data-source property;
- letting the factory bypass the data source for one record kind;
- moving forwarding, Test Value or temporal-envelope semantics into a
  repository;
- returning empty documents for missing current data;
- selecting Actor, Theme, Device, Module or Variant by name/order inference;
- adding component-specific logic to the bridge or renderer during this split.
