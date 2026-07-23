# Production Authoring Context Presentation Contract

Status: normative.

This contract governs the visible Production authoring context for Episodes,
Shots and Screens. It changes presentation and session coordination only. It
does not change Production data, Preview payloads, timeline ownership or
rendering.

## 1. Production hierarchy is the primary context

The editor and Preview use the exact `Episode › Shot › Screen` path already
provided by the explicit tree. A breadcrumb must not reconstruct this path from
names, types, order or position.

Episode and Shot headers do not repeat their own identity in a second context
strip. A Screen header supplements its breadcrumb with the exact:

- Module name;
- Module Variant name;
- effective duration in frames;
- transition type.

`ProductionScreenPresentationDataSource` is the read-only boundary for those
four values. It receives one exact Module Instance id, composes current facade
and common timeline operations, performs no SQL and never writes or repairs
data. The editor header owns only their display.

## 2. Persisted Screen data is named as payload

A Module Instance's editable Runtime values are persisted with that exact
Screen instance. The visible tab and section title are `Screen Payload`, with
the explanation `Saved with this Screen instance.`.

`Runtime API` remains implemented but is not presented in the current
Production UI. `Screen Payload` shows the persisted values directly above the
resolved Preview. This co-location does not make the values temporary, change
their owner, keys or write path, or introduce a second payload surface in the
central editor.

Design Test Values retain their separate temporary Preview ownership from
contract 83.

## 3. Preview context and timeline scope are explicit

The right panel is titled `PREVIEW`. Its resolved surface is labelled
`Current frame`, not `Design`.

When a Shot is selected, the current-frame context identifies
`Active Screen: <name>`. Selecting a Screen may continue to show its exact name
without the active prefix because the breadcrumb already fixes that scope.

The shared playhead remains absolute in Shot time internally. Its visible scale
is labelled:

- `Shot timeline` when a Shot is selected;
- `Screen local timeline` when a Screen is selected.

No frame conversion, duration policy, keyframe origin or playback behavior
changes.

## 4. Active Screen is independent from selection

The Screen containing the current absolute Shot frame is marked in:

- the Production navigation tree;
- the selected Shot's Modules card.

The mark is a presentation indicator only. It must never select the Screen,
open its editor, move the playhead, persist state or change the expanded tree.
The user's current Episode, Shot or Screen selection remains authoritative.

`ProductionScreenPlaybackState` is the single owner of the mapping from exact
ordered Screen frame ranges and an absolute Shot frame to the active stable
Screen id. Consumers may project that result but must not reproduce the range
formula.

The indicator updates from the existing shared playback/frame state. Its
visibility is session-only and is not written to window state, payloads or the
database.

## 5. Episode has no fabricated Shot context

An Episode is a Production container and has no direct image. Its empty Preview
asks the user to select a Shot or Screen and may navigate to the first explicit
renderable descendant.

When no Shot is selected, Preview Setup shows the exact Episode breadcrumb but
does not display placeholder Actor, Device, Theme or Mode chips. Values such as
`No Shot selected` are not valid inherited context and must not be presented as
if they were data.

## 6. Preserved boundaries

- stable ids and complete Module Variant references do not change;
- Runtime forwarding and local Overrides remain explicit;
- Screen payload documents and Runtime API contracts do not change;
- keyframes remain relative to their stable owner;
- the absolute Shot playhead and Screen-local authoring projection remain
  distinct;
- payload preparation still resolves the complete Production context before
  Preview;
- bridge and renderer remain generic;
- startup remains read-only and no migration, seed or parity-data change is
  introduced;
- `MainWindow` only coordinates Preview state with navigation refresh and
  constructs no Production field or timeline rule.

## 7. Enforcement and review

Automated checks must cover:

- exact read-only Screen presentation values;
- half-open active Screen frame boundaries and stable ids;
- architecture boundaries and a clean desktop build;
- unchanged committed database bytes.

Manual review covers Episode, Shot and Screen selection; breadcrumb and summary
content; Screen Payload terminology; Shot versus Screen timeline labels; active
Screen indicators during scrubbing and Play; and compact Preview widths.

Lifecycle action presentation follows
`85_consistent_lifecycle_action_presentation_contract.md`.
Screen Payload placement follows
`86_production_preview_payload_presentation_contract.md`.
