# Shot Persistence Contract

Status: normative.

This document governs the Shot persistence slice of the staged desktop
repository extraction. It extends contracts 26, 31, 33, 35, 36, 38, 39, 40,
41 and 47 without changing Production context, Screen timing or Preview.

## 1. Persistence ownership

Shot rows follow one persistence route:

```text
shots current rows
→ ShotRepository
→ SpikeDatabase compatibility facade and aggregate coordinators
→ tree / editor / Production context / timeline caller
```

`ShotRepository` owns exact and ordered row reads, the derived Episode/Project
ownership projection, strict current JSON roots, direct field writes, stored
duration writes, node writes and explicit create/duplicate/delete persistence.
It also owns copying complete Shot rows when an Episode aggregate is
duplicated.

The repository returns `ShotRecord` persistence records. It does not create
tree nodes, dictionary controls, Preview contexts, payloads or renderables.

## 2. Current Shot row

Every current Shot preserves:

- stable `id` and explicit `episode_id`;
- the Episode-derived exact Project id;
- authored name, slug, version, notes and sibling sort order;
- optional explicit FPS override;
- current positive stored duration;
- explicit non-empty owner Actor id;
- optional explicit Render Preset id;
- required object `canvas_json` and `metadata_json` documents.

Reads reject missing ownership, blank required identity, non-positive duration
and malformed or wrong-root documents. Writes validate their replacement
before mutation and throw for a missing row or unknown field. They do not
repair documents, choose an Actor, infer a Project or supply a Render Preset.

## 3. Production context stays outside persistence

The exact `Shot → owner Actor → default Device/Theme` route remains in the
Production-context owners established by contracts 40 and 41. Before an Actor
write reaches the repository, the facade verifies that it belongs to the same
Project and, when Screens exist, resolves an explicit Theme. The repository
never selects the first Actor, Device, Theme or resource by name, type, order
or position.

The following also stay outside Shot persistence:

- Module and complete Module Variant selection when adding a Screen;
- Runtime payloads, forwarding, Overrides and structured collections;
- Screen-local keyframes, temporal owners and animation contracts;
- calculated/explicit Screen duration policy and frame evaluation;
- Shot duration aggregation from ordered Screen records;
- render-name composition from Project/Episode/Shot slugs;
- effective owner Device display and Preview context;
- resolver, renderable, bridge and generic renderer behavior.

The duration coordinator may sum typed Screen records and ask
`ShotRepository` to persist the resulting positive Shot duration. The
repository does not perform or reinterpret that calculation.

## 4. Complete lifecycle copies

Creating a Shot receives one already validated explicit Actor id and constructs
the complete current row directly. Duplicating a Shot generates a new stable
id and copies every current Shot value, including the explicit Render Preset
reference. It does not copy child Module Instances; changing that aggregate UX
requires a separate approved design.

Episode duplication retains its existing aggregate rule of copying its Shots
with new stable ids. `ProjectEpisodeRepository` owns the Episode operation but
delegates every child Shot row copy to `ShotRepository`. The copy preserves the
complete current Shot row; it does not issue competing Shot SQL or infer child
ownership from display text.

Delete remains guarded by exact cross-domain Usage before repository
delegation. Declared foreign-key cascades remain schema behavior and are not
reimplemented as best-effort cleanup.

## 5. Validation

Automated enforcement verifies:

- `IShotRepository` and `ShotRepository` are explicit;
- `SpikeDatabase` constructs and delegates through the repository;
- Shot editor, tree, Episode duplication, Screen selection and duration
  coordination retain no direct `shots` CRUD SQL;
- facade and repository reads agree for current Production identity;
- direct fields, FPS inheritance, duration and node writes round-trip;
- Shot and Episode duplication preserve all current Shot documents,
  references and stable owner values, including Render Preset;
- invalid JSON and non-positive duration fail before mutation;
- Actor/Theme, Runtime, animation and Preview concerns do not enter the
  repository;
- startup remains byte-for-byte read-only and the parity database is unchanged
  by the extraction.

Whole-database validation, exact Usage and Production Theme context retain
their declared cross-domain read-only joins. They may not mutate or repair Shot
rows.

## 6. Forbidden shortcuts

- passing the repository into `MainWindow` or an editor control;
- choosing Actor, Device, Theme, Render Preset, Module or Variant in
  persistence;
- deriving ownership from a name, class, list index or tree position;
- calculating Screen or Shot timing inside the repository;
- omitting current Shot columns from a duplicate;
- copying child Screens without an explicit aggregate-product decision;
- retaining direct `shots` writes in facade or Episode repository code;
- changing schema, seeds or parity data as an incidental extraction step.
