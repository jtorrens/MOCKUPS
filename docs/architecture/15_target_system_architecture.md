# Target system architecture

Status: target model

This document describes the intended final architecture for the Mockups
application. It is not a snapshot of the current implementation. Some pieces
already exist, some are partially implemented, and some are still design goals.

The purpose of this document is to make the whole model visible in one place:
editors, value types, data ownership, inheritance, component classes, apps,
modules, screens, preview and render.

## Core principle

The application should behave like a system of black boxes with explicit
contracts.

Each layer owns one kind of responsibility and communicates with the next layer
through narrow, typed interfaces. No layer should silently know about internal
details of another layer.

```text
Editor UI
  edits fields through field definitions

Domain value system
  validates values and resolves inheritance

Data model
  stores productions, themes, apps, modules, components, instances and content

Domain resolvers
  turn stored data into resolved render props

Visual modules
  turn resolved props into renderer-agnostic renderable trees

Preview / renderer
  draw the same renderable model
```

The important rule is:

```text
Editors author data.
Resolvers interpret data.
Visual modules draw data.
Renderers output pixels.
```

No editor should contain render logic. No visual module should know how values
are stored in SQLite. No renderer should know inheritance rules.

## High-level data flow

```text
SQLite records / JSON fields
        │
        ▼
Repository
        │
        ▼
Domain resolver
        │
        ├─ ValueRegistry validates concrete values
        ├─ FieldDefinition describes system fields
        ├─ JsonFieldBinding adapts stored JSON to fields
        ├─ FieldResolver resolves inheritance
        ├─ Palette/theme/component tokens resolve to concrete runtime values
        └─ Design-space units scale to device/render pixels
        │
        ▼
Resolved props
        │
        ▼
Visual modules
        │
        ▼
Renderer-agnostic renderable tree
        │
        ├─ Debug preview
        └─ Final render / PNG / video pipeline
```

The preview and final render must consume the same resolved props and visual
module output. Differences between preview and render should be caused only by
output scale, selected frame, or explicit render options.

## Value system

The value system is a first-level domain subsystem. It is not part of Chat,
Theme, Preview, CSS or the editor.

Target location:

```text
src/domain/value-system
```

### `ValueRegistry`

`ValueRegistry` is the central dictionary of allowed value kinds. Every editable
or resolvable field in the system must eventually depend on a value kind from
this registry.

This applies to both inheritable and non-inheritable fields. A field that does
not support inheritance is still a typed field: it may be a text string, a
number, a boolean, an enum, a color token, a font weight, and so on. The only
difference is that its stored value must be concrete rather than the inherited
marker.

Examples of value kinds:

- `integer`
- `decimal`
- `text`
- `boolean`
- `enum`
- `fontFamily`
- `fontWeight`
- `fontStyle`
- `paletteColorToken`
- `themeColorToken`
- `alpha`
- `iconToken`
- `filePath`
- `relativeFilePath`
- `jsonObject`
- `jsonArray`

Responsibilities:

- validate whether a value is allowed for a kind;
- normalize values where appropriate;
- detect inherited placeholders;
- expose metadata that editors can later use to choose controls;
- fail loudly when an unknown value kind is requested.

Example conceptual API:

```ts
ValueRegistry.validate("decimal", 12.5)
ValueRegistry.assert("fontWeight", 400)
ValueRegistry.normalize("alpha", 1.4)
ValueRegistry.isInherited(value)
```

The registry defines types. It does not define individual fields.

### `FieldDefinition`

A field definition describes one conceptual field in the system.

Example:

```ts
{
  id: "chat.typography.message.fontSize",
  kind: "decimal",
  defaultValue: 17,
  ui: {
    label: "Message font size",
    control: "number",
    min: 1,
    step: 1,
    unit: "px"
  }
}
```

Final responsibilities:

- provide stable field identity;
- declare `kind`;
- declare whether the field is inheritable;
- provide an optional default;
- provide optional editor metadata;
- provide optional constraints such as min, max, step or enum options.

The field definition should not know where a value is stored in SQL or JSON.

Not every piece of concrete content needs to become a generic field-definition
driven form immediately. Large domain-specific content, such as a message list
or animation keyframe list, may keep specialized editors and schemas. However,
the typed leaf values inside those structures should still use the value system
where practical.

