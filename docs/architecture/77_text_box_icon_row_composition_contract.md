# Text Box Icon Row Composition Contract

Status: normative.

This contract governs Icon Row ownership inside Text Box and the boundary used
by Text Input Bar and Bubble.

## 1. Ownership

Icon Row owns its complete reusable Variant configuration:

- ordered `items`;
- row `gap`;
- row `orientation`;
- shared/per-Button size policy and size tokens.

Each item keeps one stable id, one full Button Variant reference and explicit
local `buttonOverrides`. Icon Row resolves every item through that concrete
Button Variant.

Text Box owns:

- one complete `leftIconRowSlot`;
- one complete `rightIconRowSlot`;
- the spacing-token `iconGap` between its Icon Rows and text;
- its placeholder and maximum line count.

Each Icon Row slot contains exactly `variantReference` and `overrides`.
Changing the selected Component or Variant clears the previous local
Overrides. Text Box never owns a second copy of the child row's Buttons, gap,
orientation or size settings.

## 2. Variant and Runtime division

The following values are Text Box Variant configuration:

```text
textBox.placeholder
textBox.maxLines
textBox.iconGap
textBox.leftIconRowSlot
textBox.rightIconRowSlot
```

The following values are Icon Row Variant configuration:

```text
iconRow.items
iconRow.gap
iconRow.orientation
iconRow.sizeSource
iconRow.iconSizeToken
iconRow.textSizeToken
```

Text Box Runtime/Test Values contain the actual text plus the isolated
inspection size or maximum width. Text animation values may also enter from a
parent Runtime boundary. Runtime does not select either Icon Row and does not
duplicate any child Variant value.

## 3. Authoring

The Text Box editor presents Left Icon Row and Right Icon Row through the
ordinary embedded Component surface:

- explicit Component/Variant selection;
- navigation to the selected Icon Row Variant;
- local `Overrides…`.

No Buttons, row Gap or Orientation fields appear beneath those selectors.
Those values are edited in the selected Icon Row Variant or in its explicit
local Overrides.

The Icon Row editor owns the shared `IconSlots` dictionary control for Buttons,
followed by its own layout and size fields. The control preserves stable item
ids, full Button Variant references, explicit Button Overrides, reorder,
duplication and deletion.

## 4. Parent composition

Text Input Bar selects one Text Box Variant and may customize it only through
that slot's explicit local Overrides. It keeps one Component Input Bindings
document for the genuine Runtime text value and its explicit forwarding
definition; it does not persist Text Box Variant fields in that document.

Bubble selects one Text Box Variant and customizes placeholder, maximum lines,
Icon Rows and spacing through the Text Box slot's local Overrides. Bubble does
not persist a parallel `textBoxInputs` Variant document.

Parents pass actual text, calculated size and animation samples to the Text Box
Runtime boundary. They do not rebuild Icon Row config in their resolvers.

## 5. Resolution

Text Box resolves each side in this order:

1. read the exact full Icon Row Variant reference from its Variant config;
2. apply only that slot's explicit local Overrides;
3. ask the Icon Row resolver to resolve its own complete config;
4. receive standard resolved atoms.

The boundary remains:

```text
Text Box resolver
→ Icon Row resolver
→ Button resolver
→ standard resolved atoms
→ generic bridge
→ generic renderer
```

Text Box rejects Variant-owned row values if they appear at its Runtime
boundary. It never accepts flat icon lists, short Variant ids, missing
Overrides, inferred Buttons or positional identity.

## 6. Persistence and migration

The parity database was migrated once so:

- every Text Box config and complete Variant contains both Icon Row slots and
  `iconGap`;
- every Icon Row config and complete Variant contains its exact `items` array;
- Text Input Bar's attachment Button lives in the right Icon Row local
  Overrides;
- Text Input Bar retains only Runtime text forwarding under `textBoxInputs`;
- Bubble's former duplicated `textBoxInputs` document was folded into its Text
  Box local Overrides and removed;
- Text Box and Icon Row Design Preview documents no longer expose
  Variant-owned values as Runtime controls;
- the editor layouts show Icon Row selectors in Text Box and Buttons/layout in
  Icon Row.

Normal startup contains no migration, fallback, normalization or dual reader.

## 7. Enforcement

Current database validation, Preview tests, desktop tests and architecture
checks require the exact ownership above. The phase is complete only when:

- isolated Text Box, Icon Row, Text Input Bar and Bubble Previews render;
- Text Box shows only standard Variant/navigation/Overrides controls for each
  Icon Row;
- Icon Row owns Buttons, Gap, Orientation and sizes;
- the attachment Button remains visible in Text Input Bar;
- Usage discovers the exact Variant and nested Override references;
- opening and validating the committed database is byte-for-byte read-only.
