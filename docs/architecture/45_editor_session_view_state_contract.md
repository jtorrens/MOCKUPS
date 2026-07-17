# Editor Session View State Contract

Status: normative.

This document governs editor card, internal navigation and scroll continuity in
the Avalonia desktop shell. It extends the shared organization rules without
changing project data, editor layouts, Preview payloads or persisted window
preferences.

## 1. Ownership and lifetime

Editor view state has one in-memory route:

```text
exact editor layout recordClassId
→ shared editor session state services
→ generic card/internal-navigation restore
→ clamped editor scroll restore
```

The state exists only for the lifetime of the current application process. It
must not be written to SQLite, `data/window-state.json`, Preview history,
Variant history or any other persisted document. A new process starts with all
top-level editor cards closed, initial internal navigation and scroll zero.

`MainWindow` may coordinate capture and restore around a generic editor swap.
It must not own per-editor state keys, editor-specific tabs or restoration
rules.

## 2. Exact editor identity

The session key is the exact `recordClassId` of the layout that builds the
editor. It is not the selected row/node id. A Component Variant uses its parent
Component Class layout id; a Module Variant uses its parent Module layout id.

Consequences:

- Theme A to Theme B keeps the Theme editor at the same working point;
- returning from Actor to Theme restores the last Theme working point;
- two different editor layout classes do not overwrite each other;
- Component and Module Variant editors never collapse into the generic
  `component.preset` or `module.variant` node classes.

Keys must never be inferred from a label, tree depth, kind, order, selected
position or control type.

## 3. Restored state

The shared services retain:

- the expanded top-level card by stable card id;
- the selected internal navigation section or tab by declared stable id;
- the session-only internal navigation width where applicable;
- the editor scroll offset.

Top-level cards expose an explicit stable session id. Restore matches ids, not
array positions. Missing cards remain ignored and newly introduced cards remain
closed. Duplicate or blank top-level card session ids fail explicitly.

After a layout swap, scroll restoration is clamped to the current extent and
viewport. A shorter destination editor therefore lands at its nearest valid
offset instead of retaining an impossible position.

## 4. Embedded navigation and reloads

Opening an embedded editor may retain an exact in-memory parent snapshot for
the breadcrumb return. Embedded cards must not overwrite the ordinary owner
layout state. Generic field reloads may capture and restore the current
in-memory view, but cannot serialize it or derive a key from the selected row.

Structured collection item expansion remains bound to its stable collection
owner/item ids. It must not be copied between records merely because two
records share a layout class.

## 5. Validation

Automated enforcement verifies:

- two nodes with the same layout `recordClassId` share one session state;
- different layout classes retain independent states and round-trip correctly;
- Component/Module Variants resolve to their parent layout identity;
- expanded cards are captured and restored by explicit stable card id;
- scroll offsets clamp to the current extent and viewport;
- relevant internal tabs use layout-class session keys; the retired
  Simplified/Complete mode creates no session key;
- persisted session-history models contain no editor view snapshot;
- a fresh session store contains no editor view state.

## 6. Forbidden shortcuts

- keying editor state by node id, row id, label, kind or list position;
- restoring expanded cards by array index;
- persisting card, tab, internal navigation or scroll state;
- allowing Preview/Variant history to override the shared class-level working
  point;
- reopening a default editor card in a fresh process;
- adding editor-specific restore logic to `MainWindow`;
- sharing structured collection item expansion across unrelated stable owners.
