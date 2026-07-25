# Production

Status: normative.

## Production workflow

Production consumes definitions and resources to build an ordered visual
sequence:

```text
Project
→ Episode
→ Shot
→ ordered Screen (Module Instance)
→ exact Module Variant
→ persisted Screen payload
→ owner-relative animation
```

Design definition editing and Production sequence authoring are independent
workflows. Navigation may cross between them to inspect a referenced owner,
while preserving each editor's session context.

## Navigation

Production exposes:

- Episodes, Shots and Screens in the sequence tree;
- one permanent **Render Queue** section for workstation-local jobs;
- one **Production Data** card containing Actors, Devices and Production Fonts.

Future Project duplication may offer:

```text
copy current records | regenerate from current seeds | create empty
```

That choice is explicit per resource group. Resource lookup must never fall back to records from another Project.

## Episodes and Shots

An Episode owns ordered Shots. A Shot owns:

- stable identity and order;
- one required explicit owner Actor;
- frame rate and current canvas metadata;
- ordered Screens;
- aggregate duration.

Shot creation requires an Actor selection and an explicit positive Shot
number. The Shot editor never offers an empty owner. The Actor may be changed
later to another Actor in the same Project. The number is stable, unique
inside its Episode and exists even when the Project is not associated with
Shot Manager.

Duplicating an Episode preserves every current Shot number because the copies
belong to a different Episode. Duplicating one Shot requires a new number in
the same Episode and creates a new stable id.

## Optional Shot Manager governance

A Project may associate with one exact VFX Shot Manager Production and Season.
The integration is optional: without an association, Episodes and Shots keep
the independent workflow above. The independent Shot Manager icon to the left
of the active Production selector opens this Project-owned association. Its
tooltip explains the current state and its glyph is green only while that
Project has an association.

With an association:

- Shot Manager owns the Production, Season and Episode identities, Episode
  names and codes, technical Shot naming rules and configured folder layout;
- the initial connection requires one explicit choice for every Shot Manager
  Episode: associate it with one unbound same-Project local Episode or choose
  **Create new**;
- no Episode is created, matched or adopted by code, name, number or order
  before those complete choices are confirmed;
- later synchronization preserves stable external identities and requires the
  same explicit choice for every newly discovered Shot Manager Episode;
- MOCKUPS remains the sole owner of every Shot row, Actor, Screen, creative
  payload and animation;
- creating or duplicating a Shot requires an explicit positive Shot number and
  Actor;
- Shot Manager returns a read-only, non-reserving plan; MOCKUPS validates it
  and stores its exact portable render contract without creating directories;
- no external Shot identity exists or is manufactured.

The cached contract contains portable relative directories, stable
structure-entry identities plus the source prefix and version padding for
every planned output. MOCKUPS uses the stable entry identity and relative
directory but owns the final render name. Workstation roots, resolved absolute
paths, discovery data and bearer credentials remain local and transient.
Opening the Render Queue modal asks Shot Manager for a fresh plan from the
exact Production, Season, Episode and local Shot number. A successful plan
replaces the cache; if the service is unavailable, the last matching contract
and workstation root may be used.

Episode rename, creation, duplication and deletion are unavailable while the
association governs that hierarchy. Synchronization may remove a governed
Episode only while it contains no local Shots. Disconnecting retains the local
Episodes, Shots and render-contract caches and removes only association
ownership. Deleting a Shot removes its local database content but deliberately
retains any production output folders. No ordinary lifecycle action deletes
external folders.

## Render Queue

The Render action is a persistent icon on every Shot row. It always opens an
add modal with that exact Shot selected. Actor, Device, Theme and local Shot
details load independently of routing. The modal resolves a fresh route and
technical name from Shot Manager using the Project association, Episode
binding and stable Shot number. A Shot created before the association follows
the same path. If neither a fresh plan nor a matching cached contract is
available, the modal keeps the local details visible, explains the route
problem and disables enqueue. Governed output never falls back to a free
folder picker.

The add modal exposes:

- the Shot Actor as read-only;
- Device and Theme defaulted from that Actor, with same-Project job-only
  overrides;
- Light, Dark or Both;
- one of the predefined output routes returned by Shot Manager or its matching
  cached contract;
- a job-owned output mode;
- an editable safe base name.

The initial output modes are MOV ProRes 422 HQ, MOV ProRes 4444 with alpha,
MOV H.264 Light at 8 Mb/s, Standard at 20 Mb/s, High at 40 Mb/s, PNG sequence
and EXR sequence. H.264 uses the exact Créditos `libx264` profiles with
`yuv420p` and fast start; it never preserves alpha. No output contains audio.