### `FieldResolver`

The final resolver model is recursive:

```text
field value is concrete → use it
field value is inherited → ask parent field
parent is inherited → ask its parent
...
no parent has value → use default
```

Conceptually:

```ts
resolveField(field, context) {
  const value = context.valueOf(field)
  if (isConcrete(value)) return validate(field.kind, value)
  return resolveField(field, context.parent)
}
```

The resolver should not care whether the field is a number, color token, font,
boolean, enum or file path. It asks `ValueRegistry` to validate the final value.

### `JsonFieldBinding`

`JsonFieldBinding` is an adapter for the current storage model.

Because many current values live inside JSON objects, a binding maps a
`FieldDefinition` to one or more JSON paths.

Example:

```ts
{
  field: CHAT_TEXT_INPUT_BAR_FIELDS.cursorWidth,
  outputPath: ["cursorWidth"],
  inputPaths: [["cursorWidth"], ["cursor", "width"]]
}
```

This is intentionally an adapter, not the conceptual model. The conceptual model
is the field and its parent chain. JSON paths exist only because the current
storage format is JSON.

## Field catalogs

Field catalogs are module/app/system-specific dictionaries of fields.

Target shape:

```text
src/domain/fields
  chat/
    keyboardFields.ts
    textInputBarFields.ts
    typographyFields.ts
    bubbleFields.ts
    headerFields.ts
  system/
    statusBarFields.ts
    navigationBarFields.ts
  components/
    avatarFields.ts
    labelFields.ts
    buttonIconFields.ts
    audioMessageFields.ts
    videoMessageFields.ts
```

Each catalog owns field definitions for a bounded domain concept.

Examples:

- Chat typography fields know about `message`, `headerTitle`,
  `headerSubtitle`.
- Keyboard fields know about key radius, key padding, pressed effect,
  language, font and bottom icon items.
- Avatar component fields know about corner radius, border width, border color,
  shadow and relief.

Resolvers import catalogs. Editors can later import the same catalogs to build
controls. The goal is to avoid defining the same field twice.

## Editor architecture

Editors should eventually render fields from field definitions.

Target flow:

```text
Editor receives record + field definitions
        │
        ▼
For each field:
  read local stored value
  read inherited/resolved value
  ask ValueRegistry/FieldDefinition which control to use
  render standard field row
        │
        ▼
User edits value
        │
        ▼
Editor stores either concrete value or inherited marker
```

### Standard editor field behavior

Every inheritable editor field should follow the same UI semantics:

- if inherited, the input displays the inherited concrete value in readonly or
  inherited styling;
- if edited, the input displays the local concrete value normally;
- changed fields show the amber/edited state;
- restore resets the local value to inherited/default;
- invalid values are rejected or shown with a standard validation error;
- the editor never invents a custom field row for a concept that already has a
  shared field renderer.

### Editor control selection

Control selection should be driven by `ValueRegistry` and `FieldDefinition`.

Examples:

| Value kind | Editor control |
| --- | --- |
| `integer` | numeric input without browser spinner |
| `decimal` | numeric input without browser spinner |
| `boolean` | checkbox |
| `enum` | dropdown |
| `fontFamily` | production font family selector |
| `fontWeight` | weight selector |
| `fontStyle` | normal/italic selector |
| `paletteColorToken` | palette swatch selector |
| `themeColorToken` | theme semantic color selector |
| `alpha` | 0–1 numeric/slider control |
| `iconToken` | icon token selector |
| `relativeFilePath` | file picker relative to production root |

Editors may still define structure: cards, groups, sections and workflows. They
should not reimplement the same field behavior.

### Per-card dictionary audit

When an editor card is migrated or cleaned, it must be checked as a unit:

```text
visible field
  → FieldDefinition
  → ValueRegistry kind
  → ValueKindControlRegistry control
  → shared editor control/component
```

The card is considered clean only when all visible editable fields follow this
chain, and all hidden/internal fields are explicitly justified.

