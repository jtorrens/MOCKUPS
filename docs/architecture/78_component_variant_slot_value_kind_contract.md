# Component Variant Slot ValueKind Contract

Status: normative.

This contract closes the Text Box isolated-Preview failure discovered during
the final UI review. It governs any dictionary Runtime Input whose value is one
complete embedded Component Variant boundary rather than a reference alone.

## 1. Two distinct values

`ComponentVariant` is a full Component Variant reference string:

```text
componentClassId::variant::variantId
```

`ComponentVariantSlot` is one exact object:

```json
{
  "variantReference": "componentClassId::variant::variantId",
  "overrides": {}
}
```

They are intentionally different ValueKinds. A field that owns local Overrides
must use `ComponentVariantSlot`; it must not declare `ComponentVariant` and
expect a later resolver to manufacture the object.

## 2. Current document

A `ComponentVariantSlot` contains exactly:

- one non-empty full `variantReference`;
- one explicit object `overrides`, including when empty.

Missing, unknown or wrong-root values fail. Short Variant ids, Component Class
ids, strings used in place of the object and `null` Overrides are invalid
current data. Readers do not infer a Component, select the first Variant or
restore a missing Overrides object.

## 3. Dictionary ownership

The shared `ComponentVariantSlot` dictionary control owns:

- explicit Component and Variant selection;
- navigation to the selected Variant;
- opening and committing local Overrides;
- clearing local Overrides when the selected Component or Variant changes.

Runtime/Test Values, ordinary editors and future users of this ValueKind use
the same control and commit route. The Preview shell does not add a local
selector or reduced preview-only value.

## 4. Runtime and Preview boundary

The Runtime Input definition uses the exact pair:

```text
kind: componentVariantSlot
valueKind: ComponentVariantSlot
```

Its `defaultValue` is the serialized exact object, not the reference string.
Default materialization therefore produces the complete slot before resolver
dispatch. Current Test Values and persisted instance values use the same
object.

The component resolver remains strict and receives a complete boundary. It
does not accept the retired string shape or supply missing Overrides.

## 5. References and Usage

Usage discovers the exact `variantReference` as a typed Component Variant edge.
It also follows owner-declared references inside the explicit local Overrides.
Navigation uses the stored full reference; labels, types, suffixes and
positions do not determine the target.

## 6. Text Box migration

The parity database was migrated once so `leftIconRowSlot` and
`rightIconRowSlot` in the Text Box Design Preview contract use
`ComponentVariantSlot` and complete serialized defaults.

Text Input Bar and Bubble already stored complete child-slot objects and remain
unchanged. No startup migration, compatibility alias or dual string/object
reader remains.

## 7. Enforcement

The current database validator, architecture checker and desktop tests require:

- exhaustive dictionary registration for the ValueKind;
- exact Runtime Input kind/ValueKind pairing;
- strict slot object validation;
- typed Usage discovery;
- complete Left and Right Text Box slots after Test Value materialization;
- successful isolated Text Box rendering through the ordinary web Preview.

Validation and application startup must leave the committed database
byte-for-byte unchanged.
