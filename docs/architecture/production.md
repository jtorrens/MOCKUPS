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
- independently inherited Device and Theme references;
- frame rate and current canvas metadata;
- ordered Screens;
- aggregate duration;
- one optional reference video document, Project-relative when possible and
  otherwise absolute, with an optional In frame and stable video-relative text
  markers.

Shot creation requires an Actor selection and an explicit positive Shot
number. The Shot editor never offers an empty owner. The Actor may be changed
later to another Actor in the same Project. The number is stable and unique
inside its Episode. MOCKUPS derives the Shot code, technical name and output
route from it.

Actor creation requires its name, short name, same-Project default Device,
same-Project default Theme and exact light/dark Palette Color identities before
the row is inserted. There is no incomplete Actor placeholder and no startup or
editor repair route. Actor and Shot creation share the generic record-creation
form and persistence entrypoint.

Device and Theme overrides are optional same-Project Shot fields. Each is
independent: `NULL` inherits the corresponding required Actor default, while a
local reference replaces only that resource. Changing the Actor therefore
changes only resources that remain inherited. The effective Device and Theme
are consumed consistently by Preview, playback, Module Theme-token resolution
and Render Queue initialization. Reference Usage reports only explicit Shot
override edges; inherited resources remain Actor edges.

The Device reference also exposes the shared Overrides action. Its sparse
Shot-local document can override every editable Device setting while leaving
the Device record unchanged. Those values remain local when the selected or
inherited Device changes and are then applied over the new Device. The standard
field Restore action removes one local value and immediately resumes inheriting
that setting from the current Device. The action opens the referenced Device
layout in the central contextual editor under the exact Shot breadcrumb; it
never opens a modal and introduces no Shot/Device editor branch.

Duplicating an Episode preserves every current Shot number because the copies
belong to a different Episode. Duplicating one Shot requires a new number in
the same Episode and creates a new stable id. It copies the complete ordered
Screen aggregate with new Screen ids and otherwise exact Screen documents.
The duplicated Shot preserves its authored Actor, resources, Overrides,
reference video and timing, but always starts free of Shot Manager association
and must be associated explicitly.

## Production Output ownership

MOCKUPS is the sole owner of its Projects, Episodes and Shots. A Project chooses
one explicit output mode. Manual mode uses the portable Production Output
contract: Production and Season codes, optional Episode and Shot prefixes,
number/version/frame padding and one relative route template containing exactly
`SEASON_CODE`, `EPISODE_CODE` and `SHOT_NAME`.

Episode codes are explicit current authored values; the optional Episode Prefix
only supplies their default at creation. Shot creation asks for one positive
stable number and the required Actor; duplication asks only for the new number
and preserves the source Actor. The optional Shot Prefix and padding only
supply the initial Shot Code. A Shot Code is editable, unique within its
Episode, accepts letters, numbers, hyphen and underscore, and is never derived
again. The technical name and route resolve from the exact authored codes. The
four naming segments are concatenated literally: punctuation belongs to the
relevant Production, Season, Episode or Shot segment rather than to a separate
global separator. The current FOQN contract resolves:

```text
FOQN + _S02_ + EP_01 + _SH0001
→ FOQN_S02_EP_01_SH0001
→ S02/EP_01/FOQN_S02_EP_01_SH0001/comp
```

The absolute Production root is workstation-local, keyed by stable Project id
and stored outside SQLite. It must already exist. The portable database never
stores an absolute workstation path. Deleting a Shot removes authored content
but never deletes an existing Production Output folder.

Shot Managed mode associates the Project with one stable external
`productionId`, plus one portable workstream and one folder chosen from that
workstream. The workstation-local, read-only `production.json` is required to
create or refresh associations. Unknown properties and schema-version changes
are tolerated, while the fields consumed by MOCKUPS remain strict. Its local
path and Production root remain workstation state. The selected production
and destination values—including production/season slugs and the folder's
optional suffix—are captured portably; the suffix participates in the
technical render name.

