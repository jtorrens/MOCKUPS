# Animation and time

Status: normative.

## One frame clock

Preview uses one absolute Shot playhead internally. Editors project that clock
into the selected Screen's local authoring scale.

```text
Shot frame
→ Screen origin
→ owner appearance origin
→ owner-local field frame
```

Frame conversion belongs to the common timeline. Editors, payload factories,
resolvers and renderers do not reproduce the formulas.

Shot reference video joins that same absolute clock only after its nullable In
is marked. Before In, the floating player's own native playhead locates the
reference frame. Persisted In and markers remain video-relative Project-FPS
frames; the common Production timeline then projects them to Shot and Screen
ruler frames. The floating player may drop visual frames to follow the shared
playhead, but it never becomes a second timing owner after synchronization.

Production Preview exposes a Screen-relative Timeline over that same clock.
Its visible range includes three contiguous zones: negative preroll for the
incoming transition plus action delay, editable Screen content beginning at
frame zero, and positive postroll for the outgoing transition into the next
Screen. The playhead may traverse all three zones. General and collection-item
lanes may be manipulated only inside the content zone; preroll and postroll are
parent-owned playback context and use an unfilled presentation.

Timeline edits commit through the existing temporal owners. Serial collection
movement writes the collection's declared pre-duration field; outgoing resize
writes its declared parent-owned presence-duration field. Presence duration is
not retime and never changes the item's local clock, its sequencing completion
or its keyframes. Neither operation rewrites local keyframes.
Collection item lifetime defaults remain collection-contract owned rather than
inferred from the Screen or collection name. Keyframes outside a resized item
or Screen range remain authored at their existing owner-local frames.

Each serial collection lane begins at the parent-owned position resolved by
the common owner timeline. A declared `preDurationFieldIds` value is the signed
offset from the preceding item's sequence end, or from Screen frame zero for
the first item. Positive values create a gap, zero makes the items contiguous,
and negative values overlap them. Moving a lane writes that same field; no
second timing document is created. Collections without a declared offset use
zero.

A collection may declare `animationTimeline.presenceDurationFieldId`. Zero is
the explicit automatic sentinel: the item remains present through the end of
the Screen. A positive value is the number of Screen frames from the item's In
to its explicit Out. The collection's shared item Motion runs forward at In and
backward so that it completes at an explicit Out. An automatic Out coincident
with the Screen end does not start a redundant item exit; Screen Motion owns
that boundary. For a calculated owner, its effective duration includes the
latest positive explicit Out across every declared collection; changing an Out
therefore extends that Screen without changing collection sequencing or local
keyframes. An explicit Screen duration remains authoritative. Conversation
messages use this contract and share one Motion recipe from the Conversation
Module Variant.

Conversation owns message geometry separately from message presence. Its
`Messages reflow timing` is one duration/easing contract for both keyed
vertical reflow after an explicit message Out and the vertical auto-scroll
needed when a newly visible message exceeds the viewport. The Bubble remains
the owner of its resolved appearance and the Conversation message Motion
remains the owner of Enter/Exit; neither owns sibling displacement. At the
discrete frame boundary, the first frame of the new message layout consumes
the first reflow sample so no unchanged hold frame separates Exit completion
from sibling movement.

## Temporal ownership

Every temporal entity follows one rule:

- appearance, disappearance, activation and selection are authored in the
  local time of its parent;
- the entity's own fields and keyframes are authored relative to its first
  appearance;
- moving or reordering an entity recalculates effective frames without
  rewriting its stored local keyframes;
- re-entry restarts parent-owned Enter/Exit Motion but does not restart the
  entity's internal timeline;
- stable ids, never indices, bind owners and tracks.

This applies recursively to Shots, Screens, stack slots, States, structured
collections and nested Components.

An ordered Screen boundary follows that same rule without a separate transition
timeline. The Shot owns the boundary event. On the incoming Screen's first
frame, the outgoing Screen exit Motion and incoming Screen entry Motion start
simultaneously from one elapsed parent clock. Each Screen supplies its own
complete reusable Motion recipe and resolves its own Theme duration and easing.
The completion dependency is the longer of the two Motions. During that
dependency the outgoing Screen keeps its final local frame and the incoming
Screen remains frozen at local frame zero. Once both Motions complete, the
incoming Screen consumes its non-negative action delay, still at local frame
zero, and only then starts its internal timeline. The first Screen has no
synthetic entry Motion but consumes its action delay before its timeline starts.
The effective Screen extent is entry-transition frames plus action-delay frames
plus its calculated or explicit action duration. Shot calculated duration is the
sum of those effective extents. A Shot may instead own a positive explicit
duration. The common Shot timeline resolves that effective extent for editor,
Preview and render: a shorter explicit extent cuts the timeline; a longer one
holds the final local frame of the last Screen.

The event clock and the visual Motion recipe have distinct owners. A child
Component Variant may declare its reusable boundary Motion, while the parent
still owns the appearance or disappearance event, its start frame and any
resulting collection reflow. List follows this rule: List Item defines how one
item enters or exits, List times `Present` and reflow, and List defines a
separate boundary Motion for the complete List.

