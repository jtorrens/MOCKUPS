# MOCKUPS agent working rules

Before changing the Avalonia/Suki desktop editor spike, read and follow:

- `docs/architecture/editor_shell_non_negotiables.md`
- `docs/architecture/editor_modernization_roadmap.md`
- `docs/architecture/24_desktop_preview_component_architecture.md`

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

## Hard rule: check common before adding helpers

Before creating any routine that could be generic, check `spikes/desktop-editor-shell/Common` and the base-routines audit for an existing equivalent.

If an analogous helper already exists, reuse or extend it there. If the new behavior is reusable by more than one editor, resolver, bridge, renderer, importer, or repository, put it in common/shared code first instead of adding a local private helper.

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

## Hard rule: no component-specific knowledge across preview boundaries

Component-specific decisions must stay inside that component's resolver/contract.

The bridge may only translate standard resolved atoms into final preview values:

- theme tokens, palette colors, alpha and neutral tint resolution;
- device/design units to final pixels;
- generic placement, boxes, text, images, SVGs, surfaces and shadows;
- generic validation/error reporting for unresolved values.

The bridge must not contain branches or layout rules for a specific component class such as label, avatar, button icon, audio, video, bubble, status bar, or navigation bar. If a component needs custom composition, create or extend that component resolver so it emits the standard atoms the bridge already understands.

There must not be a central preview bridge that grows component-specific functions or rules. Component classes and system bars use their own resolver/renderable modules and are selected only through an explicit registry. Registries may name components only to route to their owning module; they must not contain component layout, style, defaults, token resolution, or renderable construction logic. As components are migrated, remove central bridge code by moving component composition into component resolver/renderable modules and passing only standard atoms through generic helpers.

Each migrated component must keep this shape:

```text
component contract/resolver
→ component renderable module
→ common preview helpers
→ generic web renderer
```

Common preview helpers must not import concrete component resolvers/renderables or contain concrete component names. Embedded component imports are allowed only when the parent component explicitly owns that child slot.

Run `npm run check:architecture` before closing any preview/component migration phase. The check must fail if component-specific names or imports leak into central preview files, common helpers, or undeclared component dependencies.

The web renderer is even stricter: it paints the final resolved nodes. It must not know inheritance, class config, component defaults, theme token names, palette tokens, database records, or per-component business/layout rules. If the renderer needs a new visual primitive, add a generic primitive and feed it fully resolved style/data.

Animation is also frame data. Resolvers own the component state for the requested frame, and the bridge may translate that resolved frame into final pixels. The web preview/render layer must not run its own timers, CSS animations, countdowns, or component-specific interpolation. For web preview, an animated component is just a succession of resolved frames.

Component inputs are runtime component inputs, not preview-only controls. The preview panel may provide sample values for isolated inspection, but screens/modules must later supply real values through the same declared input contract. Do not add component-specific input catalogs or animation behavior to the preview shell.

If a change appears to require `if componentType == ...` behavior in the bridge or renderer, stop and move that responsibility to the component resolver or to a parameterized common helper.

## When in doubt

Stop and extract. Do not add a local exception to make one editor work.