This audit must happen immediately after finishing a card and again during the
general encapsulation audit. The purpose is to prevent a visually polished card
from still carrying local control decisions such as "this field uses a select
because this editor says so" instead of "this field is a `recordReference`,
therefore the dictionary returns `recordSelect`".

Allowed local editor responsibilities after this audit:

- grouping fields into cards/subcards;
- deciding which internal fields remain hidden;
- computing domain-specific option lists when they depend on current records;
- composing preview helpers such as image thumbnails.

Disallowed after this audit:

- choosing a control type locally for a field that has a dictionary kind;
- duplicating labels, min/max/step, semantic token groups or file picker
  metadata outside `FieldDefinition`;
- creating a new row/control class for an existing value kind without first
  promoting it to the shared editor layer.

Future richer token controls:

- `iconToken` should eventually open a selector/modal with all icons from a
  selected icon theme. If a field allows multiple icons, the control should
  allow multi-select and preserve selection order.
- `themeColorToken` should normally show the tokens relevant to the field
  semantics. A border field should prefer `borders.*`, an icon field should
  prefer `icons.*`, and so on. It should also expose an advanced selector that
  can inspect any theme and show all resolved theme color tokens for that theme,
  allowing the user to intentionally pick a token outside the usual semantic
  subset.

### Editor visual language

The visual editor language remains centralized in `src/debug-ui/editor-ui`.

Editors can decide:

- which cards exist;
- what groups are visible;
- what actions are available;
- what domain-specific rows appear.

Editors should not decide:

- card chrome;
- field row chrome;
- checkbox styling;
- restore button style;
- inherited-state styling;
- amber/edited-state styling;
- common modal styling;
- common icon button styling.

## Data ownership model

All data should have a clear owner level.

### Production

Production owns data that defines the production context:

- root media directory;
- palette;
- approved production fonts;
- actors;
- devices;
- icon themes;
- status bar definitions;
- navigation bar definitions;
- render presets;
- reusable component classes.

Production data is copied or cloned when duplicating a production if it is part
of that production's design system.

### Palette

Palette stores primitive production colors.

Example:

```text
gray_010 = #1A1A1A
blue = #007AFF
red = #FF3B30
```

Palette colors are not component semantics. They are raw approved colors.

Theme tokens reference palette colors.

### Theme

Theme owns semantic system colors and mode-specific system values.

Examples:

- `colors.textPrimary`
- `colors.textSecondary`
- `icons.primary`
- `icons.secondary`
- `icons.accent`
- `borders.primary`
- `keyboard.background`
- `keyboard.keyBackground`
- `cursor.width`
- `cursor.color`
- `surfaceRelief.default`
- `shadows.system`

Theme resolves palette tokens into concrete colors for a given mode.

Theme should not know about one specific shot or message.

### App

App owns values common to all screens/modules of that app.

Examples:

- app icon;
- app wallpaper;
- app typography overrides;
- app-level visual defaults;
- app-specific tokens that are not pure system tokens.

App should not override system-owned values such as keyboard colors if those are
defined as system/theme-owned.

### App instance

App instance is a required level in the current model.

It is not kept only as an override layer. It gives the application a formal
runtime grouping between screens and modules.

This matters for apps that contain several related module/page types. For
example, a chat app may later contain:

- a chat list page;
- a concrete chat page;
- a call page;
- a settings page;
- future notification or media pages.

The app instance gives those module instances a stable parent and keeps app
state/grouping out of individual modules. Even when an app instance has few or
no local overrides, it remains part of the hierarchy because the rest of the
model already depends on that explicit level.

### Module theme/config

Module config owns defaults for a module type within an app/theme context.

Examples for `core.chat`:

- header layout;
- message spacing;
- bubble padding;
- tail style;
- media border/radius/shadow behavior;
- bubble status layout;
- default typography for module-specific roles;
- references to component classes such as avatar, text input bar, keyboard,
  label, audio message, video message or button icon.

Module config represents "how this module behaves by default".

### Component class

Component classes are reusable design objects.

Examples:

- Avatar
- Text input bar
- Keyboard
- Button icon
- Label
- Audio message
- Video message

The component class owns reusable appearance/behavior that should be shared by
all users of that component.

Example:

```text
Avatar component:
  corner radius
  border width
  border color token
  shadow enabled
  relief enabled
```

