# Production

Status: normative.

## Production workflow

Production consumes definitions and resources to build an ordered audiovisual
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
- one **Production Data** card containing the optional Shot Manager
  association plus Actors, Devices, Production Fonts and Render Presets.

Render Presets live in Production Data because their selection belongs to a
Production, even though a normal workflow can carry common presets between
Projects. They do not imply that Render Mode or export is complete.

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

Shot creation requires an Actor selection. The Shot editor never offers an
empty owner. The Actor may be changed later to another Actor in the same
Project.

Duplicating an Episode or Shot preserves every current persisted column and
creates new stable ids.

## Optional Shot Manager governance

A Project may associate with one exact VFX Shot Manager Production and Season.
The integration is optional: without an association, Episodes and Shots keep
the independent workflow above.

With an association:

- Shot Manager owns the Production, Season and Episode identities, Episode
  names and codes, technical Shot naming rules and configured folder layout;
- MOCKUPS synchronizes Episodes by stable external identity after the initial
  exact-code adoption;
- MOCKUPS remains the sole owner of every Shot row, Actor, Screen, creative
  payload and animation;
- creating or duplicating a Shot requires an explicit positive Shot number and
  Actor;
- Shot Manager returns a read-only, non-reserving plan; MOCKUPS validates and
  creates its directories, then atomically persists the local Shot and the
  exact portable layout snapshot;
- no external Shot identity exists or is manufactured.

The persisted snapshot contains only portable relative directories and stable
structure-entry identities. Workstation roots, resolved absolute paths,
discovery data and bearer credentials remain local and transient. Repair
resolves the current workstation root through Shot Manager and recreates only
directories missing from the stored snapshot; a later Shot Manager template
change does not reinterpret an existing Shot.

Episode rename, creation, duplication and deletion are unavailable while the
association governs that hierarchy. Synchronization may remove a governed
Episode only while it contains no local Shots. Disconnecting retains the local
Episodes, Shots and folder snapshots and removes only association ownership.
Deleting a governed Shot removes its local database content but deliberately
retains its production folders. No ordinary lifecycle action deletes external
folders.

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

## Playback

Play resolves the selected Production context and presents complete frames in
the Preview panel. Replaying unchanged state reuses the prepared result.
Restore returns temporary playback state to the current authored frame.

Escape cancels preparation as well as active playback. Cancellation does not
mutate the Screen payload or animation document.
