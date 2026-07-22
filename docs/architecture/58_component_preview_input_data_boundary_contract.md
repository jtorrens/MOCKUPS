# Component Preview Input Data Boundary Contract

Status: normative.

This document governs the remaining persisted-data reads used by isolated
Component and Module Test Values, embedded Preview actions and their playback
cadence. It extends contracts 23, 24, 34, 36, 51, 53 and 55 without changing
Runtime Input, forwarding, action or persistence documents.

## 1. Objective

Isolated Preview input state, persisted lookup and action interpretation have
separate owners:

```text
validated current Project and Component Variant data
→ ComponentPreviewInputDataSource
→ exact Project fps, complete Variant config and effective runtime contract
→ ComponentPreviewInputSession / ComponentPreviewActions
→ session-only Test Values, actions and frame requests
→ complete Preview payload resolution
```

`RuntimeInputOptionsDataSource` continues to supply exact Component Variant
options. `ActorPreviewDataSource` continues to supply Actor reference data.
This phase must reuse those boundaries rather than widening the new source.

## 2. Data-source ownership

`ComponentPreviewInputDataSource` may supply only:

- the exact default frame rate of an explicit Project id;
- the complete current config of an explicit full Component Variant reference;
- the complete effective runtime contract of that same explicit reference;
- strict validation of a full Component Variant reference against one explicit
  Project and declared component type.

It must not select a Project, Component Class or Variant; shorten a Variant
reference; choose Default after a boundary already exists; parse Runtime Input
definitions or actions; mutate Test Values; run playback; create controls;
execute SQL; repair current documents; or infer behavior from a name, label,
type spelling, tree position or collection index.

The source composes current facade/domain operations during the repository
transition. It is read-only and is not a repository.

## 3. Input-session ownership

`ComponentPreviewInputSession` retains:

- session-only isolated Test Values and their scope keys;
- explicit Runtime Input and structured collection interpretation;
- application of Actor record-reference preview values;
- action snapshots and Play/Restore state;
- deterministic action time derived from the declared contract duration;
- conversion of Project fps through the common Preview playback timing policy;
- applying transient values to a complete payload before resolution.

The session may receive `SpikeDatabase` only as a construction/composition
parameter while the staged desktop shell creates its typed sources. It must not
retain a database field or perform direct database reads.

Test Values remain transient inspection fixtures. They must never be written
to a Module Instance merely by changing a control, playing or restoring an
action, or switching Preview context.

## 4. Action interpretation

`ComponentPreviewActions` is a pure action-contract interpreter. When an
explicit structured item references a concrete embedded Component Variant, the
caller supplies the exact effective child runtime contract through the typed
data boundary. The action interpreter must not accept `SpikeDatabase`, query
persistence, resolve short ids or choose a child Variant.

The Runtime Inputs editor and the isolated Preview session must use the same
typed child-contract route so action availability cannot diverge between the
editor and Preview.

Contract 60 owns the broader editor-owner document route and concrete embedded
Component selection metadata. It reuses this contract's exact Component config
source rather than duplicating that read in the Runtime Inputs editor.

## 5. Preserved boundaries

- Stable ids and complete `componentClassId::variant::variantId` references are
  preserved.
- A new authored Component boundary still starts with its explicit Default
  Variant; current references never fall back to Default.
- Forwarding and local Overrides remain explicit and are not inferred here.
- Runtime Input scalar controls still follow the dictionary route.
- Structured collections preserve stored stable item ids and ordering.
- Action timing and requested frames remain contract-owned and deterministic.
- Resolution completes before the generic bridge and renderer.
- The renderer receives resolved frame data and runs no timer or action logic.

## 6. Enforcement and tests

Architecture enforcement must verify:

- `ComponentPreviewInputDataSource` is the declared database boundary and
  contains no SQL;
- `ComponentPreviewInputSession` retains no general database field and performs
  no direct database method call;
- `ComponentPreviewActions` has no `SpikeDatabase` or Data-layer dependency;
- both the input session and Runtime Inputs editor supply embedded action
  contracts through `ComponentPreviewInputDataSource`;
- Variant options continue to use `RuntimeInputOptionsDataSource`;
- this contract is linked from `AGENTS.md` and the architecture index.

A disposable-database test must compare Project fps, complete Component Variant
config, complete effective runtime contract and strict reference validation
with the exact current facade values and prove that all reads leave the database
byte-for-byte unchanged.

## 7. Out of scope

This phase does not redesign Test Values, actions, Play/Restore, collection UI,
forwarding, Overrides or Preview transport. It does not fix keyframe dragging,
change playback cadence, add Render Mode/export, alter tables, migrate data or
change parity assets.

## 8. Forbidden shortcuts

- accepting a short or class-only Component reference;
- selecting a Variant by name, label, ordering or array position;
- treating Test Values as persisted Production payload;
- reading Component contracts directly from an editor or action parser;
- adding component-specific action behavior to the bridge or renderer;
- using a missing contract as an empty action list;
- caching derived action time or Project fps as persisted truth.
