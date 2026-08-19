# Validation and enforcement

Status: normative.

## Validation principle

Architecture rules are executable wherever a stable check is possible.
Documentation describes ownership; validation prevents a future change from
silently restoring a second owner, implicit route or invalid persisted shape.

## Standard checks

The normal final gate for one coherent local revision is:

```text
npm run test:revision
```

It derives the exact required checks from the changed semantic owners and
fails when any path has no declared validation owner. Passing every selected
check is a complete validation for that revision scope.

The complete repository validation is:

```text
npm test
```

The public gate reads `data/mockups.sqlite` from the Git index
into a disposable file and supplies that path to every database-backed check.
It never replaces, opens for writing or validates against the workstation's
active authoring database. Its disposable workspace exposes the repository
assets read-only at the same relative boundary used by normal Project
resolution. The internal `test:repository` command is owned by that wrapper
and is not the normal entrypoint.

It includes:

- desktop Preview bundle build;
- TypeScript type checking;
- desktop restore/build preparation before compiler-backed analysis;
- unused desktop-code analysis;
- typed startup classification for missing or invalid Preview bundles, missing,
  empty or invalid databases, read-only successful preparation and
  cancellation before session publication;
- exclusive visual-editor lease acquisition, exit before Avalonia construction
  for a concurrent launch and successful reacquisition after the first owner
  exits;
- per-context SQLite write coordination, covering an independent write while a
  second database gate is held and serialized concurrent writes inside one
  database context;
- UI-independent editor-operation coordination, covering worker execution,
  submission ordering and cancellation of queued work at session disposal,
  plus compiled constructor enforcement for every visual persistence writer
  and post-commit presentation reader, and failed Runtime Override persistence
  retaining the confirmed document without an early Preview publication;
- prepared root and embedded editor content, including compiled operation
  coordination, field and dictionary snapshot-only visual card construction
  and a headless rapid selection test proving that only the latest owner
  reaches the visual state;
- Preview authoring exposes no synchronous Runtime Input visual-construction
  or fallback-load method; the cancellable prepared surface is its only
  compiled entrypoint;
- prepared Preview visual context, proving that Device options and metrics,
  Theme options and Project media root form one read-only snapshot, and that
  the visual controller exposes only an operation-coordinated asynchronous
  refresh boundary;
- prepared Production Preview session data, proving exact Shot FPS, ordered
  Screen ranges, keyframes, Variant configs and Actor-owned Shot context, while
  preventing the visual controller from retaining timeline or Shot-context
  data sources, plus headless create/reload/select and failed-preparation
  regressions proving that a new Shot cannot reach Production navigation before
  its refreshed Preview session commits and that a strict catalog failure keeps
  the prior tree, catalog and selection;
- strict Shot reference-video documents, including relative and absolute video
  paths, nullable In projection, source-change reset, stable video-time markers
  and focused repository round trips, plus loopback byte-range streaming for
  large seekable local sources;
- operation-coordinated Production playback payload preparation, covering exact
  frame order, preserved local frames, Actor and animation documents,
  cancellation, byte-for-byte read-only persistence and exact owner/frame
  lookup and covered-range reuse from the resulting immutable playback
  snapshot without signature recomputation;
- operation-coordinated static Production payload preparation, covering
  worker-thread execution, latest-revision ownership, close-time cancellation
  and reuse of the already prepared playback frame rather than a visual-thread
  payload read;
- compiled header-constructor enforcement proving that breadcrumb and
  context-strip rendering cannot receive Component, Preview or timeline
  persistence ports and requires an exact prepared header;
- UI-independent `EditorWorkspaceCoordinator` tests compiled against
  Application alone, covering workspace selection memory, invalid/deleted
  selections, Production removal, embedded-context rebasing, worker-thread
  execution, public async-only loading, rapid workspace reversal, cancellation,
  disposal, obsolete revision rejection and session-only Design editor
  back/forward transitions across exact root and embedded locations, including
  forward-branch truncation and deleted-owner skipping;
- window-close Preview lifetime coverage, including cancellation of Design and
  Production preparation, ahead preload and playback timing plus release of
  frame-cache and external rasterizer resources;
- Component scaffolding contract, collision, no-overwrite draft
  materialization, semantic integration transaction, deterministic generated
  routes, persisted-spec adoption and integrated-owner verification tests;
