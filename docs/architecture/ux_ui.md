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

Design places Preview authoring above the Preview in three horizontal tabs:

- **Test Values**
- **Preview Set Up**
- **Preview Controls**

The Test Values view keeps temporary-data actions and Play/Restore controls in
a fixed upper surface. Every finite action also exposes shared previous-frame
and next-frame chevrons immediately after Restore. A compact numeric current
frame and read-only maximum frame sit between the chevrons. The numeric field
uses the shared compact numeric density rather than the form-field padding.
Play, Restore and both chevrons stay available while the action exists,
including during playback and at either endpoint. Restore holds frame zero. A
frame step or direct frame entry stops active playback and leaves the resolved
frame visible. Values below zero resolve to zero and values beyond the action
duration resolve to its maximum frame. The scrollable value groups remain
below, so playback actions stay visible while editing long input sets.

Production places the selected Screen Payload in the corresponding Preview
authoring area. Runtime Inputs and structured slots are edited beside the
result they control. Persisted payload remains owned by the Screen.

The three Preview utility headers remain in one horizontal row at the supported
1040 px minimum and the 1440 px default window widths. The Preview column has a
real minimum independent of star sizing. Preview Setup uses four columns only
when its measured content width allows them, otherwise it reflows to two rows
and finally one scrollable column. Splitter movement, the selected utility tab
and session state remain intact across these layout changes.

Preview state is visibly distinct:

- idle;
- resolving;
- preparing HTML;
- playing;
- cancelled or failed.

Repeated playback of unchanged input is immediate. Escape works during
resolution, preparation and playback.

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

That modal owns only creation of a new batch. Actor is informative and
immutable. Device, Theme, appearance, output mode, predefined route and base
name are explicit. The route control shows the Project-owned relative
directory, proposes the first available option
when no prior selection applies and has no arbitrary folder alternative. The
proposed automatic version and final
child names update before enqueue. A missing physical route directory does not
disable the form: the render worker creates it from the selected stored route
when the job starts.

Confirming the modal closes it immediately and creates visible `PREPARING`
Light/Dark children. Preparation uses an indeterminate indicator and never
reports its source traversal as rendered frames. Canceling either child during
this atomic preparation cancels the complete batch; once preparation finishes,
the children are independent queue jobs.

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

## Structured collections

Collection rows display stable identity, useful summary and owner actions.
Add, remove, reorder, duplicate and State changes preserve ids and explicit
references according to the collection contract.

For conversation messages, direction controls Actor requirements visibly.
Incoming requires an Actor, outgoing derives the Shot owner at payload
resolution, and system may optionally reference an Actor.

## Animation UX

The selected Screen shows a local authoring timeline while Preview retains the
absolute Shot playhead internally. Keyframe selection and drag use one standard
interaction. Owner, target and field identity remain visible enough to avoid
position-based editing.

Play and Restore apply to the currently visible authoring context. Cancelling a
drag or playback returns to the current authored frame without writing
temporary values.

## Input behavior

Text and numeric fields follow standard desktop selection for mouse, Wacom Pen,
touch and keyboard. Double click selects the full numeric value. Shared action
buttons and icons are reused throughout; editors do not invent alternative
chrome for an existing operation.
