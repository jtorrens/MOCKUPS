# Development and scaffolding workflow

Status: normative.

## Read before changing

Before changing the desktop editor, read:

1. `AGENTS.md`;
2. `docs/README.md`;
3. this architecture index and the canonical documents relevant to the task;
4. current manifest, schema and owner implementation.

Historical material is not an architecture source. The archive access rule in
`docs/README.md` applies to every agent and contributor.

## Make one owner responsible

Every semantic rule has one owner:

- SQL and row mapping: focused repository;
- current document validation: document contract;
- field behavior: dictionary;
- collection behavior: collection owner;
- timing and frame conversion: common owner timeline;
- Component/Module composition: its resolver and renderable;
- cross-domain context: typed data source or service;
- final painting: generic renderer;
- shell composition: `MainWindow`.

Before adding a helper, inspect `spikes/desktop-editor-shell/Common` and the
existing shared editor surfaces. Reuse or extend the owner when behavior is
generic. A one-off exception is not an implementation shortcut.

## Atom and Component scaffolding

Given a user description, Codex first converts it into an explicit authoring
contract:

1. responsibility and visual boundaries;
2. stable Component Class id and manifest category;
3. dictionary fields grouped into Variant values and Runtime Inputs;
4. canonical `ValueKind` for every editable value;
5. protected Default Variant as a complete snapshot;
6. embedded slots with exact allowed class, full Variant reference and local
   Overrides;
7. explicit forwarding declarations;
8. runtime actions, duration metadata and temporal owners;
9. resolver output expressed through generic primitives;
10. Design Preview fixture and Test Values;
11. editor layout metadata;
12. persistence/seed migration and complete validations.

The user confirms this contract before implementation. Scaffolding then creates
all declared parts in one coherent development sequence. The application does
not fill missing parts at startup or through an Add action.

### Component contract planning

The first scaffolding stage is deliberately read-only:

```text
npm run scaffold:component -- --print-template
npm run scaffold:component -- --spec <component-contract.json> --dry-run
npm run scaffold:component -- --spec <component-contract.json> --materialize
npm run scaffold:component -- --spec scaffolding/drafts/<type>.json --integrate
npm run scaffold:generate
npm run scaffold:component -- --spec scaffolding/components/<type>.json --verify
```

The printed template is intentionally not a valid authoring contract until all
of its replacement markers and example identities have been replaced.

The specification explicitly supplies responsibility, visual and temporal
boundaries, forwarding intent, identity, category, manifest routes, owner
exports, registry signature, embedded dependencies, current config, complete
protected Default Variant, zero or more complete non-protected additional
Variants, Runtime Input/collection/action fixture, dictionary fields, editor
layout and required asset paths. The planner derives current
`ValueKind` names from their canonical desktop enum, reads the current manifest
and opens the parity database read-only.

The resulting plan identifies every owner file, registry and manifest change,
dictionary descriptor, current database row, editor layout row and validation
command. It rejects identity collisions, unknown embeds or `ValueKind` values,
unsafe paths, incomplete Runtime Input definitions, hidden Variant envelopes
and editor fields that are missing or duplicated. Additional Variant ids are
stable, unique across the definition and never replace the protected Default
Variant as the current config snapshot.

The dry-run stage writes neither source nor database. Materialization persists
the exact spec under `scaffolding/drafts` and creates, without overwriting,
the contract, resolver, renderable, strict desktop config owner and focused
test. Every generated owner carries an explicit semantic marker and remains
unregistered: materialization never edits the manifest, registry or database.

The semantic implementation replaces every marker with the concrete resolver,
renderable, strict config validation and focused characterization. Explicit
integration then moves the reviewed spec into `scaffolding/components`, adds
its exact manifest route and polymorphic Stack declarations, inserts the
Component Class and editor layout in one maintenance transaction, and
regenerates the registry route, dictionary descriptors and desktop config
route. Integration rejects missing assets, owner markers, identity collisions
and partial ownership before writing anything.

The files named `generatedComponentScaffold*` and
`GeneratedComponentScaffold*` are derived exclusively from the integrated
specifications. They are never edited manually. `npm run scaffold:generate`
rebuilds them deterministically; `npm run scaffold:verify` fails if they are
stale or if an integrated owner, manifest route, database row, complete
Variants, Design Preview fixture or editor layout diverges from its spec.

An existing complete Component can be adopted once from the current manifest,
owners and committed database through the explicit `--adopt-existing` workflow.
The resulting persisted spec becomes the reviewed authority and generates the
same shared integration artifacts; adoption refuses multiple classes of the
same type and never overwrites a prior spec.

