# Component Variant Storage Vocabulary Contract

Status: normative.

This contract completes the explicit storage migration anticipated by contracts
23, 24, 31 and 35. It governs Component Variant envelopes, complete Component
Variant references and the vocabulary used by desktop, payload and Preview
boundaries. It does not change Variant behavior, ownership or lifecycle.

## 1. Canonical vocabulary

`Variant` is the only term for a complete named configuration snapshot owned by
a Component Class or Module. `Preset` is not a synonym for Variant.

The active product currently uses `Preset` only for Render Presets. The term is
reserved for future reusable recipes that are not owner-bound Variants. Future
concepts require their own approved contract; this reservation does not create
Motion, Animation or other Preset functionality.

## 2. Component Variant envelope

Every Component Class stores its required non-empty Variant array at:

```text
component_classes.metadata_json.variants
```

Each entry remains the strict complete envelope governed by contract 35:

- stable `id` unique within the Component Class;
- non-empty display `name`;
- explicit `protected` and `locked` booleans;
- complete object `config` snapshot.

The protected stable `default` id remains required. The Component Class
`config_json` parity copy remains equal to the Default Variant config. Component
and Module Variants use the same envelope contract while retaining separate
owners, persistence operations, tree kinds and resolver contracts.

## 3. Complete references

A concrete Component Variant reference has exactly this form:

```text
componentClassId::variant::variantId
```

Both ids retain their existing stable values. The composite serialization is a
complete reference, not a new entity id.

Module Variant references intentionally use the equivalent
`moduleId::variant::variantId` grammar. The delimiter alone therefore does not
identify the owner domain. Validation follows the explicit declared field,
typed contract or relational owner; it must never infer Component versus Module
ownership from an id prefix, display name, position or other spelling
convention.

Generic embedded slots, Component Stack States and Collection Stack items store
that reference in `variantReference`. Role-specific component-item fields use a
specific `*VariantReference` name, such as `buttonVariantReference`. Metadata
that declares the child-reference JSON key uses
`variantReferenceJsonKey`.

Existing stable owner field ids and role-specific paths already expressed in
Variant language remain unchanged. Theme relational columns also retain their
SQL names; their values are complete Component Variant references.

## 4. Dictionary, Runtime and payload vocabulary

The canonical dictionary and Runtime Input value is `ComponentVariant` /
`componentVariant`. It always stores a complete reference. Empty values are
allowed only when the declaring contract explicitly permits None.

Prepared Component base data exposes `variants` and `variantTypes`. Typed
desktop services, tree nodes, controls, Usage edges and Preview payload members
use `ComponentVariant` and `VariantReference` names. These names do not select,
merge or default a Variant; they identify the existing explicit value.

## 5. Explicit migration

The cutover is one temporary, self-contained migration:

1. rename the Component metadata root `presets` to `variants`;
2. rename declared Component reference keys to their Variant-reference names;
3. translate only strings that exactly match the retired complete-reference
   grammar to the new grammar;
4. migrate the canonical Runtime Input kind and declared metadata keys;
5. migrate valid session Variant selections and snapshots by exact stable
   reference;
6. discard session-only history that references a missing retired entity;
7. validate the result and remove the migration routine.

The migration must not replace words in display names, notes or arbitrary text,
and must not infer a target from a name, component type, order or position.
Normal startup never performs this work.

The historical schema-v1 cutover artifact, archived applications and exchange
handoffs remain historical evidence. They are not current readers, writers or
validation authorities and are not migrated.

## 6. Strict current contract after cutover

Active code and current data accept only:

- `metadata_json.variants` for Component Variant arrays;
- `componentClassId::variant::variantId` complete references;
- `variantReference`, declared role-specific `*VariantReference` fields and
  `variantReferenceJsonKey` metadata;
- `ComponentVariant` / `componentVariant` dictionary and Runtime vocabulary.

The following are invalid current data and forbidden active compatibility
paths:

- `metadata_json.presets`;
- `::preset::` references;
- `presetId`, `buttonPresetId` or `presetJsonKey` fields;
- `ComponentPreset` / `componentPreset` value kinds, tree kinds, controls or
  services;
- short Variant ids, aliases, dual readers or automatic conversion at startup.

Validation reports the exact invalid owner and leaves the database unchanged.

## 7. Preserved behavior and boundaries

This vocabulary migration does not change:

- Component Class or Variant stable ids;
- explicit Default selection when a new boundary is authored;
- complete-reference requirements at existing boundaries;
- Variant creation, duplication, rename, lock, Usage or deletion rules;
- local Overrides, Restore semantics or explicit Forwarding;
- Runtime Test Values versus persisted Module Instance payload;
- structured collection, Slot or State ownership;
- owner-relative keyframes, action timing or duration policy;
- resolver, bridge, renderable or generic renderer responsibilities;
- `MainWindow` shell-only ownership;
- Render Presets or their persistence and Production Data placement.

## 8. Enforcement and acceptance

Automated validation must prove:

- every current Component Class has a strict `variants` envelope and no
  `presets` member;
- every declared Component Variant reference is complete, canonical and points
  to an existing Variant in the same Project;
- current persisted documents contain no retired reference keys, delimiter or
  Runtime kind;
- active editor, payload and Preview code contain no Component Preset API;
- Usage, tree selection, embedded editing, forwarding and Preview use the same
  exact complete reference;
- Render Preset names and behavior remain unchanged;
- opening and validating the migrated database is byte-for-byte read-only.

Required manual checks cover Component Variant selection and lifecycle, an
embedded Variant and Override, a structured component collection, Runtime Test
Values, Theme system-bar Variant selection and Production Preview.

## 9. Forbidden shortcuts

- global text replacement inside persisted notes or labels;
- preserving the retired delimiter or keys as aliases;
- translating a missing reference by component type or display name;
- merging Component and Module Variant owners into one lifecycle service;
- treating Render Presets as Variants;
- compensating for stale data in an editor, resolver, bridge or renderer.
