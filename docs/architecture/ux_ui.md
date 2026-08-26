# UX and UI

Status: normative.

## Two workspaces

The top-level navigation separates two user intentions:

- **Design** defines reusable visual resources, Components, Variants, Apps and
  Modules.
- **Production** assembles Episodes, Shots and Screens from those definitions
  and authors each Screen payload and animation.

Cross-workspace navigation is explicit. Opening a Usage reference or class
action activates the correct workspace, expands the exact tree branch, selects
the item and opens its editor.

## Three-panel shell

The desktop shell has:

1. navigation tree;
2. contextual editor;
3. Preview and Preview authoring.

`EditorWorkspaceCoordinator` owns the immutable session snapshot and decides
tree refresh, workspace, Production, root selection, embedded context and
revision transitions. `MainWindow` owns only visual composition, event wiring
and application of those transitions to navigation, editor and Preview hosts.
Each editor owns its domain fields and collections.

Responsive behavior protects the current task:

- dividers are resizable;
- the top-left navigation-panel action collapses the complete left panel and
  its splitter while retaining its exact expanded width;
- the action belongs visually to the Navigation header; while collapsed, only
  its narrow left-edge recovery rail remains;
- collapsing Navigation keeps Preview at its exact current width and gives the
  complete released width, including the hidden splitter, to Editor;
- restoring the panel, including after restarting the application, returns to
  the retained Navigation, Editor and Preview widths;
- an explicit routed navigation from Usage, Preview, an embedded reference,
  history or a newly created owner reveals the panel before selecting and
  bringing the exact tree node into view;
- the horizontal division between Preview authoring and Preview is resizable;
- compact widths preserve the selected editor and Preview controls;
- each panel owns its scroll instead of creating nested page scroll traps.

## Context and breadcrumbs

The editor header identifies the complete current context. For a Component or
Module it includes the selected Variant as part of the breadcrumb/context row,
aligned with status and lifecycle actions.

Changing between records of the same class keeps the same open card and scroll
level. Returning to another editor restores that editor class's session point.
This memory lasts only for the current application session.
Committing a field prepares one replacement shell candidate for the current
root or embedded editor and its Preview-authoring surface while the existing
cards and panels remain mounted. Editor cards, header and Preview authoring are
published together with the captured expansion and scroll state. Navigation is
rebuilt in that same visual turn only when its complete presentation changed;
an unchanged tree retains its mounted controls. Loading surfaces are reserved
for transitions that do not already present that exact owner.

Component and Module headers expose compact Back and Forward actions after the
Variant actions. They traverse the exact sequence of Design editor visits,
including the selected Variant and embedded breadcrumb context. Navigation
restores the existing card and scroll memory but does not undo authored data,
Variants or Overrides. A new visit after moving back discards the forward
branch, missing owners are skipped and the complete history starts empty in
each application session.

## Cards and internal navigation

Cards represent meaningful owner groups, not arbitrary nesting. Shared metadata
chooses flat stacks, vertical child navigation or separated sections.

Tabs are used when the views are peers of the same task. Breadcrumbs identify
location; tabs change a local view; cards group authored ownership. These
patterns are not substituted for one another.

Embedded navigation actions use the shared compact action set:

- Variant selector;
- navigate to class;
- local Overrides;
- Forward state when declared.

Fixed Component boundaries do not ask the user to select a Component that the
contract already fixes.

## Preview authoring

Design places Preview authoring above the Preview in two horizontal tabs:

- **Test Values**
- **Preview**

Preview combines the visual-context setup and the generic Preview controls in
one scrollable Design surface. Both sections retain their existing semantic
owners and state. Production uses the same combined Preview surface; its
production context and Shot transport remain visible within that tab.
That complete upper Preview surface can move between its dock and one shared
resizable tool window. The floating window is topmost and uses the same live
controls and state in Design and Production; detaching never creates a second
controller or a second Preview session. While detached, the main Preview
receives the released vertical space. Closing the tool window or using its dock
action returns the surface and its prior dock height.

