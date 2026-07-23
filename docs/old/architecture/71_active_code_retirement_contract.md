# Active Code Retirement Contract

Status: normative.

This document governs phase 0B of the architecture cleanup. It extends the
ownership and enforcement rules in contracts 24, 33, 34, 36, 49, 50, 51 and
68 without changing current behavior, persisted data, Preview output or UX.

## 1. Objective

Active code may be retired only when it has no current consumer and performs no
required validation, registration, serialization or side effect. Dead-code
removal reduces obsolete surface area; it must not become a redesign, owner
move, compatibility migration or behavioral simplification.

A similarly shaped implementation that remains in use is not dead code. It is
a possible duplication and belongs to phase 0C after its semantic owner is
identified.

## 2. Evidence required before removal

Each candidate must be checked across every applicable route:

- direct calls, imports, construction and dependency registration;
- Avalonia XAML names, bindings, styles, converters and event handlers;
- manifest and registry dispatch by exact stable id;
- reflection, serialization field names and persisted JSON contracts;
- npm, command-line, build, packaging and asset-copy entrypoints;
- tests, architecture enforcement and database creation workflows.

Text search alone is not sufficient. Compiler unused diagnostics are strong
evidence only for bindings whose initialization is also proven free of required
side effects. A removed symbol must not be replaced with a speculative helper
or compatibility wrapper.

## 3. Active and historical scope

The active Avalonia editor, desktop Preview, current scripts, current schema,
current seeds and parity assets are in scope.

Archived React sources, exchange handoffs, historical migration evidence and
the explicitly retained root `legacy:*` commands are historical surfaces. They
are not current runtime authorities, but they must not be deleted merely
because active code does not call them. Their retirement requires a separate
repository-history decision.

Materialized historical migrations needed to create or explain the current
database are not dead runtime code. Normal startup must still contain no
migration or repair route.

## 4. Boundary preservation

Retirement must preserve:

- `MainWindow` as shell-only;
- owner-specific resolver and renderable modules;
- route-only registries and a generic bridge/renderer;
- complete current JSON and Variant envelopes;
- explicit forwarding, references and temporal ownership;
- repository ownership and read-only startup;
- dictionary-owned editable scalar fields;
- all parity data and required assets.

Removing an apparently unused parse or validation is forbidden until its owner
or an equivalent required validation is demonstrated. Removing an unused local
calculation is allowed only when it cannot affect output, ordering, errors or
side effects.

## 5. Execution and records

Retire code in small coherent slices. For each slice:

1. record the candidates and evidence in the phase 0B audit;
2. distinguish removed, retained and deferred items;
3. delete the confirmed inactive surface without changing its consumers;
4. run compiler/static checks, focused tests, architecture checks and the full
   validation appropriate to the affected boundary;
5. repeat discovery after the deletion exposes new candidates;
6. commit the validated slice independently.

The phase ends only when a new pass finds no clear active-code candidates. It
does not end by deleting historical evidence or by reclassifying live
duplication as dead code.

## 6. Initial enforcement

The active TypeScript typecheck enables unused-local and unused-parameter
diagnostics. Current desktop Preview source must therefore contain no confirmed
unused binding. The desktop check also rejects C# `IDE0060` unused parameters.
No-op methods, C# members and XAML candidates still require explicit
cross-reference evidence because those checks do not prove their absence by
themselves.

Architecture enforcement must keep this contract in `AGENTS.md` and the
architecture index. Additional negative checks should be added only for
specific retired routes whose return would violate an established contract;
the checker must not ban generic words or broad patterns that have valid uses.

## 7. Forbidden shortcuts

- deleting a public method, DTO, field or serialized key from occurrence count
  alone;
- treating XAML, reflection, manifest or registry consumers as absent;
- removing a required validation because its return value is unused;
- deleting archived evidence or retained legacy commands as runtime cleanup;
- combining dead-code deletion with behavior changes, owner moves or schema
  migration;
- replacing deleted code with a fallback, alias, no-op wrapper or inferred
  default;
- editing the parity database or assets when the retired code did not own a
  current data or asset change.