Each MOCKUPS Episode is `associated` or `free`. Its retained reference captures
the external Production id, Episode id, order, optional slug and exact
`pathSegments`. Order remains available for selector ordering and labelling;
neither it nor Production, Season or Episode slugs participate in managed route
resolution. Each Shot is likewise `associated` or `free`, and its retained
reference captures the Production id, Shot id and canonical name. Associated
output resolves as:

```text
canonicalName + optional folder suffix
→ ...episodePathSegments/workstream/folder
```

Shot Manager owns every physical segment through the Episode, including the
Season directory. MOCKUPS preserves those segments exactly and only appends the
selected workstream and folder. It never derives a managed path from season
number, Season slug, Episode order or Episode slug.

The external canonical Shot name is deliberately authoritative so a later
change to Production, Season or Episode slugs cannot silently rename automatic
output. MOCKUPS may use any subset of the external Episodes or Shots. An
free Shot uses the complete manual Production Output contract. Changing an
Episode association makes its child Shots free while retaining their external
references; explicitly clearing a selector removes that reference. Duplicating
an Episode or Shot starts free without an external reference.

An associated Production resolves from captured values when `production.json`
is offline. No new association or refresh is possible until it reconnects.
Reconnecting the same `productionId` refreshes Project, Episode and Shot values
atomically. Missing referenced items become free but retain their references;
the same identities automatically recover if they return on a later refresh.
Selecting a different Production is explicit and makes all descendants free.
Relocating the local Production root does not change portable associations.
Existing published renders are never moved after an association or external
document changes. Queued jobs are live plans: their Shot and Screen content is
resolved again only when the user launches them.

## Render Queue

The Render action is a persistent icon on every Shot row. It always opens an
add modal with that exact Shot selected. Actor, Device, Theme and local Shot
details load independently of routing. The modal derives the route and
technical name from the exact manual contract or exact Shot Manager
associations. An associated Shot needs only its existing local root to resolve
offline; `production.json` is not a render-time dependency. If the applicable
root is unavailable, the modal keeps the details visible, explains the
prerequisite and disables enqueue. Output never falls back to a free folder
picker.

The Production root must already exist and resolve to a real directory. At job
start MOCKUPS may safely create missing Episode-path/workstream/folder
subdirectories inside that root. Existing symbolic links, files in the route,
or paths that escape the root are rejected. Materialization is additive and
never recreates a missing root or moves an earlier render.

The add modal exposes:

- the Shot Actor as read-only;
- Device and Theme initialized from the Shot's effective resources, with
  additional same-Project job-only overrides;
- Light, Dark or Both;
- the resolved Production Output route;
- a job-owned output mode;
- an editable safe base name.

The initial output modes are MOV ProRes 422 HQ, MOV ProRes 4444 with alpha,
MOV H.264 Light at 8 Mb/s, Standard at 20 Mb/s, High at 40 Mb/s, PNG sequence
and EXR sequence. H.264 uses the exact Créditos `libx264` profiles with
`yuv420p` and fast start; it never preserves alpha. Device raster frames retain
their exact declared dimensions. When either dimension is odd, only the H.264
encoding stage applies the smallest proportional Lanczos upscale whose output
width and height are both even, satisfying the chroma-subsampling contract
without cropping or adding a border. The worker premultiplies RGB against black
by the clean raster alpha before every output conversion. PNG, EXR and ProRes
4444 preserve that alpha channel; ProRes 422 and H.264 discard alpha after the
same premultiplication and therefore represent transparent pixels over black.
Every MOV frame and its QuickTime `colr` atom declare limited range, BT.709
primaries, the CSS sRGB transfer and BT.709 matrix coefficients. ProRes 4444
additionally writes the QuickTime video-media graphics mode as premultiplied
against black; the other MOV profiles retain copy mode and contain no alpha.
No output contains audio.