Application modals always take precedence over that topmost utility window.
While a modal is open, the detached Preview is lowered and disabled; closing
the modal restores its prior topmost and interaction state.

Floating position and size are remembered only for repeated detachments in the
current application session. A new application session always starts docked,
so a tool window cannot reopen outside the available displays after a monitor
change.
In Production, Fit, playback route, markers, canonical frame, reference mode
and the unlabelled Orientation selector share one compact responsive row.
Shot navigation follows directly below. The timeline slider occupies the
remaining width of the next row and its numeric current/maximum frame sits at
the right; no redundant Shot/Screen timeline label is visible. When Split is
active, its reference controls follow immediately below the slider.

The far-right Production transport action uses the shared Video icon to toggle
the selected Shot's resizable topmost reference-video window. Shot General
associates the video through the registered `VideoFilePath` Browse control.
The reference window starts muted and provides Audio, Set In and marker
actions. Its marker strip supports stable-marker selection and drag; the lower
text area edits the selected marker and the delete action removes it. The same
markers appear as amber ticks on the Shot slider and the selected Screen ruler
after In is marked. The video surface exposes no browser-native controls. Its
shared compact navigator owns previous frame, Play/Pause, next frame, a video
frame slider and current/total frame count. Until In, that navigator remains
independent so the author can seek and play to choose the exact frame. After
`Set In`, the same controls route to the shared Shot playhead and the window
labels the synchronized frame. Replacing the associated video clears its prior
In and markers.
Cancelling the reference picker is silent. A selected video that cannot be
associated reports an explanatory modal message and preserves the prior value.
Opening an authored reference whose file is missing or unavailable reports an
explicit error message while the window retains its `Sin media` presentation.

The Test Values view keeps temporary-data actions and Play/Restore controls in
a fixed upper surface. Every finite action also exposes shared previous-frame
and next-frame chevrons immediately after Restore. A compact numeric current
frame and read-only maximum frame sit between the chevrons. The numeric field
uses the shared compact numeric density rather than the form-field padding.
The generic action control places one compact full-width frame slider below
that transport row. It uses the same zero-to-maximum frame range and stays
bidirectionally synchronized with direct frame entry, stepping, Restore and
playback. Its host preserves the complete theme-owned thumb bounds while
overlapping the transport row vertically, so the track has no artificial gap
and the thumb is never clipped.
Play, Restore and both chevrons stay available while the action exists,
including during playback and at either endpoint. Restore holds frame zero. A
frame step or direct frame entry stops active playback and leaves the resolved
frame visible. Values below zero resolve to zero and values beyond the action
duration resolve to its maximum frame. The scrollable value groups remain
below, so playback actions stay visible while editing long input sets.

Production places the selected Screen Payload in the corresponding Preview
authoring area. Runtime Inputs and structured slots are edited beside the
result they control. Persisted payload remains owned by the Screen. The utility
tabs use the order **Screen Payload**, **Timeline**, **Preview**. Screen Payload
keeps the animation activation glyph beside every animatable Runtime field, but
contains no Animation detail sections: activating a field creates its track and
the selected owner lane in Timeline is the only detailed animation editor. A
**Timeline** tab appears for a Production Screen. It uses one compact transport
above a tick ruler, then a General lane and one labelled group per Runtime
collection with one lane per stable item. Collection groups are collapsible and
use the standard card affordance at the right (`>` closed, `v` open); their
expansion state is session-only. The complete tick band is the playhead's input
surface, while the line crossing the lanes remains visual only. The ruler is relative to the Screen:
entry transition and waiting appear before frame zero, content begins at zero,
and exit transition appears as postroll. Pre- and postroll use a subdued
diagonal hatch rather than authored-boundary guides. One uninterrupted playhead
crosses the ruler and every visible lane. It snaps to visible lane boundaries
and, when exposed, keyframes; a snapped playhead becomes amber. Item blocks can
move horizontally and can be shortened only from their outgoing edge. Their
edges snap to the playhead and the boundaries of other visible blocks, showing
one thin amber guide across the complete timeline while snapped. The active
block outline also becomes amber; when its detent is the playhead, the playhead
becomes amber as well. Collection collapse and zoom remain session-only.
Releasing an editable block commits through the collection's declared offset
or presence-duration field and refreshes Preview from the newly prepared
payload. The viewport always retains a session-only authoring horizon to the
right of the effective Screen range, including when the playhead is at the
last frame or zoom changes. While a duration-affecting item boundary is dragged,
General, the content boundary and the frame counter project the growing
calculated Screen extent immediately, and the scale rebases the active drag so
the same boundary retains a trailing horizon without jumping in frame value.
Releasing it persists the declared item field, recalculates the Screen and Shot
through the common timeline owner and confirms that extent. The horizon itself
is never persisted. An outgoing-edge edit never creates or changes retime.

