# Explicit Reference and Usage Contract

Status: normative.

This document governs persisted reference discovery, the navigation-tree
`Used` state, Usage presentation and deletion protection. It extends contracts
33, 35, 36 and 37 without changing the SQLite schema or persisted data.

## 1. Objective

Usage is a projection of declared reference edges, not a search facility:

```text
current validated rows and JSON documents
→ owner-declared reference contracts
→ exact stable target identity
→ typed source navigation and Design/Production scope
→ one Usage projection
→ tree Used state, Usage card and deletion protection
```

The same edge set must govern all three consumers. A record cannot appear
unused in the tree while deletion discovers another rule, and the Usage card
cannot navigate by reinterpreting a display string.

## 2. Reference identities

Every edge has:

- a referenced `ProjectTreeNodeKind` and stable target id;
- a source navigation node id and `ProjectTreeNodeKind`;
- an explicit source label and field/contract label;
- an explicit `Design` or `Production` scope;
- optional embedded-slot context owned by the source contract.

Relational references compare declared columns with exact ids. Component and
Module Variant references compare their complete stored references exactly:

```text
componentClassId::preset::presetId
moduleId::variant::variantId
```

Palette references use the exact token serialized by the declared palette
field contract, then resolve that token to the stable Palette row id. Token
text is not itself record identity outside those declared fields.

## 3. Relational declarations

Relational Usage declarations identify the source table/column, source
navigation kind, target kind, presentation label and scope together. Foreign
keys and current validated soft references are both exact edges.

The active declarations include:

- Episode from `shots.episode_id`;
- Shot from `module_instances.shot_id`;
- App and Module from the corresponding Module/Module Instance ids;
- Device and Theme from Actor defaults;
- Actor and Render Preset from Shot settings;
- Icon Theme and system-bar Component Variants from Theme settings.

A new reference column must add its declaration in the same change. Table or
column names, SQLite affinity, label columns and foreign-key discovery must not
be used to guess an undeclared edge.

## 4. JSON reference contracts

JSON references are discovered only through an owning contract:

- Component field descriptors provide their explicit JSON paths and
  `ValueKind`;
- embedded slot declarations provide their paths and concrete child type;
- Module-owned reference fields provide explicit paths;
- Runtime Input and Structured Collection definitions provide `jsonKey`,
  `valueKind`, `tableId` and nested collection/component ownership;
- Theme color-token and typography catalogs provide their paths;
- Actor and App wallpaper/color contracts provide their paths;
- Module Instance metadata provides the exact Module Variant path.

Recursive traversal is allowed only when recursion is declared by the owner,
such as a Structured Collection, component-item contract, embedded Override or
complete Variant envelope. An arbitrary JSON string, object member or text
column is not a reference merely because it contains or equals another id.

Complete Component Variant references are intrinsically typed current values,
but their source owner and navigation target still come from one of the
declared composition contracts above.

## 5. Design and Production scope

Scope is data in the Usage edge, not a word inferred from a label.

- definition/configuration owners such as App, Module, Component Variant,
  Theme, Actor and isolated persisted Test Values are `Design`;
- Episode, Shot and Module Instance payload owners are `Production`.

Persisted isolated Test Values and a Module Instance payload are distinct
sources. Previewing, Play or Restore does not copy one into the other and does
not create a Usage edge that was not already persisted by the owning contract.
Runtime Input defaults are Design references owned by the definition that
declares them. A Production edge comes from the persisted Module Instance
payload itself; Usage must not relabel a definition default as if every Screen
had persisted that value.

## 6. Navigation ownership

Every edge stores the exact source node to open. A Component Variant source
navigates to that Variant, not to a parent selected from source-label text. A
Module Variant source follows the same rule. When a direct embedded slot is the
source, optional embedded context may open that slot's Override surface; nested
or non-slot references may navigate to the exact owning node without inventing
a path.

Opening an edge is one contextual shell operation. It must first activate the
edge's typed Design/Production scope, select the Production when applicable,
expand every tree ancestor, select and reveal the exact source node, build its
editor and then open declared embedded context when present. Opening an editor
without changing the visible workspace/tree is incomplete navigation.

The Usage card and deletion protection must call this same operation. A blocked
deletion presents its typed Usage edges as links; activating one closes the
modal before navigating. The modal must not flatten edges into prose that loses
their scope, node kind, stable id or embedded context.

No code may derive navigation kind, Production scope or owner type by testing
words such as `Episode`, `Shot`, `Instance`, `Variant` or `Component` in a
display label.

## 7. Service and facade boundary

`ReferenceUsageService` is the cross-domain owner. It may read the current
validated tables and declared JSON contracts through `SqliteProjectContext`.
It returns typed reference edges and does not build controls, dialogs or tree
nodes.

`SpikeDatabase` remains the compatibility facade. It delegates Usage queries,
maps typed edges to the existing UI-facing records and coordinates deletion
only after the service reports no incoming edge. Individual resource
repositories do not scan other tables and do not own Usage.

## 8. Enforcement and tests

Architecture enforcement must verify:

- the explicit Usage service and interface exist and are constructed by the
  facade;
- the old table enumeration, text-column discovery, label-column guessing,
  substring `LIKE` query and source-label classification are absent;
- tree Used state, Usage details and delete checks delegate to the same
  service;
- the service does not import `MainWindow` or construct UI controls;
- references remain exact and typed.

Disposable-database tests must cover:

- exact relational references and their source navigation/scope;
- exact JSON field/contract references;
- Component and Module Variant references;
- Design Test Values versus Production instance payloads;
- contextual navigation preserving scope, exact node and embedded context;
- substring and unrelated-text false positives;
- identical evidence for Used, Usage and deletion;
- byte-for-byte read-only startup and an unchanged committed database during
  this pure extraction.

## 9. Forbidden shortcuts

- `LIKE '%id%'`, `Contains(id)` or scanning every text column;
- enumerating tables or text columns to discover possible references;
- treating an arbitrary JSON scalar as a reference without an owner contract;
- resolving Palette identity from matching prose or a field name;
- mapping a source label back to a node kind;
- classifying Design/Production from source-label words;
- maintaining a separate fast Used heuristic that differs from deletion;
- adding Usage SQL or reference semantics to Palette, Device, Actor or another
  single-table repository;
- repairing missing reference metadata or accepting short Variant ids.