- Module scaffolding read-only planning, exact child Runtime-contract
  derivation, duration-policy validation, non-overwriting semantic
  materialization, transactional integration and deterministic generated
  registry/dictionary/config/slot verification, including the explicit source
  Variant path used by derived nested Runtime collections;
- macOS display-aware launcher ownership and command tests;
- Preview and desktop animation tests, including failed and rapid desktop
  commands that prove rollback to the confirmed document and ordered
  composition over the latest successful snapshot, plus Shot Screen boundaries
proving simultaneous outgoing and incoming Motion, stable owner-local frames
  frozen incoming action time, post-transition action delay, effective
  Screen/Shot duration and strict current transition documents;
- headless Avalonia Preview shell visual-tree layout at 1040 and 1440 px,
  including real measure/arrange, panel bounds, tab headers, responsive Setup
  reflow and workspace restoration;
- headless Avalonia editor view-state navigation across record classes and
  embedded breadcrumb levels, including card expansion and post-layout scroll
  restoration by exact `recordClassId`, plus the header Back/Forward controls
  restoring both embedded breadcrumbs and root editor view state;
- headless Avalonia List Item/List authoring surfaces, including Variant
  selection, numeric active-set and state Runtime values, shared List item
  dimensions, General plus promoted Content Set sections, compact Avatar/Label/
  Icon Row rows, ordinal-only List item navigation, exact nested child Runtime
  contracts, absence of duplicated child dimensions, collection add/duplicate/
  reorder actions, delayed nested commits retaining the moved stable item id,
  static Present plus its Presence playback action, distinct List and List
  Item boundary Motion ownership,
  repeated navigation across existing items without reparenting controls,
  active-set edits resolving the selected embedded Actor,
  horizontal containment with compact Runtime navigation, and collision-free
  rebasing of editable nested target ids while fixed boundary-local ids remain
  unchanged across parent add/duplicate operations;
- protected Default Variants remaining persistently locked while their edit
  unlock is session-only, with a new database session restoring the lock
  without a startup write;
- Incoming Call Avatar/Icon Row boundaries, independently authored placement
  and fixed structural Runtime reconciliation from zero to two Button rows
  while preserving matching stable-id values;
- generic Runtime action completion, visual-tree reattachment, repeat from the
  captured origin, Restore and exact prepared-frame reuse;
- Project-owned Production Output naming, stable Episode/Shot codes,
  portable route derivation, unsafe-path rejection and retained output folders
  on Shot deletion;
- Render Queue naming and shared Light/Dark version resolution, incremental
  content-addressed frame snapshots, bounded one-frame consumption, local
  recovery, strict stored-route containment,
  no-overwrite publication, sequential child jobs, permanent Production
  monitoring with stable monotonic progress controls, job-start route
  materialization, exact ProRes/H.264 profiles, proportional even-dimension
  normalization for odd H.264 Device rasters, black-premultiplied alpha output,
  interactive Preview Theme override through the Production Mode context
  control, session-only checkerboard and alpha-channel inspection with compact
  switches whose track and thumb follow the checked state without duplicate
  labels, explicit Play/Pause transport glyph contrast, and an always-openable
  Shot add action;
- explicit Conversation text tracks retaining Keyboard and Text Input Bar
  presence for the outgoing write interval;
- architecture enforcement;
- desktop application build.

The executable `Mockups.Desktop.Host` MSBuild target regenerates Preview
artifacts incrementally when a TypeScript, TSX, Preview JSON manifest or build
input changes, then refreshes their manifest for the current build. A separate
source stamp prevents manifest-only generation from accepting artifacts built
from another source hash. npm orchestration does not run a second implicit
generation step. Direct `dotnet build`, `dotnet run` and `dotnet publish`
therefore produce the same manifested bundle. Startup and tooling tests verify
its schema, source hash, required artifacts, per-file SHA-256 hashes and
aggregate hash. The desktop suite runs
in three fresh processes: `core`, native visual-tree `ui`, and manifest-wide
`exhaustive`.
This preserves complete coverage while preventing native UI state from leaking
between unrelated tests.

Architecture enforcement treats the Preview manifest as the complete executable
Component and Module catalog. It checks exact owner files, registry routes,
declared embedded dependencies and committed database parity. A matrix derived
from that manifest requires each owner contract, resolver, renderable, declared
embeds, registry route and committed fixture. The desktop integration test
renders every current Component and Module Variant at local frames 0, 1, 12 and
60; Component Variants are exercised in both Light and Dark. The explicit
Preview capability matrix inventories every root action, collection-item
action and frame-owned behavior, and validation requires exact parity with the
persisted Runtime contracts. Focused resolver tests characterize Motion,
write-on, playback, controls fade, reflow, key presses, authentication progress
and Cursor propagation through embedded boundaries. The manifest is a current
contract rather than a migration ledger; inert migration-state fields are
rejected.