The module owns size or placement if those are contextual.

Example:

```text
Chat header:
  avatar size = module/header field
  avatar component = shared Avatar class
```

### Module instance

Module instance owns shot/screen-specific content and behavior.

Examples:

- chat messages;
- message timing;
- message media path;
- selected actor for header;
- whether text input bar is visible;
- whether keyboard is visible;
- selected keyboard pressed effect if explicitly per-screen;
- status bar item runtime values if they are screen-specific;
- animation tracks and keyframes.

Module instance should not duplicate reusable component properties unless a
formal component override is being used.

### Screen instance

Screen instance owns screen-level placement and timing in a shot.

Examples:

- duration;
- transition type;
- orientation;
- device state;
- app selection;
- module instance reference;
- transform;
- order inside the shot.

Screen instance should not store duplicated app/module tokens.

### Shot

Shot owns ordered screen instances and shot-wide render identity.

Examples:

- shot slug;
- render name;
- shot version;
- fps override;
- owner device/actor;
- notes;
- ordered screens.

Screen start frames are derived from screen order and duration.

## Inheritance model

Inheritance is field-based, not object-based.

A group may be visually presented as a card, but inheritance happens per field.

Example:

```text
theme.typography.message.fontSize = 17
app.typography.message.fontSize = inherited
module.typography.message.fontSize = inherited

resolved value = 17
```

If the app changes only weight:

```text
theme.typography.message.fontFamily = SF Pro Text
theme.typography.message.fontWeight = 400

app.typography.message.fontFamily = inherited
app.typography.message.fontWeight = 600

module.typography.message.fontFamily = inherited
module.typography.message.fontWeight = inherited

resolved:
  fontFamily = SF Pro Text
  fontWeight = 600
```

The module did not need to copy the font family to inherit it.

### Inheritance stack examples

#### Typography

```text
module field
  → app field
  → theme field
  → field default
```

The module may override weight/style/size where allowed, but certain identity
fields such as font family may be locked depending on the rule for that module.

#### Keyboard system values

```text
screen/module behavior field
  → keyboard component class field
  → field default
```

Keyboard colors are not in this stack because they are system/theme-owned.

```text
theme.keyboard.background
theme.keyboard.keyBackground
theme.keyboard.specialKeyBackground
theme.keyboard.text
```

#### Text input cursor

```text
text input component / local field
  → theme cursor field
  → field default
```

The binding may adapt units if one scope is already scaled and the local scope is
still in design space.

#### Component overrides

```text
local component override
  → component class field
  → field default
```

Overrides should be edited through a standard override modal, not by duplicating
all component fields into every module editor.

## Resource resolution model

Some references are not normal inheritance parents.

Palette, production fonts, icon themes, media assets and production root paths
are resource registries. They resolve references into concrete runtime values,
but they do not behave like `theme → app → module → instance` parent chains.

Examples:

```text
theme.icons.primary = palette token gray_090
palette.gray_090 = #E5E5E5

resolved icon color = #E5E5E5 for the current mode
```

```text
component.buttonIcon.iconToken = chat_send
active icon theme = lucide

resolved icon shape = lucide/chat_send.svg
```

```text
message.mediaPath = messages/video_001.mp4
production.root = /production/root

resolved media file = /production/root/messages/video_001.mp4
```

The rule is:

```text
Inheritance decides which reference/value wins.
Resource resolution turns the winning reference into runtime data.
```

This keeps the normal parent chain simple while still allowing palettes, fonts,
icons and media to be production-scoped and portable.

## Component system

Component classes are reusable black boxes with field definitions.

Target model:

```text
component_classes
  id
  production_id
  component_type
  name
  tokens_json
  metadata_json
```

Each component type has:

- a field catalog;
- a default component seed;
- a component resolver;
- optional override modal support;
- visual-module consumption if it affects render output.

### Component references

Modules reference component classes by ID or by default component type.

Example:

```text
core.chat.header.avatarComponentId = default avatar
core.chat.keyboard.componentId = default keyboard
core.chat.audioMessage.componentId = default audio message
```

The module asks the component class for resolved properties. It should not copy
those component properties into its own token JSON.

### Component overrides

Overrides should be explicit:

