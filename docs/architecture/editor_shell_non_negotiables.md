# Editor shell non-negotiables

This document defines the architectural rules for the next editor shell. It is meant to be read before every implementation phase of the desktop editor spike.

The goal is not to describe the current code perfectly. The goal is to protect the model we want.

## 1. Editor and runtime are separate systems

The editor edits structured data. It does not own the final visual rendering.

The web runtime remains the source of truth for:

- preview;
- frame rendering;
- screen/module visual composition;
- animation playback;
- future runtime modules.

The desktop editor may embed, control, or feed the web runtime, but it must not duplicate it.

```text
Desktop editor shell
  tree
  property editors
  dictionary controls
  component override editors
  commands
  validation

        ↓ resolved data / frame model

Web runtime
  screens
  modules
  components
  preview
  render
```

## 2. FieldDefinition owns what the value is

Every editable field must have a field definition.

A field definition declares:

- stable field id;
- logical label;
- value kind;
- whether it is inheritable;
- default or inherited source;
- validation rules;
- editor hints that are truly semantic, not visual hacks.

If a value is editable, it should not appear in the UI without a field definition.

Internal/calculated fields may exist, but then they should be explicitly marked as internal/calculated and not silently edited as ordinary fields.

## 2A. Animatable state belongs to field definitions

Fields that can participate in animation must declare it in their field definition.

Animatable support is not an editor-specific decoration. It is metadata of the field.

The field definition should declare:

- whether the field is animatable;
- which interpolation modes are valid;
- whether the animated value is frame-relative, screen-relative, or module-relative;
- whether the value can be keyframed directly or only through a higher-level routine.

Examples:

- message text can be animated with text interpolation/write-on semantics;
- delivery status can be animated as hold-only enum changes;
- numeric offsets can support linear/ease interpolation;
- palette/theme token colors should usually be hold-only unless a future color interpolation system is explicitly designed.

The animation editor may show keyframe controls next to animatable fields, but it must derive that affordance from the field definition.

## 3. ValueKind owns how a value is edited

The field does not decide which control to paint manually.

The value kind resolves to an editor control through the value/control registry.

Examples:

- `string.singleLine`
- `string.multiline`
- `number.integer`
- `number.decimal`
- `boolean`
- `enum`
- `directoryPath`
- `filePath`
- `pair.xy`
- `pair.lightDarkColor`
- `paletteColorToken`
- `themeColorToken`
- `fontFamily`
- `fontWeight`
- `fontStyle`
- `iconToken`
- `iconTokenList`
- `surfaceStyle`
- `componentOverride`

Adding a new kind means adding its validation and its control in the dictionary layer, not inside a random editor.

## 4. Controls own their own visual invariants

If a control comes from the dictionary, its visual invariants are owned by that control.

That includes:

- control border;
- control radius;
- internal layout;
- restore button placement;
- multi-control layout;
- debug styling during migration;
- focus behavior;
- multiline behavior;
- picker/modal trigger placement.

An editor may provide layout slots and section structure. It may not restyle the internals of a dictionary control.

If a control looks wrong, fix the control class, not the editor that happens to use it.

## 5. No parallel manual control route

There must not be two ways to paint the same concept:

- dictionary route;
- legacy/manual editor route.

During migration, a field may temporarily be unavailable rather than being recreated by hand.

Manual controls are allowed only for genuinely custom editor chrome, not for value editing.

Examples of allowed custom chrome:

- section headers;
- tree rows;
- toolbar buttons;
- preview navigation shell;
- modal frame.

Examples of disallowed manual value controls:

- ad hoc text input for a dictionary string;
- ad hoc dropdown for an enum;
- ad hoc color picker outside the color control;
- ad hoc X/Y layout outside the pair control.

## 6. Visual style is token-based

The editor shell may have its own UI tokens, but they must be centralized.

Controls should not hardcode colors, borders, shadows, or radii except as temporary debug markers during a clearly named migration phase.

The runtime visuals must resolve through:

- palette;
- theme tokens;
- component defaults;
- component overrides;
- module/app/screen inheritance where applicable.

No primitive color literals should leak beyond palette/theme definition layers.

## 7. Inheritance is recursive and uniform

An inheritable field has either:

- an explicit value;
- an inherited value.

Resolution walks the parent chain until it finds a concrete value.

This rule should be generic. It should not be reimplemented per editor, per module, or per component.

Resource systems are related but not ordinary parents:

- palette;
- fonts;
- icon themes;
- assets.

They resolve resources, but they are not normal inheritance parents.

## 8. Components are reusable classes, not copied property bags

Component classes define reusable visual/behavior defaults.