Selecting General or any item lane highlights the complete lane, including its
label, and opens one Keyframes section after the final visible lane. The section
reuses the standard animation track and field editor but aligns its ruler and
markers to Screen time. Only tracks owned by the selected stable target appear.
Their displayed positions follow the lane when it moves; the authored values
remain relative to the lane and are not rewritten. Collapsing a collection
whose selected lane is hidden returns the selection to General.

Every serial collection-item block starts at its resolved position in the
Screen. A declared pre-duration field is edited as a signed gap from the prior
block's outgoing edge: positive separates, zero joins and negative overlaps.
Collections without that declaration use zero. Stack State lanes instead span
the Screen and show filled active intervals over a subdued hatched inactive
range. Multiple filled intervals on one lane represent re-entry. Their shared
In/Out boundaries edit the existing selector keyframes.

A compact scale control shares the transport row and aligns to its right edge,
leaving the ruler and lanes the complete remaining width. Its larger thumb,
visible center tick and pointer detent restore `1:1`, where the viewport is
exactly the declared Screen range. Moving right zooms in; moving left zooms out
and reveals additional time before and after that range. Each scale change
anchors on the current playhead position. Scale and viewport are session-only.
Collection item entry remains at or after Screen frame zero, while an item's
outgoing edge may be dragged beyond the current Screen range when the expanded
viewport reveals it. After commit, a calculated Screen expands through the
latest explicit collection-item Out; an explicit Screen keeps its declared
range.

The visible Preview utility headers remain in one horizontal row at the
supported 1040 px minimum and the 1440 px default window widths. The Preview
column has a real minimum independent of star sizing. The setup section uses
four columns only when its measured content width allows them, otherwise it
reflows to two rows and finally one scrollable column. Splitter movement, the
selected utility tab and session state remain intact across these layout
changes.

Preview state is visibly distinct:

- idle;
- resolving;
- preparing HTML;
- playing;
- cancelled or failed.

Repeated playback of unchanged input is immediate. Escape works during
resolution, preparation and playback.

Fixing a Preview context retains its exact visible breadcrumb and context label
through refreshes and editor or workspace changes. Releasing it keeps that
chrome mounted until the newly selected Preview context is ready.

In the interactive Preview, hovering a resolved element shows its exact
renderable identity. Right click pins that element and presents its ordered
rendered ancestor path. Every path level with an explicit authoring target is
selectable. Selection opens the exact Design owner and Variant, then the exact
nested Overrides context represented by its declared slot chain. When that
target also declares an exact visible dictionary field, the completed editor
transition expands the unique top-level card containing that field and brings
it into view, including when the card was already open. A structured field may
also declare the exact stable item id that governs the rendered element; its
registered dictionary control selects that item without deriving it from the
renderable name or collection position. Full Component Variant references
cross to that referenced Variant as the new authoring owner, while
`ComponentVariantSlot` boundaries retain their current owner and append their
declared local Overrides path. This explicit focus supersedes pending session
scroll restoration only for that navigation. A missing or ambiguous
field-to-card or field-to-item match reports a warning and never falls back to
a label, prefix, position or first match. A level without an explicit target is
disabled and reports that it has no associated editor. The interaction never
derives authoring context from a renderable id, primitive type, label,
hierarchy position or visual geometry.

