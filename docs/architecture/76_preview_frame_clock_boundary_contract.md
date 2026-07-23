# Preview Frame Clock Boundary Contract

Status: normative.

This contract closes the duplicate `localFrame` decision deferred by contract
74. It governs Production payload construction, recursive Component
composition, owner-timeline resolution and the module-to-renderable boundary.

## 1. Two clocks with different owners

`DesignPreviewPayload.localFrame` is the frame of the **current resolver or
renderable boundary**. At the root Module Instance boundary it is the selected
Screen-local frame. A parent may explicitly rebase it when entering an
activated child State or another nested visual owner.

`instance.context.screenFrame` is the **root selected Screen-local frame**. It
is a non-negative integer produced once when the absolute Shot playhead crosses
the selected Screen boundary. It remains unchanged through every recursive
Component payload.

At the root Module Instance boundary:

```text
payload.localFrame == instance.context.screenFrame
```

Inside an embedded boundary the values may intentionally differ:

```text
instance.context.screenFrame = 20
payload.localFrame = 5
```

This means the Shot is at frame 20 of the Screen while the current child has
been active for 5 frames.

## 2. Exact current shape

A Module Instance Preview requires an object `instance.context` containing an
exact non-negative integer `screenFrame`. The Production factory continues to
carry its exact `shotId` and `moduleInstanceId` in that same context under the
existing payload and session contracts.

The retired `instance.context.localFrame` member is invalid. Readers must not
accept both names, copy one into the other or use a missing root context as
frame zero.

An isolated Design Component or Module has no Production Screen context. Its
session-only `payload.localFrame` is therefore its one inspection clock. This
is a distinct payload kind, not a fallback for an incomplete Module Instance.

## 3. Factory and root-boundary ownership

`DesignPreviewPayloadFactory` owns the single conversion:

```text
absolute Shot frame
→ selected Screen and Screen start
→ screenFrame
→ root payload.localFrame + instance.context.screenFrame
```

The generic Module renderable boundary validates equality before owner routing.
Recursive child routing preserves `instanceJson` and may change only
`payload.localFrame` when the owning parent explicitly rebases the child.

Module-declared runtime frame inputs, such as Conversation's current authored
frame field, remain values in the effective runtime contract. They do not
replace either generic clock and are not consulted as a compatibility source.

## 4. Resolver and renderer ownership

The complete root owner timeline consumes `instance.context.screenFrame`.
Conversation, Component Stack and structured collection animation therefore
keep resolving Screen-owned and target-owned keyframes on the root Screen
scale, even after a child payload has been locally rebased.

Component renderables may consume `payload.localFrame` for the current child's
already-resolved visual interval. The bridge and web renderer receive only
resolved frame state and must not choose between clocks, reconstruct a Screen
origin or run their own timer.

Clock access is provided by one shared Preview frame-context helper. Concrete
Component or Module names must not appear in that helper.

## 5. Migration

The transient Production instance envelope changed once from:

```json
{"context":{"localFrame":20}}
```

to:

```json
{"context":{"screenFrame":20}}
```

No persisted SQLite document changed. Tests and diagnostics were updated in
the same phase, and no dual reader or migration routine remains.

## 6. Enforcement

Architecture enforcement must require:

- `screenFrame` production payload construction and Shot-to-Screen projection;
- equality validation at the root Module Instance boundary;
- shared root-Screen-frame reads in timeline-aware resolvers;
- recursive child payload rebasing through `payload.localFrame` only;
- complete rejection of active `context.localFrame` reads and writes;
- tests where root `screenFrame` and embedded `localFrame` intentionally
  diverge without changing owner-timeline results.
