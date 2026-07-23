# Cleanup Verification and Baseline Closure Contract

Status: normative.

This contract governs phases 2 and 3 of the cleanup intervention defined by
contract 68. It closes verification and records what remains before the user
accepts the architecture baseline. It does not authorize new features, UX
changes, migrations or decisions deferred by phase 1.

## 1. Enforcement belongs to each cleanup slice

Every architectural cleanup slice must update its focused test and
`check:architecture` enforcement in the same commit as the implementation.
Checks target the exact owner, file, symbol or retired pattern. Generic words,
framework APIs and valid concepts must not be prohibited globally.

The check must require the new route and reject the specific removed route.
It must not merely count files or assume that absence of one spelling proves
the semantic owner is unique.

Tests use complete current documents. A synthetic fixture discovered to omit a
required current member is completed at its factory or owner; production code
must not restore a fallback to keep an obsolete fixture passing.

## 2. Automated Mac closure

Before a cleanup phase is reported as automatically verified, the same clean
checkout must pass:

- the complete Preview and desktop test command;
- strict TypeScript checking and unused desktop-code checking;
- architecture enforcement;
- the desktop build with no warnings or errors;
- explicit read-only validation of the committed desktop database;
- `git diff --check`;
- a before/after hash proving validation and tests did not modify the database.

The committed parity database and required assets change only for an explicit
migration or intended visual-data change. A validation phase must not update
them incidentally.

Generated build output is not a parity artifact and is never committed merely
to demonstrate verification.

## 3. Manual closure is a separate gate

Automated success does not mean that the complete baseline has been accepted.
The latest validated desktop build must still be opened and reviewed against
the real Design and Production flows when the user is available. Windows/PC
parity remains a separate smoke gate for filesystem, assets, fonts, processes,
Preview, editing and Shot playback.

If the user explicitly postpones UI review until cleanup is complete, the app
must not be opened repeatedly after internal slices. The handoff records that
manual review is pending and opens only the final validated build for that
review.

## 4. Deferred decisions are not validation defects

The following phase-1 findings require explicit product/model approval and are
not resolved through a compatibility fallback:

- Text Box and Text Input Bar icon composition requires one explicit migration
  to structured Icon Row items with stable ids, complete Button Variant
  references and local Overrides. Names, types and positions must not generate
  identity or select a Variant.
- `durationInputId` was approved as the declared stable field id and is now
  governed by
  [Action Duration Field Identity Contract](75_action_duration_field_identity_contract.md).
  Readers must not support the retired JSON-key spelling.
- the authoritative relationship between payload `localFrame` and instance
  context `localFrame` must be decided before one source is removed or made a
  fallback for the other.

These items remain documented decisions, not permission to keep expanding the
old path. No code or data migration begins until the user confirms the chosen
contract.

## 5. Baseline acceptance

The cleanup intervention is complete only when:

- phases 0A, 0B, 0C, 1 and automated phase 2/3 evidence are closed;
- every critical/high finding is fixed or explicitly approved as future work;
- the working tree is clean and intended commits are pushed when requested;
- final Design and Production UI review passes on the latest build;
- the PC smoke gate passes or the user explicitly accepts its deferral;
- the user accepts the baseline before scaffolding or new functional expansion.

## 6. Forbidden shortcuts

- weakening a current contract for an incomplete test fixture;
- marking manual UI or PC review complete from automated tests;
- pushing without explicit user direction;
- opening an older build after a later validation commit;
- solving a deferred model decision through dual reads, aliases or inference;
- describing a phase as closed while its intended changes are uncommitted or
  its working tree is dirty.
