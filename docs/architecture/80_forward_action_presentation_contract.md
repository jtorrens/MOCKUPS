# Forward Action Presentation Contract

Status: normative.

This contract governs the visible action used to expose or stop exposing an
embedded Component input across its explicit parent Runtime boundary.

## 1. Shared presentation owner

Every Forward action uses `EditorForwardVisuals`. The dictionary Component
input binding control owns when the action is present and delegates its visual
construction to that shared surface. Editors and collection controls must not
create local Forward glyphs, buttons, tooltips or highlighted states.

The canonical action has:

- a compact right-pointing triangular indicator;
- a 10-unit indicator inside the existing 30-unit action target;
- an outlined inactive state using the current foreground;
- a filled active state using the existing Override/Forward highlight brush;
- the existing inactive and active accessible descriptions and tooltips;
- the same right-side field-action alignment at every embedded boundary.

The action target does not shrink with the indicator. Compact presentation must
not weaken pointer or accessibility affordance.

## 2. Behavior remains explicit

This is presentation only. The shared surface does not decide whether an input
can be forwarded, read or write JSON, construct a forwarding definition, infer
a boundary, or handle confirmation.

Forward eligibility and state continue to come from the exact declared Runtime
input and its explicit `$forwardedInputs` document. Activating and removing
Forward continue through `RuntimeInputForwardingContract` and the generic
dictionary commit path. Nothing is inferred from a field name, label, kind,
component type, hierarchy or position.

## 3. Preserved boundaries

- stable ids and complete Component Variant references do not change;
- forwarding remains explicit across every boundary;
- Variant values and local Overrides retain their existing ownership;
- stopping Forward retains its confirmation flow;
- persisted payloads, Preview resolution, bridge and renderer do not change;
- no database, seed, migration or asset change is required.

## 4. Enforcement

Architecture checks require the shared right-pointing geometry, compact
indicator size, unchanged action target, active/inactive styling and accessible
descriptions. They also require the dictionary Component input binding control
to consume the shared action and reject its retired local upward-pointing
triangle.

A desktop test verifies the canonical geometry, shared dimensions and
accessible descriptions for both states without requiring a graphical test
platform. Architecture enforcement covers active/inactive styling. Manual
review checks the symbol on several embedded Component inputs at normal and
compact panel widths.