Examples:

- avatar;
- text input bar;
- keyboard;
- button icon;
- label;
- audio;
- video;
- surface style.

Modules use component classes by reference and may expose controlled overrides.

If many modules need the same cluster of fields, that cluster should become:

- a component class;
- a value kind such as `surfaceStyle`;
- or a `componentOverride` control.

It should not be copied into each module editor.

## 9. Component overrides are field-level and explicit

A component override editor receives:

- the component/class being overridden;
- the allowed fields;
- each field's dictionary definition;
- inherited/default values from the component.

It returns only changed fields.

The resolved model combines:

```text
component defaults
  + module/app overrides
  + instance overrides where allowed
```

The UI must show clearly whether a field is default/inherited or overridden.

## 10. Editors organize; controls edit

An editor is responsible for:

## 18. Deferred implementation notes

These are agreed improvements to revisit after the current migration pass. They should not be solved with editor-specific shortcuts.

- Extract the current `DictionaryFieldControl` switch into a dedicated dictionary control registry/factory. `DictionaryFieldControl` should become mostly a shell/row host; each `ValueKind` should resolve to a reusable control class.
- Promote numeric controls beyond plain text boxes. `number.integer` and `number.decimal` should have numeric validation, formatting, and step behavior owned by the dictionary layer.
- Add compound value kinds for paired values instead of solving pairs visually per editor:
  - `pair.xy`;
  - `pair.widthHeight`;
  - `pair.lightDarkColor`;
  - future logical pairs/triples where the value is conceptually edited as a unit.
- Layout JSON should be able to reference either individual fields or compound field groups. If a pair is conceptually one value, the JSON should declare it as one dictionary field/control, not as two unrelated controls forced onto one row by an editor.
- Centralize editor-shell UI tokens for common chrome still hardcoded in the spike:
  - selected row background;
  - amber/changed marker;
  - swatch border;
  - preview demo colors.
- Keep navigation rows, dialogs, and preview shell as common editor chrome. They may be custom, but must remain shared infrastructure, not per-editor styling.
- The palette “used” marker is already modeled in navigation. When theme/component tables land, implement reference scanning there instead of inventing table-specific markers.

- choosing the record/entity;
- grouping fields into cards/subcards;
- ordering groups;
- deciding what is shown or hidden;
- calling commands.

A dictionary control is responsible for:

- editing the value;
- validation feedback;
- restore affordance;
- picker/modal UI;
- local layout of compound values.

If these responsibilities blur, stop and refactor before continuing.

## 11. Cards and groups are structural, not data logic

Cards, subcards, accordions, and tabs are presentation structure.

They may be reusable shell controls, but they must not contain field-specific logic.

General ordering rule unless explicitly overridden:

- groups/cards sorted alphabetically;
- later, an explicit order field may override alphabetical sorting.

Cards supporting animation/overrides/status may show indicators, but those indicators should be derived from data, not manually toggled.

## 12. The preview consumes resolved/frame models

The preview should not know about editor forms.

The editor sends or persists data. A resolver builds:

- resolved model;
- frame model for a specific frame.

Preview/render consumes that output.

Separate clearly:

- editable data;
- resolved data;
- frame-specific data.

## 13. No compatibility fallbacks unless explicitly approved

We are not supporting arbitrary old databases during this spike.

If a field is missing because the current schema needs migration, migrate it.

Do not add silent fallback paths that hide schema problems.

Any fallback must be:

- explicit;
- temporary;
- documented;
- approved before implementation.

## 14. Destructive cleanup requires a commit first

Before any destructive cleanup or migration:

1. commit the current known-good state;
2. run the relevant validation;
3. perform the destructive step;
4. validate again.

This applies especially to:

- schema cleanup;
- deleting legacy fields;
- removing fallback code;
- renaming tokens;
- consolidating palette/theme/font/icon records.

## 15. Stop conditions

Stop and ask before continuing if:

- a dictionary field seems to need editor-specific styling;
- a value needs a new kind but the kind is unclear;
- a component override would require manual per-field UI;
- runtime rendering logic is about to be duplicated in the editor;
- a compatibility fallback feels tempting;
- a new class/control is being created for something that already exists conceptually.

The default answer should be: create or improve the shared class, not patch the local case.

## 16. Spike success criteria

The desktop shell spike is only useful if it proves:

- three-panel layout is simple and robust;
- dark/light palette is centralized;
- property editors can be generated from field definitions;
- dictionary controls can own their visuals;
- component override modal is simpler than the React/CSS version;
- preview can remain web-based;
- adding a new field kind does not require editing every editor.

If the spike fails these criteria, we should not migrate further.