## Persisted tracks

Parameter animation is persisted only as version 2 tracks identified by stable
`fieldId` and `targetId`. A track is relative to its declared owner.

Discrete Conversation direction and chat-Actor tracks use `hold`. Direction is
message-owner-relative and changes presentation without changing the message's
stable Actor reference. The chat Actor is Screen-owner-relative, resolves
through the prepared Runtime record-reference catalog and remains independent
from the Shot Actor.

The common owner timeline derives:

- effective origin;
- completion dependencies;
- finite action duration;
- non-sequencing fields;
- absolute Preview frame projection;
- retime projection.

A serial Runtime collection may declare
`animationTimeline.sequenceCompletionFieldIds`. When present, only completion
from those exact declared item fields advances the next item's start. Other
field tracks and finite actions remain on the item's local timeline and may
extend the owner's total duration without delaying later collection items.
Conversation uses this boundary so only the message text/write-on completion,
followed by its hold and the next message delay, sequences message arrival;
delivery state, media playback and full-screen actions may overlap later
messages.

An editor never stores absolute Shot frames in a child-owned keyframe.

Production Screen animation authoring receives one immutable prepared snapshot
containing the exact animation document, Screen origin and current duration.
Persistence and timeline reads run through the session operation coordinator
before visual construction. Animation controls consume that snapshot, reject a
snapshot for another Screen and perform no synchronous persistence read while
creating or refreshing their visual surface. A committed animation write
returns the replacement snapshot through the same boundary.

Animation edits are serialized semantic commands over the last confirmed
snapshot. Each command creates its candidate only after the previous command
has succeeded or failed. Success replaces the complete prepared snapshot;
failure restores the confirmed document and reports the error in the editor.
The visual timeline never treats an unawaited write as committed and rapid
keyframe, track or retime commands cannot overwrite one another from a shared
stale JSON copy.

Production Preview navigation separately receives a Project-wide immutable
timeline catalog. Shot controls use its ordered Screen ranges and absolute
keyframe frames; visual callbacks do not reload a Screen, walk persisted slots
or recalculate the Shot timeline.

## Duration policies

A Module declares one Screen duration policy:

- `calculated`: finite actions and collections determine Screen extent;
- `explicit`: the Module Instance frame count is authoritative.

Those policies determine action duration, not the parent-owned entry interval.
The common Screen timeline prepends the resolved entry transition and the
authored action delay when it calculates effective Screen and Shot duration.

An explicit policy declares a positive default and is edited only on the
Screen instance. Child keyframes and composition cannot extend it silently.

The authoring horizon is session-only for both policies. The Screen timeline
always reserves trailing frames to the right independently from the playhead
anchor and zoom; extending a lane grows only its semantic duration, and the
Screen timeline projects the resulting calculated extent live while dragging
and expands its viewport as needed to preserve the trailing horizon.
On release the declared mutation is persisted, the common owner recalculates
the effective Screen and Shot durations, and the refreshed viewport reserves a
new trailing horizon. The compact animation track may additionally expose its
`+` action. Neither form of horizon is duration or persisted data.

## Behavioral timing

Reusable action duration uses dictionary `BehaviorTiming`:

- fixed mode resolves authored frames;
- natural mode resolves semantic units × the Module-owned base rate × a
  `theme.motion.naturalPace.*` multiplier.

The owning resolver determines deterministic internal cadence inside the final
duration. Bridge and renderer receive only the resolved state for the requested
frame.

An animatable text field whose completion references a `BehaviorTiming` field
uses that standard action while no parameter track exists. Activating its track
converts the action to an equivalent explicit pair: protected frame zero stores
empty text with `hold`, and the resolved completion frame stores the full text
with `writeOn`. The track then becomes the single timing owner and the referenced
duration field is disabled until the track is removed. This conversion changes
no visible frame and avoids composing two independent text reveals.

Contract-declared finite and base durations use the shared reference-duration
lane. Retime is disabled when `targetDurationFrames` is absent.

## Action transport

A finite Runtime action captures one temporary origin before its first Play.
Completion leaves the visible result and playhead at the final frame, returns
the transport to idle and enables Play again. Repeated Play first restores that
same captured origin internally and then executes the same initial-to-final
action; it does not reinterpret a toggle from the previous final value.

Prepared Design frames are session-only and keyed by an exact cryptographic
request signature containing the resolved payload, complete action and Preview
setup. An unchanged replay retains and reuses that preparation. A changed
payload, action, Theme, Device, orientation, route or visual setup produces a
different signature and prepares a new result.

Restore stops the action, consumes its temporary origin and returns immediately
to its initial frame and values. Reparenting an editor surface must not detach
its transport-state observation permanently; Play and Restore follow the
current common playback state whenever the surface is attached.

## Keyframe interaction

Keyframes are selected and dragged through the shared timeline interaction.
Drag converts pointer movement into the selected temporal owner's authoring
scale and commits a valid owner-local frame. Screen-owned fields use the Screen
timeline. Fields owned by one collection item use a timeline starting at that
item's first appearance; tracks from another item never enter that lane or its
transport. Multiple animated fields owned by the same item share that one local
timeline.