## Module scaffolding

A Module description is converted into:

1. stable App and Module ownership;
2. exact manifest route;
3. complete protected Default Module Variant;
4. Runtime Inputs, structured collections and Screen payload shape;
5. embedded Component Variant references and explicit forwarding;
6. calculated or explicit duration policy;
7. recursive temporal ownership and action timing;
8. Production context requirements;
9. Module resolver and renderable;
10. Design fixture, editor metadata and validation scenarios.

The user confirms this contract before implementation. A Module is complete
only when Design authoring, Production payload, timeline and Preview all use
the same declarations.

### Module contract planning

Module development follows the same read-only planning and explicit semantic
integration boundary as Component development:

```text
npm run scaffold:module -- --print-template
npm run scaffold:module -- --spec <module-contract.json> --dry-run
npm run scaffold:module -- --spec <module-contract.json> --materialize
npm run scaffold:module -- --spec scaffolding/module-drafts/<class>.json --integrate
npm run scaffold:module:generate
npm run scaffold:module -- --spec scaffolding/modules/<class>.json --verify
```

The specification names the exact existing App and Project, Module identity,
manifest contract/resolver/renderable routes, complete protected Default
Variant, optional complete additional Variants, duration policy, dictionary
fields, embedded Component slots, editor layout, assets and one exact child
Component Runtime contract source. The source is a full Component Variant
reference plus the ordered stable Runtime Input and collection ids expected by
the Module. Planning reads the manifest and parity database without writing
either, resolves that exact same-Project Component fixture and rejects drift in
any declared id.

Materialization stores the reviewed draft and creates non-overwriting semantic
owner skeletons without registering the Module. Integration is allowed only
after the contract, resolver, renderable, strict desktop config owner and
focused test have replaced every semantic marker. It then adds the manifest
route, current Module row and editor layout in one maintenance transaction and
regenerates the Module registry, dictionary descriptors, config route and
embedded-slot catalog.

Files named `generatedModuleScaffold*` or
`GeneratedModuleScaffold*` are derived only from integrated specifications and
must not be edited manually. Verification re-derives the Runtime fixture from
the exact child Component contract and checks the manifest, owners, generated
routes, complete Variants, editor layout and committed parity database.

## Persisted changes

Persistence changes use an explicit maintenance workflow. Update schema,
seeds, current rows, references, parity database and assets together. Remove
the migration routine from the delivered revision.

Normal startup remains read-only. Do not add compatibility parsing, alternate
field names or fallback defaults to ease a migration.

## macOS display-aware development launch

Avalonia.Native registers its render timer against the active macOS displays
before it creates the compositor. A process started remotely while every
display is asleep can therefore fail before the first window with CoreVideo
error `-6661`.

Display wake policy belongs to development tooling, not application startup.
The macOS launcher declares user activity and prevents display sleep for ten
seconds while it starts the requested process. The assertion then expires; the
editor never keeps the display awake for the rest of the session.

Use:

```text
npm run desktop:mac
npm run desktop:open:mac
npm run desktop:launch:mac
```

`desktop:mac` runs the development build. `desktop:open:mac` opens the existing
packaged bundle and fails if it is absent. `desktop:launch:mac` packages the
current revision first. Automated macOS UI review uses one of these launchers;
headless checks continue to use the Avalonia headless platform.

## Safe implementation sequence

For each coherent phase:

1. confirm branch, expected commit and clean worktree;
2. stop the desktop editor and any writer of the parity database;
3. inspect the current owner and shared equivalent;
4. implement the smallest complete owner change;
5. update current documentation and enforcement together;
6. run focused checks, then the full applicable validation;
7. inspect the final diff, including parity artifacts;
8. create a local commit;
9. open the validated desktop application for manual review when UI behavior
   changed;
10. push only when the user asks.

Only one task writes tracked project code or parity data in the shared checkout
at a time. Read-only investigation may run independently.

## Collaboration

A question starts discussion, not implementation. A proposed interaction,
architecture or data mechanism is summarized with its ownership boundaries and
awaits explicit confirmation before files are changed.

After implementation, report:

- what changed;
- exact manual checks;
- whether the validated app is open;
- branch and local commit;
- whether the worktree is clean.

When a revision becomes the version used on other computers, integrate it into
`main`, push `main`, switch the checkout to `main` and verify local and remote
commit equality.