```text
componentOverrides:
  avatar:
    cornerRadius: 12
```

The UI should show:

- component name;
- edit/pencil button;
- amber indicator if any override exists;
- modal listing overridable fields;
- restore per field;
- validation through `ValueRegistry`.

The module editor should not permanently list every component property inline.

### Nested components

Components may embed other components.

Example:

```text
Notification component
  contains Avatar component
  contains ButtonIcon component
```

Nested component resolution follows the same rule:

```text
local override → referenced component class → default
```

## Apps, modules and screens

### App

An app is a reusable application model such as Messages, WhatsApp-like Chat or
future app surfaces.

App-level data describes values shared by all module screens of that app.

App should not store shot-specific content.

### Module

A module is a reusable feature/screen type inside an app.

Examples:

- `core.chat`
- future feed screen
- future call screen
- future notification screen

Module config defines reusable layout and behavior defaults.

### Module instance

The module instance is the concrete occurrence of a module inside a screen/shot.

For Chat, module instance owns:

- concrete message list;
- header actor/text content;
- media paths;
- timing;
- message-specific animation tracks;
- per-screen behavior flags.

### Screen instance

The screen instance places a module/app state in a shot timeline.

It owns:

- duration;
- order;
- orientation;
- transition;
- device state;
- transform;
- app/module binding.

Screen instances should be ordered in the shot. Start frames are derived:

```text
start(screen N) = sum(duration of previous screens)
```

## Animation model

Animation is field-based.

A field can be declared animable. Animation does not change the ownership of the
field; it changes the value at a frame.

Target model:

```text
animation track:
  fieldId
  targetId
  keyframes:
    frame
    value
    interpolation
```

Examples:

- message text;
- message status text;
- delivery status;
- header subtitle;
- future numeric positions or opacity.

### Text interpolation

For text fields:

- `hold` switches instantly at the keyframe;
- `linear` / `ease` can remove the differing suffix and type the new suffix
  across the keyframe interval.

Example:

```text
Hola Jorge → Hola Juan
```

The common prefix is `Hola J`. The animation deletes/replaces the differing
characters instead of treating the entire string as unrelated.

### Timeline scale

Animation keyframes are relative to the relevant screen/module context, not to
absolute shot order.

For a message:

```text
track duration = from message start to end of current screen
```

For header-level animation:

```text
track duration = full screen duration
```

If the screen is reordered inside the shot, animation keyframes remain valid
because they are screen-local.

## Preview and render architecture

Preview and render must use the same visual pipeline.

```text
domain resolver
  → resolved model
  → frame model
  → visual module tree
  → preview renderer
  → PNG/video renderer
```

### Resolved model vs frame model

The system should keep two runtime models separate.

The resolved model is the result of data ownership, inheritance and resource
resolution. It answers:

```text
For this screen/module, what are the final values before time is evaluated?
```

Examples:

- resolved theme colors for the current mode;
- resolved typography;
- resolved component class properties plus local overrides;
- resolved module layout values;
- resolved media paths;
- resolved screen/device state.

The frame model is the evaluated state for one specific frame. It answers:

```text
At frame N, what exactly should be visible?
```

Examples:

- current message write-on text;
- current animated subtitle;
- keyboard pressed key;
- current video frame;
- audio play progress;
- status text/ticks at that frame.

Target flow:

```text
resolveScreenModel(records, screenId)
  → resolved model

evaluateFrame(resolved model, frame)
  → frame model

renderFrame(frame model, render options)
  → pixels
```

The preview changes the selected frame often, but should not re-resolve the
entire storage model for concepts that only need frame evaluation.

### Preview responsibilities

The preview owns:

- current frame selection;
- screen navigation;
- zoom;
- optional device frame overlay;
- preview-only controls;
- render current frame button;
- display of selected device/theme/frame info.

The preview does not own:

- chat layout;
- message timing;
- keyboard behavior;
- theme token resolution;
- media scaling;
- component inheritance.

### Render responsibilities

The renderer owns:

- output resolution;
- output scale;
- frame export;
- video frame extraction where needed;
- deterministic render output.

The renderer should not reinterpret data differently from preview.

### Render reproducibility