The animation playhead and the keyframe lane use the same bounded owner-local
scale. Preview projects that local frame to the Screen/Shot playhead and projects
global navigation back while the owner exists. The active track uses compact
amber diamonds, other tracks from the same owner use discrete circles, and any
keyframe at the current playhead is blue while retaining its track shape. A
protected frame-zero keyframe uses the outline form of its vector marker;
editable keyframes use filled markers. A destination keyframe owns interpolation for the
preceding segment: `hold` preserves the source value, while `writeOn` resolves
the source-to-destination text at every intervening frame. That one resolved
value is the value shown by both the animation editor and Preview; a concrete
Module must not apply a second text reveal after track resolution, while its
composer and keyboard remain visible for the resolved field-completion interval.
Inserting a keyframe within a segment captures that exact resolved value and
inherits the destination interpolation, preserving the segment before further
editing.

During drag:

- the keyframe keeps its stable track and owner;
- preview playhead updates through the common frame projection;
- bounds come from the owner timeline and current duration policy;
- cancellation restores the uncommitted position.

No drag path identifies a keyframe by visual position or collection index.

Production Screen Timeline presents this same Screen-relative coordinate space
as a compact multi-lane surface. Its single continuous playhead snaps to visible
item boundaries and to keyframes once keyframe markers are projected into that
surface. Screen Payload owns only Runtime values and the
per-field activation glyph that creates or removes a track; it never embeds a
second Animation detail editor. Selecting the corresponding General or item
lane exposes the complete track and keyframe editor in Timeline.
Item appearance blocks snap their start or outgoing edge to the
playhead and to other visible item boundaries. Snap feedback is transient and
amber; preroll and postroll boundaries remain neutral, diagonally hatched
regions. Collection collapse and viewport zoom are session-only. Block edits
persist through their declared collection fields or animation document and do
not rewrite owner-local keyframes.

Selecting General or one stable item lane selects that exact temporal owner.
The complete lane receives the selected treatment and one contextual animation
section opens below the final visible lane. That section contains only tracks
owned by the selection. Its keyframes are projected onto Screen-relative
positions for presentation and playhead interaction, while their persisted
frames remain owner-local. Moving a collection lane therefore moves every
projected keyframe with its owner without rewriting any keyframe frame. General
uses the Screen action origin; a collection item uses its first appearance.
Tracks belonging to another item never enter the selected section.

The Screen Timeline viewport is independent of the Screen duration contract.
At `1:1` it presents the declared Screen range. Session-only zoom can expand the
viewport around the current playhead to expose a prospective item Out during a
drag and keyframes outside that range without clamping or retiming them.
Collection item entry cannot move before Screen frame zero. Committing a later
explicit item Out extends a calculated Screen to that Out; an explicit Screen
keeps its authoritative duration and may retain an Out beyond its range.
Viewport scale itself never changes completion dependencies or duration.

Parallel Stack slot lanes default to the complete Screen range. A child
collection with `ownerOrigin.kind: firstMatchingValue` is presented as State
lanes over that same range: active intervals are filled and inactive intervals
are subtly hatched. Intervals are derived from the source selector track, not
stored as additional entities. Under replace behavior, an outgoing edge and
the following incoming edge are the same selector keyframe. Dragging either
edge moves that keyframe; dragging a bounded active interval moves both of its
selector boundaries while preserving its duration and without crossing its
neighbors. The initial frame-zero boundary and the derived Screen-end boundary
are not movable. Re-entry creates another active interval on the same lane and
does not restart the State's internal timeline.

## Frame-by-frame Preview

Animation is resolved frame data. For every requested frame:

1. the timeline computes exact local frames;
2. each Module and Component owner resolves its state;
3. renderables emit generic resolved primitives;
4. the renderer paints that frame.

The web layer does not run timers, CSS animations, countdowns or
Component-specific interpolation.

Temporal render contracts carry resolved state such as `active`, normalized
`progress`, current text, current playback time and child-local frame. Raw
elapsed milliseconds, frame-rate conversion and track evaluation remain in the
owning resolver. Renderables may apply resolved progress to geometry but never
calculate that progress.

Continuous visual state follows the same rule. Cursor owns its fade duration
and minimum opacity, resolves the exact opacity from its owner-local frame and
passes that number to its renderable. Text Box and Text Input Bar forward the
frame through their declared embedded boundaries; they do not restart the
Cursor clock or paint a fixed replacement opacity.

Button pressed-state duration belongs to its declared Runtime action and
`BehaviorTiming`; Button Variant config does not persist a second duration.
Likewise, Text Input Bar persists no Cursor blink duration. It forwards the
resolved child frame and Cursor remains the only owner of that continuous
state.

Screen transition composition follows the same frame-data boundary. Payload
preparation selects the two exact Screen owners and their local frames. The
generic transition resolver applies the existing Motion timing helpers to the
shared elapsed interval and emits two resolved layers. The HTML renderer never
starts an animation or chooses an outgoing or incoming Screen.
