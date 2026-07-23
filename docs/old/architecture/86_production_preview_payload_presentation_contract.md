# Production Preview Payload Presentation Contract

Status: normative.

This contract governs where persisted Production Screen payload is presented.
It changes desktop layout and session selection only. It does not change the
payload document, its owner, persistence, forwarding, animation or Preview
resolution.

## 1. Screen Payload is co-located with its Preview

When one exact Production Screen is selected, the upper Preview utility
surface uses three horizontal tabs in this order:

1. `Screen Payload`;
2. `Preview Setup`;
3. `Preview Controls`.

`Screen Payload` directly contains the current shared Runtime Input editor for
the persisted scalar values, structured collections, slots and their current
animation authoring actions. It must not add a second internal tab bar.

The surface is not duplicated in the central editor. Episode and Shot
selection expose no fabricated payload tab and open `Preview Setup`.

Design retains its distinct first tab, `Test Values`, under contract 83.

## 2. Placement does not transfer ownership

The first Preview tab is an authoring host, not a Preview-owned data store. The
exact ownership remains:

```text
Screen / Module Instance
→ persisted instance payload and animation documents
→ existing Runtime Input document stores and generic commit paths
→ payload preparation and complete resolution
→ Preview
```

Moving the control must not:

- turn persisted values into temporary Test Values;
- add a second Screen payload, transient mirror or save operation;
- route writes through `EditorPreviewController`;
- change Runtime Input keys, collections, slots or forwarding;
- move animation keyframes away from their stable owner;
- let Preview Setup, bridge or renderer interpret instance documents.

Every edit continues to call the same current document owner used by the
former central card. Preview refresh remains a consequence of that existing
commit, not the owner of it.

## 3. Runtime API is retained but hidden

`Runtime API` is a read-only inspection of the selected Screen's declared
Module Runtime contract. Its implementation and exact contract data remain
available, but the current Design and Production UI expose no Runtime API tab
or card. Hiding it must not delete the capability, weaken Runtime validation or
replace the contract with a Production Test Values catalog.

Its input and collection identities come from the existing exact contract.
The UI must not infer them from names, types, order or position.

## 4. Scrolling and session state

The existing horizontal splitter continues to resize the whole utility surface
against the resolved Preview. Within `Screen Payload`:

- the persisted-owner explanation remains fixed;
- values and structured collections have their own vertical scroll;
- that scroll does not move the resolved Preview or the central editor.

The selected top-level utility tab remains session-only and is keyed by the
exact editor layout `recordClassId`. A selected Screen defaults to
`Screen Payload`. No tab or splitter state is persisted in the database,
payload, window state, Variant history or Preview history.

## 5. Shell and architectural boundaries

`MainWindow` owns only the generic Preview authoring host, tab selection and
layout wiring. The shared collection-card factory chooses the exact authoring
surface and delegates its construction to `RuntimeInputsCollectionEditor`.

The central editor continues to own identity, Variant context and its other
metadata-driven or collection cards. Removing its Runtime Inputs card must not
remove Usage or any other Screen editor surface.

No resolver, payload factory, bridge, renderer, repository, schema, seed,
asset or committed database change is required.

## 6. Enforcement and review

Automated checks must verify:

- the ordered first-tab label can be `Test Values` or `Screen Payload` while
  Setup and Controls remain shared;
- the shared factory supplies Design and Production authoring surfaces;
- only a Module Instance can create the persisted Production payload surface;
- Module Instances no longer add a duplicate central Runtime Inputs card;
- persisted values have one dedicated scroll and no nested tab bar;
- Runtime API code remains present but has no visible Design or Production
  surface;
- the shell constructs no Runtime Input fields or documents;
- architecture, tests and desktop build pass;
- committed database bytes remain unchanged by this presentation phase.

Manual review covers a selected Screen, Shot and Episode; persisted scalar and
collection edits; absence of visible Runtime API surfaces; the horizontal
splitter; compact panel widths; Preview refresh after an edit; and returning to
the same top-level tab during the session.