MOCKUPS proposes:

```text
<SHOT>_LIGHT_v001
<SHOT>_DARK_v001
```

The user may add a qualifier such as `GFX` or `COMP` to the base name.
Appearance and version remain automatic. Both reserves one free version across the pair
and creates two independent child jobs with the same batch and route.
One child may complete or fail without changing the other.

Every Screen resolves its exact Module appearance contract. A Screen forced to
Light or Dark keeps that mode even inside the opposite requested Shot job;
`inherit` follows the job appearance. Device and Theme combinations are never
rejected by family because Themes are authored visual fiction.

Adding a batch first creates its visible `PREPARING` children. Snapshot
preparation then resolves each Shot frame and writes its immutable raster
document to the local queue store before releasing that frame from memory.
Documents and referenced assets are content-addressed; identical documents,
fonts and media are stored once for the batch and shared by its Light/Dark
children. Only after every child manifest is complete do the jobs become
`PENDING`. Later editor changes do not modify that work.

The worker uses the same raster-document pipeline as raster Preview but owns a
separate persistent Chromium session. It reads one frozen document at a time
and produces the clean Production canvas with no editor chrome or device frame.
It releases that document before requesting the next. Identical
documents reuse their existing lossless raster. Jobs run sequentially, can be
paused or canceled, and active work with a complete snapshot returns to
Pending after application restart. Interrupted incomplete preparation becomes
a non-retryable failed batch and its partial local store is removed.

The selected route is stored by stable Shot Manager `entryId`. The modal
restores the last route used by the Project when it remains available and
otherwise proposes the first predefined route. Its relative directory and
version padding come from the freshly resolved or matching cached portable
contract. The workstation root comes from the on-demand Shot Manager plan and
is cached locally so a previously synchronized Production can still render if
that service cannot start.

The predefined relative route does not need to exist when the batch is
planned. The worker validates its containment under the existing workstation
root and creates any missing route directories when that job begins. Existing
route segments must be real directories rather than files, symbolic links or
reparse points. Planning and enqueue never create folders, and publication
still refuses an existing final output.

Monitoring is separate from batch creation. The permanent Production
**Render Queue** panel is available even when it is empty, when Shot Manager is
offline and when no Shot has a cached output route. It groups child jobs by
batch and owns pause/resume, cancel, retry, reveal, remove and clear-finished
actions together with progress, error and output-path reporting. Preparation
is indeterminate; only rasterized frames advance the determinate frame bar.
Rows update their existing controls in place and execution updates are ordered
monotonically, so an older notification cannot make a bar move backwards. The
add modal and this panel share one workstation-local queue manager.

## Screens

A Screen is a persisted Module Instance. It owns:

- exact App, Module and Module Variant references;
- order within its Shot;
- transition document;
- Runtime Input payload in `content_json`;
- behavior and animation documents;
- duration when the Module policy is explicit;
- current metadata.

Screen payload is authored in Preview because that is where its effect can be
checked, but ownership remains with the Screen instance.

The first Preview tab is an authoring host, not a Preview-owned data store.

The Runtime API diagnostic presentation is hidden in the current Design and
Production UI. Its implementation may remain available to internal tooling.

## Production context

Every Screen resolves through its exact Shot. A complete valid route is:

```text
Screen → Shot → owner Actor → Actor default Theme → Device and visual context
```

Missing or cross-Project context fails explicitly. App, Module, Variant, name,
type, order and position cannot supply an Actor, Theme or Device implicitly.

The Preview context shown to the user is derived from the selected Shot and
Screen. Switching to a referenced definition also switches to the correct
Design or Production workspace and selects the exact tree item.

## Conversation message ownership

The Actor attached to a conversation message describes the message owner and
is independent from the Shot owner:

- an incoming message requires an explicit same-Project Actor;
- an outgoing message stores no duplicated Actor and resolves the exact Shot
  owner in the Production payload;
- a system message may optionally refer to an explicit same-Project Actor.

Changing direction clears or requires the Actor as one atomic prepared
collection write. Design sample Actors are fixtures and never repair persisted
Production messages.

An outgoing message with an explicit animated `text` track keeps the
track-owned write interval for composer presence even though that track
replaces the bubble's base write-on. Text Input Bar and Keyboard therefore
remain visible while the animated text is being authored on screen.

## Playback

Play resolves the selected Production context and presents complete frames in
the Preview panel. Replaying unchanged state reuses the prepared result.
Restore returns temporary playback state to the current authored frame.

Escape cancels preparation as well as active playback. Cancellation does not
mutate the Screen payload or animation document.