The clean-checkout gate is:

```text
npm run test:cold
```

It removes desktop build outputs before running the complete validation. The
repository CI executes this cold gate so analyzer results cannot depend on a
previous local build. On Linux, the workflow installs Xvfb and WebKitGTK 4.1,
then runs the same gate on a virtual display so tests can construct the real
`MainWindow` and its native Preview WebView.

Use focused checks while iterating:

```text
npm run test:changed
npm run test:changed -- --list
npm run test:revision
npm run test:revision -- --base <git-ref>
npm run test:focus:preview -- tests/animation/<owner>.test.ts
npm run test:focus:application -- --exact "<Application test name>"
npm run test:focus:application -- --filter "<stable name fragment>"
npm run test:focus:desktop -- --exact "<desktop test name>"
npm run test:focus:desktop -- --filter "<stable name fragment>"
npm run animation:test:desktop:owner -- --owner "component:<manifest id>"
npm run animation:test:desktop:owner -- --owner "module:<manifest id>"
npm run test:guard
npm run check:architecture
npm run validate:contracts
npm run validate:generated
npm run validate:pipeline
npm run validate:retired
npm run validate:architecture
npm run test:architecture
npm run desktop-preview:build
npm run desktop:build
npm run desktop:db:validate
git diff --check
```

`test:changed` derives the iteration plan from tracked and untracked workspace
paths. `test:revision` uses the same ownership plan for the current workspace
revision; when the workspace is clean it compares `HEAD^` with `HEAD`, and an
explicit `--base` selects a larger coherent revision. `--list` prints every
selected command and its reason without running it. Repeated `--file <path>`
arguments allow a deliberately narrower inspection when the shared workspace
contains unrelated authoring data.

The workstation's unstaged `data/mockups.sqlite` is excluded from automatic
discovery because normal application authoring can keep it dirty and repository
validation owns the staged parity artifact. Staging the database includes it
automatically. During an intentional database edit,
`--file data/mockups.sqlite` includes the active file explicitly before it is
staged.

The scoped owner is conservative about coverage but never selects `npm test`
implicitly. A path without a declared validation owner stops immediately,
prints every unclassified path and requires the route plus its focused checks
to be added before validation continues. This makes validation ownership an
explicit current contract instead of hiding a missing classification behind
the slow complete suite. The complete suite remains deliberate and is required
for shared Preview boundaries, manifest or registry changes, persistence
schema or parity data, generated scaffolding, cross-owner integrations, phase
handoffs, merges and publication, or when explicitly requested.

An exact Application or Desktop name, Preview owner or filter that does not
exist, an unknown selector or a filter that matches nothing fails explicitly.
Owner selectors use the exact stable manifest key, such as `component:label`
or `module:module.core.chat`; more than one `--owner` may be supplied in one
run. A local owner change starts with its owner-specific Preview and Desktop
tests. These commands reach Preview generation through the Desktop project
before running, so they cannot exercise stale web output.

Validation scope follows the changed owner:

- a concrete Preview contract, resolver or renderable runs its matching
  characterization files and only that manifest owner;
- a shared Preview helper, boundary, registry, renderer or bridge runs the
  complete Preview suite and manifest-wide render;
- an Application or Domain change runs Application tests and the compiled
  consumer;
- a local Desktop behavior runs Desktop core coverage, adding native UI only
  for shared visual-tree or XAML surfaces; exact declared regressions replace
  the broad group when the owner has a stable mapping;
- persistence, parity data and referenced assets add read-only database
  validation;
- scaffolding, tooling and architecture paths run only their focused
  executable owners;
- normative documentation runs contract validation; non-normative
  documentation needs only the common diff check.

The manifest-wide Desktop exhaustive process is reserved for changes that can
affect more than one owner: manifest or registry changes, common Preview
helpers, generic renderer or bridge changes, shared resolver contracts,
persistence/schema/fixture changes, generated scaffolding, broad Desktop
surfaces, phase handoff, merge or publication. `npm test` and CI keep that
complete sweep, but local iteration never reaches it as a fallback. An
unchanged successful focused gate is not repeated; any
subsequent source, contract, database, asset or generated-file change
invalidates the applicable result.

