# Single Semantic Rule Ownership Contract

Status: normative.

This document governs phase 0C of the architecture cleanup. It extends the
ownership and boundary contracts without changing current behavior, persisted
data, Preview output or UX.

## 1. Objective

A domain rule that is currently consumed by more than one surface must have one
semantic owner and one implementation. Editor, payload preparation, timeline,
Preview, playback and future export may coordinate or project that result, but
must not independently reconstruct the rule.

Phase 0C consolidates live duplication. It does not retire inactive code, move
validation ownership, redesign persistence, introduce new abstractions or add
features.

## 2. What counts as a real duplication

Two implementations are duplicates only when all of these conditions hold:

- they represent the same domain rule;
- equivalent current inputs must produce equivalent outputs or failures;
- a future divergence between them would necessarily be a defect;
- their consumers do not intentionally operate at different ownership or time
  boundaries.

Similar names, control flow, JSON traversal, arithmetic or data shapes are not
sufficient evidence. Generic mechanisms may be shared only when they already
express the same contract; phase 0C must not invent a framework in anticipation
of future consumers.

## 3. Owner selection

The definitive owner is the layer that understands the meaning of the rule,
not the layer with the most callers. Existing contracts decide ownership:

- repositories own current row SQL, mapping and prepared complete writes;
- document boundaries own strict current document reads and explicit writes;
- domain contracts and resolvers own Component or Module meaning;
- the common timeline owns owner-relative frame projection and duration rules;
- payload preparation owns complete boundary envelopes and explicit forwarding;
- the bridge owns only generic final-value translation;
- the renderer owns only painting fully resolved primitives;
- the editor shell owns coordination and session presentation state.

If two plausible owners remain, the candidate is blocked until ownership is
decided explicitly. A consolidation must never place domain meaning in
`MainWindow`, a registry, the generic bridge or renderer.

## 4. Required evidence per candidate

Before implementation, the phase audit must record:

- the rule in product terms;
- every active implementation and consumer found;
- the current result and failure behavior of each route;
- the definitive owner and the contract that assigns it;
- the minimum consumer migration;
- the complete alternative route that will be removed;
- parity or regression tests and any focused architecture enforcement;
- persistence, Preview, platform and UX risk.

Candidates must be traced through XAML, manifests, registries, serialization,
scripts, tests and packaging where applicable. If parity cannot be stated
precisely, the candidate remains an observation rather than a refactor.

## 5. Consolidation procedure

One semantic responsibility is consolidated per coherent slice:

1. capture current parity or the intended explicit failure in a focused test;
2. select the existing owner implementation where possible;
3. move every consumer to that owner without changing its public behavior;
4. delete the complete alternative implementation and obsolete wrappers;
5. repeat discovery for newly exposed duplicates;
6. run focused checks, architecture enforcement and the full validation suite;
7. commit the slice independently.

When a current divergence is discovered, phase 0C must not silently choose one
behavior. Record the difference and obtain the required product or ownership
decision before changing it.

## 6. Boundaries that must remain unchanged

Every consolidation preserves:

- stable ids and complete Variant references;
- explicit default Variant selection at a new boundary;
- explicit forwarding and local Overrides;
- complete current JSON envelopes;
- owner-relative keyframes and stable target ids;
- complete frame resolution before Preview;
- a generic bridge and renderer;
- shell-only `MainWindow`;
- dictionary-owned editable scalar fields;
- read-only startup and explicit migrations only;
- separation between Design Test Values and persisted Production payloads.

No compatibility fallback, alias, inferred default or parallel wrapper may be
left behind for safety.

## 7. Separation from later phases

Moving dispersed validation into its owner belongs to phase 1. A live
duplicated rule may include validation, but phase 0C may consolidate it only
when the owner is already unambiguous and the change does not broaden into a
validation redesign.

UX consolidation also remains outside this phase unless duplicate code can be
removed with no visible interaction change. Presentation improvements are
handled by the later UX audit and approved implementation waves.

## 8. Closure

Phase 0C closes when the recorded families have each been consolidated,
retained as intentionally distinct, deferred with a concrete ownership
question or rejected as superficial similarity. A final discovery pass must
find no further clear semantic duplication in the active scope.

The audit must list remaining wrappers or parallel routes explicitly. Closing
the phase does not authorize phase 1 or additional architectural work beyond
the agreed cleanup sequence.
