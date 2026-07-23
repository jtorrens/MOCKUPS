# Text Box Icon Row Composition Contract

Status: normative.

This contract closes the structured Icon Row decision deferred by contract 74.
It governs the two Icon Row children embedded by Text Box, their use from Text
Input Bar and Bubble, and the shared `IconSlots` dictionary control.

## 1. Ownership

Text Box owns two explicit child boundaries: Left Icon Row and Right Icon Row.
Each boundary consists of:

- one complete Icon Row Variant slot with `variantReference` and `overrides`;
- one ordered `IconSlots` item array;
- one spacing-token gap within the row;
- one explicit `horizontal` or `vertical` orientation.

Text Box also owns one spacing-token gap between its Icon Rows and text.

Icon Row owns row distribution and resolves every item through the concrete
Button Variant stored by that item. Button owns its own appearance and state
composition. Text Input Bar and Bubble may supply complete Text Box inputs, but
they do not recreate Button or Icon Row defaults.

## 2. Current Text Box Runtime Input document

The current Text Box boundary uses these exact stable keys:

```text
leftIconRowSlot
leftIconRowItems
leftIconRowGap
leftIconRowOrientation
rightIconRowSlot
rightIconRowItems
rightIconRowGap
rightIconRowOrientation
iconGap
```

The retired `leftIcons`, `rightIcons`, `leftIconRowInputs`,
`rightIconRowInputs`, `iconRowSize`, `iconRowGap` and `iconRowOrientation`
keys are not compatibility input. A current reader rejects them.

The two sides intentionally have separate gap and orientation values. Button
icon and text sizes belong to the selected Icon Row Variant when its
`sizeSource` is shared, or to each Button item when it is `perButton`. Text Box
does not own or duplicate a shared Button-size field.

An empty row is still a complete boundary. Its item array is empty, but its
Icon Row Variant slot, local Overrides, gap and orientation remain required.

## 3. Current Icon Slots item

Every item is one exact current object containing:

```text
id
buttonVariantReference
contentMode
state
iconToken
text
iconSizeToken
textSizeToken
pushTrigger
pushElapsedMs
buttonOverrides
```

The `id` is non-empty and unique inside the row. It is generated once when the
user creates the item and survives reordering.

`buttonVariantReference` uses the full
`componentClassId::variant::variantId` form. `buttonOverrides` is always an
explicit object, including when empty. The supported content modes are `icon`,
`text` and `iconText`; the supported Button states are `normal`, `active`,
`pushed` and `disabled`.

Unknown, missing or wrong-root members fail. Readers do not manufacture ids,
select a Button from a component name or type, search references by suffix, or
derive identity from item position.

## 4. Authoring behavior

The shared `IconSlots` dictionary control owns this structured authoring UI.
It provides:

- explicit Component and Variant selection for every Button item;
- navigation to the selected Button Variant;
- editing of item-local Button Overrides through the ordinary embedded editor;
- icon, text, content mode and state editing;
- stable-id-preserving reorder;
- duplication by inserting after the selected item;
- explicit deletion.

Creating the first item starts with no inferred Button. The user first chooses
the Button Component; crossing that new boundary selects that Component's
explicit stable Default Variant. The item is persisted only after this
selection exists.

Inserting after an existing item clones its complete Button Variant and local
Overrides, then assigns a new stable item id. Changing the Button Component or
Variant is explicit and clears the previous local Overrides so they cannot leak
across a different Variant boundary.

The control is a dictionary ValueKind owner. Text Box, Text Input Bar and other
editors do not create local raw scalar controls for these values.
The same control and services are used in Runtime/Test Values, including
navigation to the selected Button Variant and editing its explicit local
Overrides. Test Values do not introduce a reduced or preview-only Icon Row
document.

## 5. Parent composition

Text Input Bar persists one complete `textInput.textBoxInputs` document using
the exact Text Box Runtime Input keys. Its resolver forwards that document
unchanged, adding only the parent-calculated Text Box size.

Bubble persists one complete `bubble.textBoxInputs` document. Empty message
Icon Rows are represented by complete slots and empty item arrays, not by
missing slots or hard-coded resolver fallbacks.

No component other than Text Input Bar may retain a foreign top-level
`textInput` configuration object. Component-specific configuration remains
inside its owning root.

## 6. Resolution boundary

Text Box requires its config, Surface slot, Cursor slot and both complete Icon
Row input boundaries. For each side it:

1. resolves the exact Icon Row Variant reference;
2. applies only that slot's explicit local Overrides;
3. supplies the exact item array, gap and orientation;
4. delegates Button resolution to Icon Row.

Text Box never searches the component base-config catalog for a Button,
converts icon-token lists into Button items, assigns positional ids or skips
slot validation because a row is empty.

The established boundary remains:

```text
Text Box resolver
→ Icon Row resolver
→ Button resolver
→ standard resolved atoms
→ generic bridge
→ generic renderer
```

## 7. Persistence and migration

The parity database was migrated once to the current document:

- flat icon-token lists became explicit stable Button items;
- every stored Button reference became a full Variant reference;
- local Button Overrides became explicit;
- left and right row layout inputs became independent;
- Text Input Bar's malformed duplicate forwarding member was removed;
- foreign top-level `textInput` config was removed from non-owning components;
- Bubble received complete empty Text Box Icon Row inputs.
- affected local Variant history snapshots were migrated once so Restore cannot
  reintroduce the retired foreign config.

Normal startup, repositories, payload preparation and resolvers contain no
migration, alias, normalization or compatibility fallback for retired keys.

## 8. Enforcement

The desktop current-database validator checks the owner-specific Text Box,
Text Input Bar and Bubble documents. `IconSlotsDocumentContract` checks the
exact item envelope. Preview tests cover complete empty boundaries and retired
input rejection. Architecture enforcement requires the new resolver/editor
route, checks committed parity data and rejects the removed inference helpers.

The migration is complete only when the full automated suite passes, database
validation is byte-for-byte read-only, and the final UI review confirms:

- Left and Right row cards expose structured Button items;
- Component and Variant selection is explicit;
- Button Overrides open and return correctly;
- inserting, reordering and deleting preserve stable identity;
- Text Box, Text Input Bar and Bubble Preview remain visually correct.
