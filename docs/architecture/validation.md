# Validation and enforcement

Status: normative.

## Validation principle

Architecture rules are executable wherever a stable check is possible.
Documentation describes ownership; validation prevents a future change from
silently restoring a second owner, implicit route or invalid persisted shape.

## Standard checks

The complete repository validation is:

```text
npm test
```

It includes:

- desktop Preview bundle build;
- TypeScript type checking;
- desktop restore/build preparation before compiler-backed analysis;
- unused desktop-code analysis;
- read-only Component scaffolding contract and collision tests;
- Preview and desktop animation tests;
- headless Avalonia Preview shell visual-tree layout at 1040 and 1440 px,
  including real measure/arrange, panel bounds, tab headers, responsive Setup
  reflow and workspace restoration;
- architecture enforcement;
- desktop application build.

Architecture enforcement treats the Preview manifest as the complete executable
Component and Module catalog. It checks exact owner files, registry routes,
declared embedded dependencies and committed database parity. A matrix derived
from that manifest requires each owner contract, resolver, renderable, declared
embeds, registry route and committed fixture. The desktop integration test
renders every current Component fixture and exercises every Module fixture at
more than one local frame. The manifest is a current contract rather than a
migration ledger; inert migration-state fields are rejected.

The clean-checkout gate is:

```text
npm run test:cold
```

It removes desktop build outputs before running the complete validation. The
repository CI executes this cold gate so analyzer results cannot depend on a
previous local build.

Use focused checks while iterating:

```text
npm run check:architecture
npm run animation:test
npm run desktop-preview:build
npm run desktop:build
npm run desktop:db:validate
git diff --check
```

## Architecture enforcement

`scripts/checkDesktopPreviewArchitecture.ts` verifies current boundaries,
including:

- canonical documentation and archive isolation;
- exact manifest routing and declared dependencies;
- strict Preview payload documents;
- generic bridge and renderer boundaries;
- dictionary and Runtime Input `ValueKind` coverage;
- complete Variant references and local Overrides;
- focused repository and typed data-source ownership;
- recursive timing and animation contracts;
- shared UI action and input behavior;
- absence of startup persistence writes and compatibility paths.

Architecture enforcement reads only active documentation through one guarded
repository reader. It rejects absolute paths, parent traversal, alternate
separators that resolve outside the repository and every path below `docs/old`;
archive isolation is checked from active rules and active links without
consulting the sealed archive.

The check must fail when a concrete Component name, resolver or layout rule
leaks into a common Preview helper, central bridge or generic renderer.

## Persistence validation

Database validation is read-only and confirms:

- schema version and expected tables, columns, indexes and foreign keys;
- exact JSON root kinds;
- complete Component and Module Variants;
- full reference formats and same-Project integrity through the same guard used
  by repository writes;
- required Shot Actor and Production context;
- declared font, icon and media assets;
- manifest-to-row agreement.

Lifecycle and migration tests operate on disposable database copies.

## Manual UI validation

For any editor or Preview change, exercise at least:

1. Design selection, Variant change and class navigation;
2. temporary Test Values, Play, Restore and Escape;
3. fixed and polymorphic embedded Component authoring;
4. Overrides and explicit Forward presentation;
5. structured collection add, reorder, selection and deletion;
6. Component Stack and Collection Stack slots and States;
7. Production Episode → Shot → Screen selection and context;
8. Screen Payload editing beside Preview;
9. keyframe selection, Wacom/mouse drag and playback;
10. Usage navigation across Design and Production;
11. tree/editor Rename consistency and destructive confirmation links;
12. resizable panels, compact layout and scroll restoration.

Component-specific changes add an isolated Design case and a Production case
that reaches the same owner through a Screen payload.

## Delivery gate

A revision is ready for review only when:

- focused and full applicable checks pass;
- `git diff --check` passes;
- no unintended code, database or asset changes remain;
- required parity files are included;
- the worktree is clean after the local commit;
- the latest validated app is open for UI review, or the handoff states why a
  UI launch is not applicable.