Each Render Queue output row presents the technical color range derived from
its exact output mode. MOV modes report Legal range; PNG and EXR sequences
report Full range. Alpha-capable outputs additionally report their
black-premultiplied alpha contract, while modes without alpha omit an alpha
label. These values belong to the output-mode catalog and are not duplicated
in queued job state.

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

Adding a batch persists only one live plan per child: exact Shot id, selected
Device and Theme ids, requested appearance and output target. It does not
prepare frames, copy assets, capture duration or store resolved Shot or Screen
documents. Every added child is immediately `PENDING`, and enqueue never starts
preparation or execution.

The user starts work with the Render Queue panel's **Render pending** action.
That action captures the exact pending job ids available at activation and runs
only that closed batch sequentially. At the start of each child, the preparation
owner reads the current Shot, its current ordered Screens, current durations,
Actor context, Device overrides and authored Preview inputs. It creates the
frame documents and referenced-asset store only under a unique temporary job
directory. The worker then uses the same raster-document pipeline as raster
Preview through its separate Chromium session and produces the clean Production
canvas with no editor chrome or device frame. The complete temporary preparation
is deleted after completion, failure or cancellation and is never written into
the queue document.

Jobs enqueued while a launched batch is active remain `PENDING` until a later
activation. A paused or interrupted active child returns to Pending after
application restart without restarting automatically; its next launch resolves
current authored state again. Retry creates a new pending plan and therefore
also renders the latest Shot and Screens rather than the earlier failed state.

The selected route is stored by stable route id. Its relative directory,
version padding and frame padding come from the resolved portable Project
contract. Its absolute root comes only from the workstation-local Production
Output root store.

The predefined relative route does not need to exist when the batch is
planned. The worker validates its containment under the existing workstation
root and creates any missing route directories when that job begins. Existing
route segments must be real directories rather than files, symbolic links or
reparse points. Planning and enqueue never create folders, and publication
still refuses an existing final output.

Monitoring is separate from batch creation. The permanent Production
**Render Queue** panel is available even when it is empty or the local
Production Output root is not configured. It groups child jobs by
batch and owns Render pending, pause/resume, cancel, retry, reveal, remove and
clear-finished actions together with progress, error and output-path reporting.
Render pending is enabled only when live pending plans exist and no previously
launched batch remains active. Job-start preparation reports its exact current
and total prepared frame count through the same stable
determinate progress control used by later execution. The row names the Screen
currently being prepared and shows prepared and remaining frame counts; these
are preparation counts, not rendered frames. Rows update their existing
controls in place and execution updates are ordered monotonically, so an older
notification cannot make a bar move backwards. The
add modal and this panel share one workstation-local queue manager.

## Screens

A Screen is a persisted Module Instance. It owns:

- exact App, Module and Module Variant references;
- order within its Shot;
- transition document;
- non-negative action delay in frames;
- Runtime Input payload in `content_json`;
- behavior and animation documents;
- selected duration policy from the Module's allowed policies;
- duration when that selected policy is explicit;
- current metadata.

The transition document is one complete boundary Motion. At a boundary between
two ordered Screens, the Shot starts both parent-owned events at the first frame
of the incoming Screen: the previous Screen uses its Motion as an exit and the
incoming Screen uses its Motion as an entry. Both events receive the same
elapsed Shot interval and resolve their own Theme timing. The outgoing Screen
is retained at its final local frame until both Motions complete. The first
incoming frame remains frozen at local frame zero throughout that boundary.
After both Motions complete, the Screen remains at frame zero for its authored
action delay; only then does its internal action timeline start. The first
Screen has no synthetic entry transition but applies its delay before its
actions; the last Screen has no synthetic exit. The effective Screen duration
and calculated Shot duration include entry transition, delay and action duration.
A Shot can select an explicit positive duration at its own timeline boundary;
that single resolved duration cuts the final Screen early or holds its final
frame after the calculated sequence ends for Preview and render alike.
`none` with no fade remains an immediate change.