When a finite action completes, its final frame remains visible and Play
becomes available again. Play repeats the same initial-to-final action without
repreparing unchanged frames. Restore returns to the captured initial state.
Moving the authoring surface between visual hosts does not leave either
transport control disabled.

## Render Queue

Every Shot row keeps one compact Render action visible independently of
selection. The action remains enabled when no output route is currently
available: it still opens the add modal, shows the local Actor and defaults,
derives the route from the Project contract and stable Shot number, and
explains a missing workstation root in place.

The Project Production Output card exposes the explicit Shot Managed switch.
Manual mode keeps the workstation root and current naming/route fields. Shot
Managed mode browses a workstation-local `production.json` and presents only
workstreams and folders declared by that document, without interpreting their
meaning. Its resolved optional suffix is informative. Episode association is
an optional selector ordered and labelled by external Episode order. Shot
association is an optional selector filtered by the owning Episode's exact
external id and labelled with canonical Shot names. Clearing a Shot association
immediately restores manual output; missing referenced items become visibly
free while retaining their identity for later recovery. Project state
distinguishes not associated, setup incomplete, associated and connected, and
associated but disconnected. A disconnected association keeps its captured
route and naming values but cannot create or refresh selectors.

That modal owns only creation of a new batch. Actor is informative and
immutable. Device, Theme, appearance, output mode, predefined route and base
name are explicit. The route control shows the resolved relative directory,
whether manual or Shot Managed, and proposes the first available option
when no prior selection applies and has no arbitrary folder alternative. The
proposed automatic version and final
child names update before enqueue. A missing physical route directory does not
disable the form: the render worker safely creates missing subdirectories from
the frozen manual or Shot Managed route when the job starts. The root itself
must already exist; symbolic links, files in the route and escapes are rejected.

Confirming the modal closes it immediately and creates visible `PREPARING`
Light/Dark children. Preparation uses the stable determinate job bar and shows
the exact Screen, prepared frames and remaining frames without presenting
those source frames as rendered output. Canceling either child during this
atomic preparation cancels the complete batch; once preparation finishes, the
children are independent queue jobs.

Production also exposes a permanent **Render Queue** section alongside
Episodes and Production Data. Its central panel remains accessible with an
empty queue or without a configured local Production Output root. It groups Light/Dark children by batch
and shows phase, frame progress, errors and final output. Pending or active
work can be canceled; failed or canceled jobs retain a retry snapshot while
available; completed work can reveal its output; terminal history can be
cleared. Each job row and progress control remains mounted while values change;
render progress is monotonic and cannot visually restart on every frame.
Pause lets the active job finish and prevents the next pending job from
starting.

## Lifecycle consistency

An action may be available in more than one useful context.

When an action is valid both in the tree and editor, it uses the same label,
rules, result and confirmation in both places. Rename is therefore consistent
between Design and Production surfaces.

App and Module definitions expose Rename only. Module Variants expose Create,
Duplicate, Rename and conditional Delete. Other records expose only actions
allowed by their exact owner and Usage state.

Deletion confirmation presents each blocking Usage reference as a navigable
link. Activating it closes the dialog, switches workspace when necessary,
opens the exact tree branch and selects the owner.

## Selection and Overrides

A selector displays the current complete Variant. Crossing into another class
chooses its protected Default Variant only at that explicit boundary.

Overrides are local and visible through the shared action. The UI never hides a
class change, silently replaces a Variant or manufactures Overrides from
position.

Every Overrides action that reveals editable dictionary fields is contextual
editor navigation. It publishes one exact coordinated context, breadcrumb,
central card set and session view-memory key. It never opens a modal, utility
window, parallel panel or temporary editor. Modals remain bounded confirmation,
selection, import or search workflows and never host persistent dictionary
authoring.

