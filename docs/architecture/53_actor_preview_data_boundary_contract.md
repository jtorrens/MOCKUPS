# Actor Preview Data Boundary Contract

Status: normative.

This document governs the desktop Actor read boundary used by inline avatar
inspection, Runtime record references and final Preview payload preparation. It
extends contracts 24, 34, 36, 37 and 51 without changing Actor persistence or
the resolved Actor payload shape.

## 1. Objective

Actor persistence and Actor Preview interpretation have three explicit owners:

```text
validated current Actor, Project and Palette data
→ ActorPreviewDataSource
→ immutable raw Actor Preview sources
→ ActorPreviewInputFactory / ActorAvatarPreviewFactory
→ resolved Runtime Actor value / inline avatar control
```

The data source reads exact current values. The factories interpret those
values for their respective Preview surfaces. The repository stores current
Actor rows only.

## 2. Data-source ownership

`ActorPreviewDataSource` is the only `SpikeDatabase` dependency of the Actor
Preview factories and generic record-reference resolver. It may supply:

- the Actor's explicit Project, default Device and default Theme ids;
- current display name, short name and required metadata document;
- the Project media root;
- exact current avatar and mode field values through the established Actor
  field-value route;
- explicit Actor record options and Palette color options.

It returns narrow immutable sources. It must not resolve a mode, Palette token,
initials, media file, crop, wallpaper or final payload; return a database
handle; execute SQL; repair current JSON; or select any record by name, type,
position or index.

## 3. Interpretation ownership

`ActorPreviewInputFactory` owns the Runtime Actor value consumed by Component
and Module Preview:

- strict parsing and cloning of current wallpaper/mode data;
- explicit Light/Dark selection from the stored pair;
- exact Palette-token lookup with a visible failure for a missing token;
- initials, media URI, scale and offset resolution;
- the stable resolved Actor JSON shape.

The exact initials text is shared with the inline inspection through
`ActorIdentityText`; both surfaces must represent the same Actor identity with
the same two-word uppercase rule. The input factory still owns including that
resolved text in the Runtime Actor payload, while the avatar factory owns its
inline sizing and presentation.

`ActorAvatarPreviewFactory` owns only the editor's inline avatar inspection:

- overlaying unsaved dictionary draft values for immediate feedback;
- preview crop, scale, offset, initials sizing and brushes;
- safe conversion of a selected local media path back to the Project-relative
  storage path.

Neither factory may query persistence. Draft values remain editor-local and
must never enter a final Runtime payload or persisted Actor document merely by
previewing them.

## 4. Generic record references

`ComponentPreviewRecordInputResolver` routes a declared `RecordReference` with
`tableId: actors` to the Actor data source and input factory. It does not know
component classes, infer an Actor from input names or create a second Actor
payload shape. Empty isolated Test Values may still use the explicit sample
Actor; Production references resolve the persisted stable Actor id.

Nested and collection record references use the same declared input contract
and the same Actor boundary. Forwarding remains explicit and is completed
before Preview registry dispatch.

## 5. Preserved boundaries

- `ActorRepository` continues to own Actor table SQL and stored documents.
- `GetActorFieldValue` remains the single current field-to-document mapping;
  the Preview data source must not duplicate JSON paths.
- Actor default Theme and Device remain explicit stored ids.
- Palette tokens remain exact ids and are never inferred from labels or color.
- Component and Module resolvers retain component-specific composition.
- The bridge and renderer remain generic and receive resolved values only.
- `MainWindow` remains shell-only.

## 6. Enforcement and tests

Architecture enforcement must verify:

- both Actor Preview factories and the generic record resolver contain no
  `SpikeDatabase` dependency;
- `ActorPreviewDataSource` is their declared database boundary and contains no
  SQL;
- payload, nested Runtime Input and inline avatar routes compose this boundary;
- this contract is linked from `AGENTS.md` and the architecture index.

The desktop parity test must compare the data source with the current facade,
exercise the resolved Actor payload in both Theme modes and prove that all
reads leave a disposable database byte-for-byte unchanged.

## 7. Out of scope

This phase does not change Actor fields, wallpaper opacity behavior, avatar
visual design, Runtime Input contracts, the payload shape, tables, parity data
or asset files. It does not add Actor automation, Render Mode or export.

## 8. Forbidden shortcuts

- moving avatar or wallpaper interpretation into `ActorRepository`;
- reading Actor fields directly from either Preview factory;
- accepting malformed metadata as an empty object;
- falling back to a different Actor, Theme, Device or Palette token;
- identifying an Actor from its display name, input name or collection index;
- letting inline draft values cross into persisted Production payloads;
- adding Actor-specific behavior to the bridge or web renderer.
