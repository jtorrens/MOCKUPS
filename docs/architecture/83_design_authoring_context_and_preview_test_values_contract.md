# Design Authoring Context and Preview Test Values Contract

Status: normative.

This contract governs the visible Component/Module Variant context and the
placement of Design-only Test Values in the desktop editor.

## 1. One explicit Design authoring context

The existing editor header is the single persistent context surface above the
scrolling editor content. When a Component or Module Variant is active, it
shows:

- the exact `Component/Module › Variant` breadcrumb;
- one explicit Variant selector;
- the active Variant's `Used`, `Protected` and `Locked` states when applicable;
- the count of explicit local Overrides when applicable.

The Variant selector is built only from the parent definition's explicit
Variant child nodes. Its option values are those nodes' exact stable ids, and
selection navigates to that exact node. It must not choose or reconstruct a
Variant from its name, type, order, hierarchy depth or visual position.

Embedded editing keeps its breadcrumb-owned Component and Variant identity.
It must not invent a tree Variant selector for an embedded reference.

The context strip beneath a root Variant breadcrumb does not repeat the parent
Component or Module identity. It contains only the aligned Variant selector,
active states and explicit local Override count.

The editor commits through the existing immediate generic write paths, so the
header does not display a permanent `Saved` badge or imply an unsaved document
mode that does not exist.

Creating a Variant remains an explicit clone of the active complete Variant.
The visible action is `New Variant…`; it is not presented as saving the current
Variant.

## 2. Design Test Values belong to Preview

For Component and Module authoring, Test Values are temporary inputs used to
inspect the isolated Preview. The upper Preview utility surface therefore uses
three horizontal tabs, in this order:

1. `Test Values`;
2. `Preview Setup`;
3. `Preview Controls`.

Only the selected tab's content is shown. `Test Values` is absent when the
selected definition exposes no Test Values or Preview actions. The other two
tabs remain available.

That surface retains the existing:

- scalar and structured Test Value controls;
- declared Preview actions;
- Reset operation;
- explicit `Save as defaults…` operation and confirmation;
- playback busy-state coordination.

Moving the surface does not change any Test Value, default, forwarding,
collection, action or payload behavior.

The central Design editor exposes the Runtime Contract separately. It describes
the inputs and collections that the authored Component or Module makes
available; it does not mix that contract with temporary Preview samples.

## 3. Production remains distinct

A Module Instance is Production data. Its Runtime Values are persisted instance
payload and remain in the central editor together with its Runtime API. They
must not move into the Design Test Values surface or be described as temporary
Preview data.

The Preview Test Values tab is never shown for a Production Module Instance.

## 4. Session and shell ownership

The selected Preview utility tab is session-only and keyed by the exact editor
layout `recordClassId`; it is not written to `data/window-state.json`. A Design
definition with Test Values opens that tab by default. A definition without
Test Values and every Production context opens `Preview Setup` by default.

`MainWindow` owns only the generic Preview host and delegates construction to
the shared collection-card factory. Runtime input controls, documents, actions
and persistence remain in their existing owners.

## 5. Preserved boundaries

- stable ids and complete Component/Module Variant references do not change;
- forwarding and local Overrides remain explicit;
- the Variant default rule at a new boundary does not change;
- no database, schema, seed, migration or persisted document changes;
- no resolver, payload, bridge, renderer or Preview resolution changes;
- editor card and scroll restoration retain their existing exact-class,
  session-only ownership.

## 6. Enforcement

Architecture checks require:

- the shared context strip's exact Variant selector and active status metadata;
- the exact root `Component/Module › Variant` breadcrumb without a repeated
  parent identity in the context strip;
- absence of the retired permanent `Saved` state;
- the `New Variant…` clone presentation;
- the ordered horizontal Preview utility tabs and dedicated Test Values host;
- separate Design Runtime Contract and Preview Test Values surfaces;
- exact-class session ownership for selected Preview utility tab;
- Production Runtime Values to remain in the central Runtime Inputs surface;
- `MainWindow` to host and delegate without constructing Runtime fields.

Desktop tests verify the context metadata, exact selector ids, active statuses
and session-only expansion key. Manual review covers Component and Module
Variant switching, embedded context, temporary Test Values, Production Runtime
Values and compact panel widths.