Final renders should be reproducible.

The target model should eventually save enough render metadata to reproduce a
render later:

- production id;
- shot id;
- screen order/durations at render time;
- theme id;
- icon theme id;
- device id;
- render preset;
- output scale;
- app/module/component versions or snapshots where needed;
- source media paths;
- frame range.

This can become a future `render_manifest.json`. The important rule now is that
preview and render must be driven by the same resolved/frame model, not by two
separate interpretations of the database.

### Device frame

The device frame/border is an overlay layer. It must not affect layout
calculations. It may be shown or hidden in preview and render.

### Media and video

Images and videos are referenced relative to production root.

For video in preview/render:

- the system should request the required frame;
- while a frame is loading, preview may keep the previous rendered frame instead
  of flashing black;
- render should use deterministic frame extraction.

## Units and scaling

The system distinguishes design-space units from output pixels.

Typical flow:

```text
stored value in design pixels
  × device scaleToPixels
  × render output scale
  = output pixels
```

Resolvers should scale design-space values before sending them to visual
modules. Visual modules should not guess whether a value is already scaled.

If a binding mixes scopes where one value is already scaled and another is not,
the binding must adapt that explicitly. That adaptation belongs with the binding,
not hidden in renderer code.

## Color model

The target color model has three levels.

### Palette colors

Palette colors are primitive production-approved colors.

Example:

```text
gray_010 = #1A1A1A
blue = #007AFF
```

Palette colors do not know whether they are text, border, icon or background.

### Theme color tokens

Theme tokens give semantic meaning to palette colors.

Examples:

```text
colors.textPrimary = gray_000
icons.primary = gray_000
icons.accent = blue
borders.primary = gray_080
```

Theme tokens may vary by light/dark mode.

Editor controls for theme color tokens should select semantic theme tokens, not
primitive palette colors. The selected token is resolved later through the
active theme and palette.

### Component/module references

Components and modules should reference semantic theme tokens where possible,
not primitive palette colors.

Example:

```text
buttonIcon.borderColorToken = borders.primary
audioMessage.playCircleColorToken = icons.accent
```

Exceptions should be explicit and audited.

### Alpha

Alpha should be stored separately from color token where possible:

```text
backgroundColorToken = colors.surface
backgroundAlpha = 0.86
```

This avoids mixing RGBA strings with token-based color resolution.

## Typography model

Production fonts define approved font families.

A font picker should choose only approved production font families.

Typography values should be split into:

- family;
- weight;
- style;
- size;
- line height.

Theme/app/module can inherit typography field by field.

The final runtime style passed to preview/render should be normalized:

```text
fontFamily = approved family name
fontWeight = numeric weight
fontStyle = normal | italic
fontSize = resolved/scaled number
lineHeight = resolved/scaled number
```

The browser/renderer may choose the best matching font file from registered
`@font-face` definitions, but the app's own data model should be normalized.

## File and asset model

Production owns a root media directory.

All file pickers inside production content should store paths relative to that
root.

Examples:

```text
actors/alex/avatar.png
icon-themes/material-rounded-basic/chat_send.svg
messages/media/video_001.mov
```

Absolute paths may be used by the editor/browser dialog temporarily, but stored
data should be relative for portability.

## Icon model

Icon themes are production data.

An icon theme resolves icon token → SVG shape.

The theme resolves icon color.

```text
icon token = chat_send
icon theme = lucide
theme color = icons.accent
```

Apps/modules/components should reference icon tokens, not SVG files directly.

The editor control for icon tokens should resolve the active icon theme for
preview, but store only the icon token. Theme/icon-theme selection determines
the concrete SVG.

## Status bar, navigation bar and keyboard

These are system UI concepts.

### Status bar

Status bar is a reusable system definition:

- left/right zones;
- ordered items;
- token/text/generated item types;
- item size/gap/padding;
- runtime values from device state or module instance behavior.

The status bar module draws itself from device/theme/app context.

### Navigation bar

Navigation bar is a reusable system definition:

- generated buttons;
- filled/stroke mode;
- stroke width;
- corner radius;
- background with alpha;
- item size/padding.

### Keyboard

Keyboard has two major parts:

- key layout and key behavior;
- text input bar / composer area.

