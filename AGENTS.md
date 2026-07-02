# MOCKUPS agent working rules

Before changing the Avalonia/Suki desktop editor spike, read and follow:

- `docs/architecture/editor_shell_non_negotiables.md`

## Hard rule: `MainWindow` is shell-only

`spikes/desktop-editor-shell/MainWindow.axaml.cs` must not contain editor-specific implementation.

It may contain only shell/orchestration responsibilities:

- window initialization;
- three-panel composition;
- selected tree node state;
- navigation tree refresh/selection wiring;
- editor card composition from generic layout metadata;
- preview panel wiring;
- generic modal hosting/delegation;
- persisted window/panel visual state.

It must not contain:

- editor-specific field construction;
- editor-specific collection rows;
- table-specific business rules;
- domain-specific pickers or dialogs;
- SVG/icon/media/font/palette logic specific to one editor;
- one-off layout fixes for a specific editor.

If an editor needs special behavior, put it in that editor's own class. If the behavior can be reused, extract it to a shared editor-shell class. `MainWindow` should only instantiate or delegate.

## Hard rule: editable fields go through the dictionary

Every editable scalar field must be defined by `FieldDefinition` and rendered through the dictionary/control path.

The expected route is:

```text
editor layout metadata
→ FieldDefinition
→ ValueKind
→ DictionaryFieldControl / registered dictionary control
→ generic commit path
→ repository/database
```

Do not create raw `TextBox`, `ComboBox`, `CheckBox`, numeric inputs, color pickers, font pickers, or icon pickers inside an editor for a value that should be a dictionary field.

If a needed control does not exist, add or extend the dictionary value kind/control first.

Collection editors are allowed for structured lists, but simple fields inside those collections must still use dictionary definitions and dictionary controls.

## When in doubt

Stop and extract. Do not add a local exception to make one editor work.