## Architecture enforcement

Architecture validation has focused entrypoints:

- `validate:contracts` owns canonical normative documentation and archive
  isolation;
- `validate:generated` owns Component and Module scaffolding contracts and
  generated-artifact parity;
- `validate:pipeline` owns npm and CI orchestration;
- `validate:retired` owns only the finite exact path list for architecture
  surfaces that have been removed and must not return;
- `validate:architecture` owns compiler- and parser-backed dependency
  boundaries.

`check:architecture` aggregates those owners once. Important dependency rules
do not inspect source text. The .NET suite evaluates the actual MSBuild graph,
project references, package references and resolved compiler assemblies. It
also allowlists friend assemblies and external resources, rejects direct
assembly references, custom analyzers, external compiled source, custom SDKs
and project-local MSBuild imports, and compiles negative fixture projects that
must fail when a consumer tries to use a transitive Domain or SQLite
capability. An Application-only fixture must also fail when it attempts to use
the separately compiled synchronous persistence ports.

The .NET graph has one canonical exact matrix for every production project's
project and package references, plus one resolved-assembly matrix for
capabilities whose absence must be proven after MSBuild resolution. Layer-
specific prose tests do not repeat those same edges. The four negative compile
fixtures remain independent because they prove that forbidden source cannot
compile rather than merely restating the positive graph.

The Preview suite parses every
static, exported, import-assignment, `require` and dynamic TypeScript import
recursively with TypeScript module resolution, rejects computed module loads,
then derives permitted concrete owner edges from the current manifest.
Pipeline validation parses its ordered command stages and compares the exact
declared owner sequence. It does not accept a command merely because an
expected substring appears somewhere in it. Retired-contract validation names
only removed roots and executable entrypoints. Individual implementation
filenames are not architecture boundaries; project graphs, import graphs and
focused behavior tests own those rules. The shared validation context has
no implementation-source blacklist API; remaining text-presence checks belong
only to normative documentation, where prose is the contract being validated.

That structural suite also requires:

- the production persistence assembly contains no `SqliteProjectEngine` or
  other universal project object; `SqliteProjectSessionFactory` has no public
  surface or Application interface and publishes only the focused session
  ports;
- read-only startup contract checks belong to the internal
  `SqliteCurrentDatabaseValidator`, which exposes no public surface or
  Application interface;
- every session capability exposes exactly the public methods declared by its
  own port;
- Production, Design and Resource record-field adapters are pairwise
  non-castable and expose only their owner-specific contract;
- Component-field adapters cannot be cast to Component-document adapters, and
  animation-write adapters cannot be cast to timeline-read adapters;
- test fixtures reach navigation through the focused navigation owner;
- Design, Production and Resources reads and writes cannot reappear as
  composition forwarding methods;
- Component document and embedded-boundary overloads exist only on their
  focused store;
- Component reference catalogs, Runtime contracts and reference validation
  cannot reappear as composition forwarding queries;
- Component Variant commands and reference details cannot reappear on the
  session or its factory;
- Module Variant fields, selection, lifecycle commands and effective Runtime
  reads cannot reappear as a composition facade;
- Module Instance Runtime writes, collection lifecycle, Production scalar fields,
  animation and read models cannot reappear as composition operations;
- Shot fields, Design scalar fields, Resource scalar fields and default path
  discovery remain on their focused owners;
- exact manifest owner files, categories and declared embeds;
- exact Component and Module registry parity;
- registries whose factory entries are direct owner calls without business
  conditions;
- generic renderers with no dependency on concrete Preview owners;
- filesystem imports confined to explicit asset and request boundaries.

Behavior is not owned by architecture validation. Strict Preview payloads,
dictionary and Runtime Input contracts, Variant references, Overrides, timing,
animation, UI interaction and Render Queue behavior belong to their focused
tests. The manifest-wide desktop test renders every committed Variant fixture.
Compiled Desktop tests require every visual persistence writer, including
Runtime Input and Module Instance animation stores, to receive the session
operation coordinator and expose task-returning mutation methods.
They also require Production Screen preparation to carry the animation source,
Screen origin and duration together, and verify that the visual Runtime Input
store exposes no synchronous animation read path.
The same constructor rule covers persistence-backed resource pickers, preventing
new synchronous token queries from being wired directly into visual callbacks.
Headless UI coverage verifies that deferred cards perform no load while
collapsed and load exactly once across repeated expansion.
It also reverses the selected record while editor preparation is in flight and
requires the committed card owner to match the latest session revision.
The C# startup validator and persistence tests own the complete staged SQLite
contract. Scaffolding read-only, collision, materialization and integration
behavior belongs to executable tests over temporary workspaces and databases;
generated validation compares deterministic artifacts exactly instead of
searching implementation text. The repository pipeline is an ordered
executable gate list with tests for order, early failure, staged-database
isolation and cleanup. Generated artifacts, documentation and CI keep their
separate validators.

