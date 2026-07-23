# Consistent Lifecycle Action Presentation Contract

Status: normative.

This contract governs lifecycle-action consistency between navigation and
editors. An action may be available in more than one useful context. Every
presentation of that action must expose the same capability, use the same
operation and preserve the same result.

## 1. Consistency is the rule

Navigation is not required to be the sole lifecycle surface. A complete parent
collection may repeat Add, Duplicate, Move or Delete when that makes authoring
clearer. Repetition is intentional only when:

- the same existing shared action control is used;
- both routes delegate to the same current lifecycle operation;
- stable ids, references, Usage protection and confirmation rules are
  identical;
- the resulting selection and navigation behavior is explicit;
- neither route invents a fallback or weaker version of the action.

Action availability must not be inferred from labels, hierarchy depth, sibling
position or the presence of another button.

## 2. Rename is consistent across Design and Production

Every current named entity whose identity is user-editable exposes Rename:

- through the standard compact Rename action in its Design or Production
  navigation row;
- through its dictionary-rendered identity field in the editor.

Both routes call the same exact `RenameDirectNode` operation. The operation
changes the display name only and preserves the stable id, record class,
parent, Variant identity, payload and animation ownership.

Current named entities include Project, App, Module, Component Class,
Component Variant, Module Variant, Episode, Shot, Screen, Palette Color, Icon
Theme, Render Preset, Device, Actor, Theme and Production Font.

Protected default Variants may be renamed even though they cannot be deleted.
Lock and delete protection must not silently remove Rename.

The editor identity field remains metadata-driven. Most named records use
`core.name`; Palette Color retains its explicit `palette.token` identity field.
Both routes commit through `RenameDirectNode`:

```text
editor layout identity metadata
→ core.name or palette.token FieldDefinition
→ registered dictionary control
→ RenameDirectNode
→ owning repository/current document
```

No editor may create a local raw name input or an alternate name-write path.
Changing the editor field refreshes the tree, breadcrumb/title and relevant
Preview options in the same session.

## 3. Shot and Screen actions may appear in both useful contexts

The Production tree retains:

- Add Screen on a Shot;
- Rename, Duplicate and Delete on a Screen;
- the existing Shot and Episode lifecycle actions.

The Shot Modules card also exposes Add Screen, Move, Duplicate and Delete
because it is the complete ordered Screen collection. Both Duplicate routes
open the newly created Screen. Both Delete routes use the same destructive
confirmation and Usage protection and then return to the owning Shot.

The tree remains the only Rename surface outside the selected Screen editor;
the Screen editor also exposes the same `core.name` identity field.

## 4. Other lifecycle contracts remain unchanged

App, Module and Component Class creation/deletion remains development-owned.
Their normal editor lifecycle remains Rename-only.

Variant creation, duplication, rename, lock and protected deletion continue to
follow the current complete Variant contracts. Resource and Production
collection actions retain their existing ownership and Usage rules.

The Production picker shows only its current selection and open/edit action.
Disabled Add, Duplicate and Delete placeholders remain absent. Future Project
duplication is still a separate explicit workflow with copy, regenerate and
empty choices.

## 5. Common presentation

Tree actions use the shared navigation action buttons. Collection actions use
the shared compact Add, Move, Duplicate and Delete controls. Rename uses the
existing compact Edit icon in every tree.

Equivalent actions use the same language:

- Add Screen;
- Duplicate Screen;
- Delete Screen;
- Rename.

Dialogs and accessible names describe the user-facing entity (`Screen`), not
its persistence implementation (`ModuleInstance`).

## 6. Preserved boundaries

- stable ids and complete Variant references do not change;
- no runtime forwarding, Override, payload or animation document changes;
- no Preview resolver, bridge or renderer change;
- normal startup remains read-only;
- database writes occur only after an explicit user action;
- editor identity remains a dictionary field;
- `MainWindow` remains shell-only and delegates navigation/editor refresh.

## 7. Enforcement and review

Automated checks must verify:

- every current named editable entity supports direct Rename;
- editor `core.name` delegates to the same direct Rename operation;
- current Component editor layouts expose `core.name`;
- editor Rename refreshes navigation and Preview options;
- Shot Add and Screen Rename/Duplicate/Delete remain in navigation;
- the Shot Modules card retains Add/Move/Duplicate/Delete;
- Production picker dead placeholders remain absent;
- architecture, read-only database, desktop tests and build pass.

Manual review covers Rename from Design and Production trees, Rename through
General/Identity, immediate tree and breadcrumb refresh, Screen actions in the
tree and Modules card, protected Variants and compact layouts.