Screen payload is authored in Preview because that is where its effect can be
checked, but ownership remains with the Screen instance.

The first Preview tab is an authoring host, not a Preview-owned data store.

The Runtime API diagnostic presentation is hidden in the current Design and
Production UI. Its implementation may remain available to internal tooling.

## Production context

Every Screen resolves through its exact Shot. A complete valid route is:

```text
Screen → Shot → owner Actor
              → effective Device (Shot override ?? Actor default)
              → effective Theme  (Shot override ?? Actor default)
              → visual context
```

Missing, blank or cross-Project context fails explicitly. App, Module, Variant,
name, type, order and position cannot supply an Actor, Theme or Device
implicitly. Actor identity always remains the Shot owner even when either
visual resource is overridden.

The Preview context shown to the user is derived from the selected Shot and
Screen. Switching to a referenced definition also switches to the correct
Design or Production workspace and selects the exact tree item.

## Conversation message ownership

The Actor attached to a conversation message describes the message owner and
is independent from the Shot owner:

- every incoming, outgoing and system message requires and retains one explicit
  same-Project Actor;
- changing message direction never clears, replaces or derives that Actor;
- the Conversation chat Actor is a separate required Runtime Input and never
  derives from the Shot owner.

Message direction and the chat Actor are independent discrete animation
targets. Both use `hold`. A direction keyframe changes the complete resolved
Bubble presentation for that frame—including side, alignment, colors, Actor
identity, avatar and name—without rewriting the message Actor. A chat-Actor
keyframe changes the header Actor and therefore its automatic Actor color when
enabled; it does not change Shot Theme, Shot ownership or message direction.
Design sample Actors are fixtures and never repair persisted Production
messages.

An outgoing message with an explicit animated `text` track keeps the
track-owned write interval for composer presence even though that track
replaces the bubble's base write-on. Text Input Bar and Keyboard therefore
remain visible while the animated text is being authored on screen.

Conversation owns one shared message Motion in its Module Variant. Each
message has a parent-owned presence interval: its In remains the serial
arrival derived from write-on/hold/delay, while a zero visible duration keeps
it through the Screen end and a positive duration defines an explicit Out.
Only an explicit pre-boundary Out runs the shared Motion in reverse. Presence
never delays the next message and never retimes or rewrites message keyframes.
For a calculated Conversation Screen, the latest positive message Out extends
the action duration and therefore the reachable playhead range. Conversation
also allows an explicit duration on each Screen instance. Its Duration field
and General timeline lane are the same authoring value, may end before or after
the messages, and clip message content at the Screen boundary without retiming
it. Conversation header presence follows the Screen boundary, so an explicit
duration can keep the header after the final message disappears.

## Playback

Play resolves the selected Production context and presents complete frames in
the Preview panel. Replaying unchanged state reuses the prepared result.
Restore returns temporary playback state to the current authored frame.

Escape cancels preparation as well as active playback. Cancellation does not
mutate the Screen payload or animation document.

The Shot reference video is independent of Preview's Split reference. Before
In is marked, its native controls own an independent reference playhead so the
author can locate the intended frame. `Set In` persists that current video
frame; it coincides with absolute Shot frame zero from then on, so the shared
playhead resolves `video frame = In + Shot frame` across every ordered Screen.
The video is interpreted at the Shot FPS. When that frame is outside the media
duration, the reference window presents `Sin media`. While the window is
visible after In, Production Play starts the reference player from the same
shared frame and pause, stepping and scrubbing project that playhead back into
it. Replacing the source clears In and its video-owned markers.

Reference markers remain owned by video time. Before In they stay editable in
the reference window but have no Shot or Screen Timeline projection. Setting or
changing In projects them without rewriting their stored frames. Markers may
exist before Screens or outside the current Shot duration; only markers inside
the visible timeline range are painted.