Architecture enforcement reads only active documentation through one guarded
repository reader. It rejects absolute paths, parent traversal, alternate
separators that resolve outside the repository and every path below `docs/old`;
archive isolation is checked from active rules and active links without
consulting the sealed archive.

## Persistence validation

Database validation is read-only and confirms:

- schema version and expected tables, columns, indexes and foreign keys;
- exact JSON root kinds;
- exact Device metric objects and nested properties, including rejection of
  retired geometry and scale fields, incomplete Module transparency policies,
  separation of background opacity from the global gradient mask,
  and Palette tokens that do not exist in the same Project;
- complete Component and Module Variants;
- full reference formats and same-Project integrity through the same guard used
  by repository writes;
- required Shot Actor, independent nullable Device/Theme overrides and their
  exact effective Production context;
- exact Production Output settings and derivable Shot plans;
- declared font, icon and media assets;
- manifest-to-row agreement.

Lifecycle and migration tests operate on disposable database copies.

Repository validation derives its pristine source from the staged parity
artifact. This keeps workstation-local Production Output roots and other local
authoring changes out of test expectations while still validating the exact
database intended for the revision.

Component scaffold verification has a narrower boundary than persistence
validation. It checks the development-owned integration surfaces, stable
Component Class identity and editor layout created by the scaffold. It never
compares later application-authored names, notes, config, Design Preview or
Variants with their initial scaffold values.

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
13. Production Output card, generated Shot nomenclature, workstation-local
    root and retained output folders on Shot deletion.
14. Shot Render action on a pre-association Shot, Actor loaded before routing,
    automatic route proposal, Device/Theme overrides, Light/Dark/Both naming,
    job-start folder creation, queue progress, cancel/retry/pause and output
    reveal.
15. Shot reference Browse, missing/out-of-range `Sin media`, Set In, muted and
    audible playback, shared Shot/Screen scrubbing, marker add/drag/text/delete
    and marker ticks across Screen boundaries.

Component-specific changes add an isolated Design case and a Production case
that reaches the same owner through a Screen payload.

## Windows package validation

For a Windows release, or a revision that changes WebView, child-process or
Preview transport behavior, validate from a clean `main` checkout on Windows
10 or Windows 11 with Node.js, the .NET 10 SDK and the current Microsoft Edge
WebView2 Runtime installed:

```powershell
npm ci
npm run desktop:db:validate
npm run desktop:publish:win
Copy-Item ".\data\mockups.sqlite" ".\data\.windows-smoke.sqlite"
& ".\out\desktop\win-x64\Mockups.Desktop.Host.exe" --db ".\data\.windows-smoke.sqlite"
```

The smoke test uses only that ignored disposable database. Confirm Design and
Production navigation, editor-to-Preview updates, playback, timeline input,
panel visibility restoration and routed navigation. Preview text must preserve
Spanish punctuation and accents, `áéíóúüñ¿¡`, a single-code-point emoji and a
multi-code-point emoji. Repeated selection changes must commit without a
five-second Preview pause, retain the last valid Preview on failure and leave
no MOCKUPS or Preview process after normal close.

After closing, remove only `data\.windows-smoke.sqlite`, confirm the worktree is
clean and report the tested commit, Windows version, CPU architecture, display
scale, tool versions, command results and any screenshots or exact failures.

## Delivery gate

A revision is ready for review only when:

- every check selected by `npm run test:revision` passes;
- `npm test` also passes when the revision crosses a complete-gate boundary
  declared above;
- `git diff --check` passes;
- no unintended code, database or asset changes remain;
- required parity files are included;
- the worktree is clean after the local commit;
- the latest validated app is open for UI review, or the handoff states why a
  UI launch is not applicable;
- on macOS, the open app was rebuilt, packaged and launched through
  `npm run desktop:launch:mac`; a development process or pre-existing bundle is
  not a delivery artifact.