Declared `RecordReference` Overrides use this same action and standard
inherited controls. Their metadata declares the referenced class, sparse local
document and exact field set; shell and shared editor services never route by a
concrete field, record or owner pair. In the Shot Device row, changing the
selected Device keeps the Shot-local values; each field's Restore action
removes only that local value. The Shot and Device editors contain no
override-specific controls or branches.

Component and Module Variant editors also expose a flat `Overrides (N)` peer
view. It shows the standard controls for every locally overridden inherited
field owned by that Variant, across its declared embedded boundaries. It hides
all inherited fields and all direct Variant fields. One path header identifies
each owning boundary. Fields with the same owner path share that header and
form one continuous compact group. The group fits the measured Overrides
viewport without horizontal clipping. Restore uses the normal field action and
the row disappears after the confirmed write.

## Structured collections

Collection rows display stable identity, useful summary and owner actions.
Add, remove, reorder, duplicate and State changes preserve ids and explicit
references according to the collection contract. All structural actions use
the shared collection command surface for both Variant and Runtime authoring;
an editor never implements a collection type's lifecycle locally.

Every conversation message exposes one required Actor independently from its
incoming, outgoing or system direction. Changing direction preserves that
selection. Direction and the chat-level Actor expose standard animation
activation and discrete `hold` keyframes; resolving a direction keyframe
immediately applies the complete corresponding Bubble appearance.
The Conversation Layout card exposes `Messages reflow timing` as duration and
easing only. It controls both the upward/downward displacement that closes a
message gap and the auto-scroll that accommodates a newly visible message;
message presence and composer viewport Motion remain separate fields.

## Animation UX

The selected Screen shows its effective local range while Preview retains the
absolute Shot playhead internally. Its internal keyframes remain relative to
the action origin after transition and delay. Keyframe selection and drag use
one standard interaction. Owner, target and field identity remain visible
enough to avoid position-based editing.

The animation authoring playhead and its keyframe lane are one compact control:
a directly draggable, ticked frame ruler whose vertical cursor begins at the
tick baseline and reaches the lane, with only a 2–4 px visual gap between them.
Its compact head terminates at the tick baseline and its visual line has a wider
invisible pen/touch capture area. Both share the identical
horizontal extent, excluding only the session-only `+` horizon action, so frame
positions have one unambiguous visual meaning. The selected track is
distinguished with compact amber diamonds; keyframes from other tracks remain
small circles and the current-frame marker is blue. These tracks always share
the exact temporal owner: a Screen field uses the Screen ruler, while fields in
one collection item use that item's local ruler from its first appearance.
Different collection items never share a lane. Protected origin markers use a
vector outline instead of a filled marker; labels never depend on Unicode glyph
coverage.

The Screen General card exposes its boundary Transition through the registered
Motion control used by Components and its Action delay through the registered
integer control in frames. Playing the Shot starts the outgoing and incoming
Motion together at each Screen boundary, holds the incoming Screen at local
frame zero for the delay, then starts its actions.

Play and Restore apply to the currently visible authoring context. Cancelling a
drag or playback returns to the current authored frame without writing
temporary values.

## Input behavior

Text and numeric fields follow standard desktop selection for mouse, Wacom Pen,
touch and keyboard. Double click selects the full numeric value. Single-line
text and numeric drafts persist on Enter or focus loss; multiline drafts
persist on focus loss. A typing pause never commits. Every desktop
slider uses the shared input behavior: native mouse, touch and keyboard remain
intact, while primary Wacom Pen press owns a stable capture across the full
track from the initial press. Pen motion keeps only the latest value and applies
it at render priority, while release commits the exact final position
synchronously without retaining an event backlog. Shared
text input behavior suppresses native contextual editing popups, including
those produced by compound numeric templates, while standard keyboard
cut/copy/paste shortcuts remain available. Shared action buttons and icons are
reused throughout; editors do not invent alternative chrome for an existing
operation.
