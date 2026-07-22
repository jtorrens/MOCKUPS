# Preview Visual Context Data Boundary Contract

Status: normative.

This document governs the desktop read boundary for Preview Device/Theme
selectors, resolved Device frame metrics and Project media-root context. It
extends contracts 24, 33, 36, 37 and 51 without changing Device, Theme or
Project persistence, Preview payloads or renderer primitives.

## 1. Objective

Preview visual resource lookup, shell state and rendering have separate owners:

```text
validated current Project, Device and Theme data
→ PreviewVisualContextDataSource
→ exact options, media root and resolved DevicePreviewMetrics
→ EditorPreviewController session selection/orientation
→ generic web Preview surfaces
```

The data source reads exact current values. The controller owns transient
Preview UI state. Web Preview receives only fully resolved generic metrics.

## 2. Data-source ownership

`PreviewVisualContextDataSource` may supply only:

- ordered Device options for an explicit Project id;
- ordered Theme options for an explicit Project id;
- the exact stored media root for an explicit Project id;
- fully resolved `DevicePreviewMetrics` for an explicit Device id through the
  established Device domain route.

It must not select a Device or Theme; infer a Project from a tree position;
apply portrait/landscape orientation; construct canonical-frame metrics;
resolve payload Theme/Actor context; create controls; execute SQL; repair
Device metrics; or infer a resource from name, family, type or index.

The data source composes current facade/domain operations during the repository
transition. It is not a repository.

## 3. Preview-shell ownership

`EditorPreviewController` retains:

- session-only selected Device, Theme, mode, orientation and scale;
- synchronization of available options after the tree changes;
- reference-image browsing and overlay state;
- portrait/landscape projection of resolved Device metrics;
- canonical-frame inspection mode;
- Preview refresh, playback preparation and presentation orchestration.

It obtains all Device/Theme option lists, media roots and Device metrics through
one reusable `PreviewVisualContextDataSource`. This phase does not define a new
persisted default-selection policy.

## 4. Generic metrics boundary

`DevicePreviewMetrics` is a common fully resolved Preview DTO. It is not a
nested `SpikeDatabase` record and contains no metrics JSON, repository handle,
token or Device inference rule.

`SpikeDatabase.GetDevicePreviewMetrics` may continue to compose strict current
Device settings with `DeviceMetricRules` during the facade transition, but the
web renderer receives only the common DTO. `WebDesignPreviewRenderer` and
`WebPreviewPanes` must not reference `SpikeDatabase`.

Required Device metric paths are finite JSON numbers; numeric strings are not
another current representation. Optional frame/design-guide coefficients may
be absent but fail when present with the wrong shape. The optional
`dynamicIsland` editor group follows contract 37 and is not a renderer
fallback.

## 5. Preserved boundaries

- Device settings remain stored and written by `DeviceRepository`.
- `DeviceMetricRules` remains the shared domain interpreter of current metrics.
- Production Device/Theme context still comes only from the explicit Shot
  owner Actor route governed by contracts 41 and 54.
- Design Preview selector state remains session-only and never rewrites a
  Project, Device, Theme or payload by refreshing.
- Payload resolution completes before Preview.
- The bridge and web renderer remain generic and receive resolved frame data.
- `MainWindow` remains shell-only.

## 6. Enforcement and tests

Architecture enforcement must verify:

- `DevicePreviewMetrics` is a common top-level DTO and is not nested in
  `SpikeDatabase`;
- web Preview surfaces contain no `SpikeDatabase` reference;
- `PreviewVisualContextDataSource` is the controller's declared database
  boundary for options, media root and metrics and contains no SQL;
- the controller does not bypass it for those reads;
- this contract is linked from `AGENTS.md` and the architecture index.

A disposable-database test must compare both option lists, the media root and
resolved metrics with their exact current facade values and prove that all
reads leave the database byte-for-byte unchanged. Existing render integration
tests continue to consume the common metrics DTO.

## 7. Out of scope

This phase does not redesign selector defaults, Preview controls, orientation,
reference overlays, playback routes, Render Mode, Raster export, Device
metrics, tables, parity data or assets.

## 8. Forbidden shortcuts

- selecting a Production Device or Theme from the first option;
- deriving resources from display name, Theme family, Device type or tree
  position;
- exposing Device metrics JSON or a database handle to the renderer;
- rebuilding Device metric formulas in the controller, bridge or renderer;
- reading Project, Device or Theme persistence directly from web Preview code;
- persisting selector or reference-overlay state as payload truth;
- adding Device-specific rendering branches to the generic renderer.