Keyboard colors come from theme. Keyboard geometry and interaction options come
from keyboard component/class fields and allowed instance behavior.

The message write-on routine feeds:

- text input bar text;
- pressed key;
- keyboard mode;
- bubble visibility timing.

Outgoing messages may use keyboard/text input. Incoming/system messages should
not show keyboard behavior unless explicitly supported by a future module.

## Resolver responsibilities

Domain resolution should be composed from small resolvers, not one megaclass.

Recommended split:

- resource resolvers: palette, theme colors, fonts, icon themes, media paths;
- field resolvers: inherited/concrete/default values;
- component resolvers: component classes plus overrides;
- module resolvers: module-specific runtime props;
- timeline/frame evaluators: animation, write-on, video/audio progress;
- screen resolvers: device metrics, orientation, order, transitions.

Domain resolvers should:

- load related records through repository;
- validate stored JSON through schemas;
- resolve field inheritance;
- resolve component class references and overrides;
- resolve palette/theme/icon/font references;
- compute timeline timing;
- compute screen orientation and device metrics;
- scale design-space values;
- output resolved props validated by Zod.

Resolvers should not:

- know editor-specific UI layout;
- contain local field catalogs when a catalog exists;
- contain long-term legacy fallbacks;
- draw anything;
- call preview or renderer APIs.

## Visual module responsibilities

Visual modules consume resolved props and produce renderer-agnostic nodes.

They may:

- be defensive against missing optional visual fields;
- compute local visual child layout;
- use already-resolved styles;
- output metadata useful for debug.

They should not:

- read SQLite;
- resolve inheritance;
- resolve palette/theme/component references;
- understand old data formats;
- depend on debug UI components.

## Renderer-agnostic render tree

The render tree should be stable and serializable.

Each node should describe:

- id;
- type;
- role;
- box;
- style;
- text/content/media where needed;
- children;
- debug metadata where useful.

Visual validation should be able to render the same resolved props repeatedly
and get identical trees.

## Modals and destructive actions

All destructive actions should use shared modals.

Rules:

- no raw `window.confirm`;
- no raw `window.prompt`;
- delete actions require confirmation;
- deleting a referenced record should show usages and block deletion unless a
  safe migration/replacement path exists;
- default focused action should be cancel where destructive risk exists;
- modals should render at app level, not inside one panel where other panels can
  cover them.

## Consolidation and fallback policy

The final system should not support old experimental shapes forever.

Allowed:

- migrations;
- normalizers;
- audit scripts;
- temporary fallbacks marked for removal;
- defensive visual fallback after resolved props.

Not allowed:

- permanent support for two JSON shapes;
- hidden legacy branches in visual modules;
- duplicated field definitions in UI and resolver;
- component properties copied into every module that uses them.

Before destructive cleanup:

```text
commit clean state
run read-only audit
run migration/normalizer
run validations
remove temporary fallback
commit result
```

## Target implementation checklist

The final model is reached when:

- every editable/resolvable field has a `FieldDefinition`;
- every field definition references a `ValueRegistry` kind;
- editor controls are selected from field definitions, not handwritten per
  field;
- inheritance is resolved per field, not by merging whole objects;
- component class fields are not duplicated in modules;
- module instance only stores content/behavior/animation that is truly
  instance-specific;
- preview and render consume the same resolved props;
- visual modules do not resolve storage-level data;
- no production color/font/icon picker bypasses approved palette/font/icon
  registries;
- current-model audits fail on old shapes instead of silently accepting them.

## Mental model

The intended system should feel like this:

```text
ValueRegistry
  "What kind of value is allowed?"

FieldDefinition
  "What field is this?"

JsonFieldBinding
  "Where does this field currently live in JSON?"

FieldResolver
  "What is the resolved value after inheritance?"

Editor field renderer
  "How do I let the user edit this field?"

Domain resolver
  "How do all records become render-ready props?"

Visual module
  "How do render-ready props become a visual tree?"

Preview/render
  "How do visual nodes become pixels?"
```

If a future feature cannot be placed clearly in this model, pause before
implementing it. The architecture should absorb new features by adding fields,
components, modules or visual modules, not by adding exceptions.
