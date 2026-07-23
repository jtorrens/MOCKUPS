# 2026-07-03 - Avalonia/Suki editor modernization and field audit

This document is the working audit for continuing the Avalonia/Suki desktop editor modernization on branch:

```text
codex/editor-modernization-rules
```

It replaces the earlier incremental handoff view. The important change is that the document is now oriented around the rules introduced during the refactor thread:

- `MainWindow.axaml.cs` is shell-only.
- Every editable scalar value goes through `FieldDefinition` and dictionary controls.
- Missing controls are added to the dictionary/value-kind layer first.
- Collection editors may have custom row chrome, but scalar values inside rows must still use dictionary fields.
- Runtime/design preview should be resolved through a shared web/runtime path, not reimplemented in Avalonia.

## Current desktop editor shape

As of the `v0.60.0` visible title phase:

```text
MainWindow.axaml.cs                     shell/orchestration only, ~561 lines
DictionaryFieldControl.cs              row host/default/restore state, ~215 lines
DictionaryValueControlFactory.cs       ValueKind -> dictionary value control
EditorFieldValueRouter.cs              field create/read/storage/commit routing
EditorFieldCommitCoordinator.cs        shared same-value/default commit behavior
EditorCardHostController.cs            editor card host and accordion behavior
EditorViewStateController.cs           preserve card/scroll state within same editor type
EditorNavigationRenderer.cs            tree rendering
EditorPreviewController.cs             preview selector wiring
```

Recent extracted dictionary controls include:

```text
DictionaryBooleanControl
DictionaryOptionTokenControl
DictionaryTextControl
DictionaryHexColorControl
DictionaryThemeTokenControl
DictionaryPaletteTokenControl
DictionaryPalettePairControl
DictionaryIntegerPairControl
DictionaryPathBrowseButton
HueDegreesControl
IconSlotsControl
```

Do not move editor-specific logic back into `MainWindow`. If a future change needs special behavior, put it in:

- a dictionary value control, when it edits a scalar value;
- a record field catalog/value service, when it defines or persists field data;
- a focused editor-shell helper/controller, when it is generic UI shell behavior;
- a collection editor class, when it is structured list editing.

## Current field/data pipeline

The target route is already partly in place:

```text
editor layout metadata
-> RecordClassFieldCatalog / ComponentClassFieldCatalog / core field service
-> FieldDefinition
-> ValueKind
-> DictionaryValueControlFactory
-> IDictionaryValueControl
-> DictionaryFieldControl
-> EditorFieldCommitCoordinator
-> EditorFieldValueRouter
-> field value service
-> SpikeDatabase
-> preview refresh / resolver
```

Important current files:

```text
spikes/desktop-editor-shell/EditorShell/RecordClassFieldCatalog.cs
spikes/desktop-editor-shell/EditorShell/RecordClassFieldValueService.cs
spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs
spikes/desktop-editor-shell/EditorShell/ComponentClassFieldValueService.cs
spikes/desktop-editor-shell/EditorShell/CoreFieldValueService.cs
spikes/desktop-editor-shell/EditorShell/EditorFieldValueRouter.cs
```

`SpikeDatabase` is still intentionally large. Do not split it until field catalogs, field value routing, and resolver boundaries are stable.

## Dictionary/value-kind status

Current `ValueKind` values:

```text
StringSingleLine
StringReadOnly
StringMultiline
Integer
HueDegrees
IntegerPair
DirectoryPath
ImageFilePath
OptionToken
ThemeToken
HexColor
PaletteColorToken
PaletteColorPair
IconSlots
Boolean
```

Known missing or too-broad value kinds before completing parity:

```text
Decimal
FontFamily
FontWeight
FontStyle
IconToken
IconTokenList
ThemeRadiusToken
SurfaceStyle
ComponentOverride
ScreenInstanceSelector
ModuleInstanceSelector
FrameRange / timeline values
```

Guideline:

- Do not infer semantics from field id strings when metadata can declare them.
- Use pair metadata for labels such as `X/Y`, `W/H`, `Light/Dark`.
- `IDictionaryValueControl.SetValue` is a silent programmatic repaint and must not emit `ValueChanged` or `ValueCommitted`.

## UI behavior already decided in this thread

Preserve these decisions:

- Editor cards behave like normal accordions: click header opens/closes.
- Card content opens immediately; avoid secondary reveal animations inside cards.
- Expander/global transitions are intentionally very fast.
- ComboBox opens on press, but there is still a residual Suki/Avalonia fade to debug later.
- ComboBox closes on outside click and Escape.
- Text inputs keep standard selection behavior as far as Avalonia/Wacom allows; no sticky custom context menu.
- Palette color picker is a modal picker, defaulting to 3 columns, with fixed width and vertical-only growth.
- Palette neutral colors show an `N` mark on the swatch.
- Editor card/scroll state is preserved only when moving between records of the same editor type; changing editor type returns to defaults.
- Usage indicators are loaded at startup, refreshed before delete, and can be refreshed explicitly through `Update usage`.

## Records currently covered by dictionary route

The Avalonia/Suki tree can open editors for:

```text
Project
App
Module
Episode
Shot
PaletteColor
Device
Actor
Theme
ProductionFont
IconTheme
StatusBar
NavigationBar
ComponentClass
```

Coverage is not equal across these records.

Mostly covered or actively migrated:

```text
Project core fields
Episode basic fields
PaletteColor
Device
Actor
Theme
ProductionFont
IconTheme read-only metadata plus token collection editor
StatusBar scalar fields plus items collection editor
NavigationBar scalar fields plus items collection editor
ComponentClass partial fields
```

Still weak or not meaningfully migrated:

```text
App
Module
Shot
Screen instances
Module instances
Component overrides on instances
Timeline/keyframe/frame data
Screen-level records if/when they are added to the tree
```

These should not be implemented by adding ad hoc controls. They need field catalogs, value services, and dictionary controls first.

## Pending record-class migration checklist

### App

React/source references:

```text
src/domain/fields/appFields.ts
src/debug-ui/field-descriptors/appDescriptors.ts
src/debug-ui/components/ProjectTree.tsx
```

Known old/domain fields include:

```text
app.id
app.productionId
app.name
app.bundleKey
app.appType
app.config
app.metadata
app.wallpaper.kind
app.wallpaper.opacity
app.wallpaper.color.light
app.wallpaper.color.dark
app.wallpaper.image.filePath
app.note
app.icon.filePath
app.icon.scale
app.icon.offsetX
app.icon.offsetY
app.icon.baseSize
```

Avalonia currently has tree nodes for `App` and add/duplicate/delete plumbing, but app-specific scalar fields are not yet cataloged like `Device`, `Actor`, or `Theme`.

Migration rules:

- Add `app.*` descriptors to `RecordClassFieldCatalog`.
- Add app read/write routing to `RecordClassFieldValueService`.
- Use existing dictionary controls for wallpaper color/image/icon fields where possible.
- Add missing `Decimal` or numeric control if opacity/scale should not remain plain string.
- Do not build a custom App editor in `MainWindow`.

### Module

React/source references:

```text
src/domain/schemas/module.ts
src/debug-ui/field-descriptors/recordDescriptors.ts
src/debug-ui/field-descriptors/coreChatV1Descriptors.ts
src/debug-ui/components/ComponentOverrideModal.tsx
src/domain/resolvers/resolveChatScreen.ts
```

Known module concerns:

```text
module identity/schema/version fields
module design typography fields
module chat bubble colors
module chat bubble tail/shape fields
module status icon fields
module component overrides
module theme config per theme/app/schema
```

Avalonia currently has `Module` nodes and add/duplicate/delete support, but module-specific editing is still not migrated into the dictionary/catalog route.

Migration rules:

- Do not port the old React form as a custom Avalonia editor.
- Start with a small `module.generic` catalog for identity, schema/version, and notes.
- Then add `module.core.chat` fields as dictionary fields grouped by semantic section.
- Introduce semantic value kinds before adding override-specific fields.
- Component overrides should become a structured collection editor, but scalar values inside each override must still use dictionary definitions.

### Episode

Current Avalonia coverage:

```text
episode.slug
episode.sortOrder
core.name
core.notes
```

React/domain references:

```text
src/domain/fields/episodeFields.ts
```

Missing or not explicit:

```text
episode.id
episode.productionId
episode.metadata
episode.note
```

Decision:

- `id` and `productionId` can stay read-only/internal if shown.
- `metadata` needs a structured strategy before exposing.
- `note` probably maps to `core.notes` unless we decide to preserve a separate field.

### Shot

React/domain references:

```text
src/domain/fields/shotFields.ts
src/domain/resolvers/resolveShot.ts
src/debug-ui/preview/PreviewOptionsCard.tsx
```

Known fields include:

```text
shot.id
shot.productionId
shot.episodeId
shot.ownerActorId
shot.name
shot.slug
shot.version
shot.sortOrder
shot.durationFrames
shot.fps
shot.renderPresetId
shot.canvas
shot.metadata
shot.note
shot.renderName
shot.ownerDevice
```

Avalonia currently has `Shot` tree nodes and duplicate/delete support, but shot scalar editing is not cataloged.

Migration rules:

- Add `shot.*` to `RecordClassFieldCatalog`.
- Add shot read/write to `RecordClassFieldValueService`.
- Use `Integer` for fps/duration/version/sort order if integer remains adequate.
- Add `OptionToken` backed by project data for owner actor/device/render preset.
- `canvas` and `metadata` should not become raw multiline JSON unless explicitly marked internal/debug.

### Screen instances and module/component instances

React/source references:

```text
src/domain/resolvers/resolveShot.ts
src/domain/resolvers/resolveScreenInstance.ts
src/debug-ui/components/ProjectTree.tsx
src/debug-ui/components/ComponentOverrideModal.tsx
```

Current Avalonia state:

- Screen-level records are intentionally noted as added later in seed data.
- The tree does not yet expose a full screen instance editor.
- Module/component instances and component overrides are not migrated to the dictionary architecture.

Migration rules:

- Treat screen/module/component instances as structured collections, not scalar record forms only.
- Add collection editors only after scalar field kinds for override values exist.
- An override row may use custom chrome for target selection and row actions, but every editable scalar override value must be a `FieldDefinition` and dictionary control.
- Introduce a resolver layer before relying on preview output for correctness.

## Component class audit

Source of truth for old React/Electron component editor:

```text
src/debug-ui/editors/ComponentClassRecordEditor.tsx
src/domain/repository/fixtures/exampleDataset.ts
src/domain/resolvers/resolveChatScreen.ts
```

New Avalonia/Suki component files:

```text
spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs
spikes/desktop-editor-shell/EditorShell/ComponentClassFieldValueService.cs
spikes/desktop-editor-shell/Data/SpikeDatabase.cs
src/desktop-preview/renderDesignPreviewHtml.tsx
```

### Component type naming mismatch

Old runtime component types:

```text
avatar
button_icon
label
audio_message
video_message
text_input_bar
keyboard
```

New Avalonia spike uses mixed/new names in places:

```text
avatar
buttonIcon
label
audio
video
textInputBar
keyboard
```

Risk:

- old resolver functions query names such as `button_icon`, `audio_message`, `video_message`, `text_input_bar`;
- new names may not resolve unless explicitly mapped.

Rule:

- Either preserve runtime names in DB/config or create one explicit normalization layer.
- Do not let each editor or preview path invent its own component type naming.

### Generic component-class gaps

Before filling fields one by one, add or normalize:

```text
FontFamily
FontWeight
FontStyle
Decimal
IconToken
IconTokenList / IconSlots
ThemeRadiusToken
SurfaceStyle
```

These missing value kinds are why several component-class fields are absent or represented as plain strings.

### Avatar

Old fields:

```text
cornerRadius
borderWidth
borderColorToken
shadowEnabled
shadowToken
surfaceReliefEnabled
```

New fields:

```text
avatar.defaultSize
avatar.cornerRadiusToken
style.*
```

Discrepancies:

- `defaultSize` is new.
- numeric `cornerRadius` became tokenized `cornerRadiusToken`.
- `shadowToken` is missing.
- `surfaceReliefEnabled` is represented as `style.reliefEnabled`.

Decision needed:

- Confirm whether avatar size is component-class-owned or module/context-owned.
- Decide whether radii are always theme tokens or whether numeric radius remains valid.

### Button Icon

Old fields:

```text
cornerRadius
borderWidth
iconPadding
borderColorToken
shadowEnabled
surfaceReliefEnabled
labelEnabled
labelPosition
labelPadding
labelSize
labelColorToken
```

New fields:

```text
buttonIcon.iconPadding
buttonIcon.labelEnabled
buttonIcon.labelPosition
buttonIcon.labelSize
buttonIcon.labelPadding
style.*
```

Missing or unclear:

```text
labelColorToken
cornerRadius
borderWidth
borderColorToken
shadowEnabled
surfaceReliefEnabled
```

Some style fields may be intentionally covered by `style.*`, but the mapping must be explicit.

### Label

Old fields:

```text
sizingMode
width
height
paddingX
paddingY
backgroundVisible
backgroundColorToken
cornerRadius
borderWidth
borderColorToken
fontSize
fontFamily
fontWeight
fontStyle
textColorToken
shadowEnabled
shadowToken
surfaceReliefEnabled
```

New fields:

```text
label.dimensionMode
label.size
label.padding
label.backgroundVisible
label.backgroundColorToken
label.textColorToken
label.textSize
label.textStyle
style.*
```

Missing:

```text
fontFamily
fontWeight
shadowToken
```

Changed:

- `width`/`height` became `size` pair.
- `paddingX`/`paddingY` became `padding` pair.
- `fontSize` became `textSize`.
- `sizingMode` became `dimensionMode`.
- border/corner/shadow/relief moved conceptually into `style.*`.

### Audio

Old component type:

```text
audio_message
```

Old fields:

```text
width
height
avatarSize
avatarGap
avatarPosition
microphoneBadgeSize
microphoneBadgeIconToken
playCircleSize
playCircleColorToken
playIconColorToken
progressKnobSize
waveformBarCount
waveformGap
waveformMinHeight
waveformMaxHeight
waveformColorToken
waveformPlayedColorToken
textSize
textColorToken
cornerRadius
borderWidth
borderColorToken
shadowEnabled
shadowToken
surfaceReliefEnabled
```

New fields:

```text
audio.size
audio.avatarPosition
audio.avatarSize
audio.textSize
audio.playColorToken
audio.waveformColorToken
audio.knobSize
style.*
```

Missing:

```text
avatarGap
microphoneBadgeSize
microphoneBadgeIconToken
playCircleSize
playIconColorToken
waveformBarCount
waveformGap
waveformMinHeight
waveformMaxHeight
waveformPlayedColorToken
textColorToken
shadowToken
```

Changed:

- `width`/`height` became `size` pair.
- `progressKnobSize` became `knobSize`.
- style fields are partly under `style.*`.

### Video

Old component type:

```text
video_message
```

Old fields:

```text
playOverlayEnabled
playCircleSize
playCircleAlpha
playCircleColorToken
playIconColorToken
statusVisible
statusIconToken
statusColorToken
statusSize
statusPaddingX
statusPaddingY
statusGap
cornerRadius
borderWidth
borderColorToken
shadowEnabled
shadowToken
surfaceReliefEnabled
```

New fields:

```text
video.statusVisible
video.statusHeight
video.statusIconSlots
video.playOverlayVisible
video.playColorToken
style.*
```

Missing:

```text
playCircleSize
playCircleAlpha
playIconColorToken
statusIconToken
statusColorToken
statusSize
statusPaddingX
statusPaddingY
statusGap
shadowToken
```

Changed:

- `playOverlayEnabled` became `playOverlayVisible`.
- `statusIconToken` became `statusIconSlots`.
- `playCircleColorToken` became `playColorToken`.

Decision:

- Decide whether video status is a single icon or slots.
- If slots remain, add a deliberate runtime adapter.

### Text Input Bar

Old component type:

```text
text_input_bar
```

Old fields:

```text
placeholder
idleTextColor
fieldRadius
fieldShadowEnabled
cursorVisible
cursorWidth
cursorBlinkFrames
cursorColor
iconSets.left.idle
iconSets.left.typing
iconSets.right.idle
iconSets.right.typing
```

New fields:

```text
textInput.height
textInput.placeholder
textInput.idleTextColorToken
textInput.cursorColorToken
textInput.cursorWidth
textInput.cursorBlinkFrames
```

Missing:

```text
fieldRadius
fieldShadowEnabled
cursorVisible
iconSets.left.idle
iconSets.left.typing
iconSets.right.idle
iconSets.right.typing
```

Changed:

- `idleTextColor` became `idleTextColorToken`.
- `cursorColor` became `cursorColorToken`.
- `height` was added.

Recommendation:

- Add icon slots for idle/typing left/right using a dictionary-owned value kind.
- Add field appearance fields or map them explicitly to `style.*`.

### Keyboard

Old fields:

```text
language
pushDurationFrames
messageGapToTextInput
fontFamily
fontWeight
fontStyle
pressedEffect
keyRadius
keyPadding
keyShadowEnabled
surfaceReliefEnabled
bottomItems.left
bottomItems.right
```

Old `pressedEffect` options:

```text
popover
inPlace
none
```

New fields:

```text
keyboard.keyPadding
keyboard.keyCornerRadius
keyboard.keyShadowEnabled
keyboard.pressedEffect
keyboard.specialKeyTextScale
keyboard.emojiScale
keyboard.bottomIconSlots
```

New `pressedEffect` options:

```text
popup
scale
none
```

Missing:

```text
language
pushDurationFrames
messageGapToTextInput
fontFamily
fontWeight
fontStyle
surfaceReliefEnabled
```

Changed:

- `keyRadius` became `keyCornerRadius`.
- `bottomItems` became `bottomIconSlots`.
- `specialKeyTextScale` and `emojiScale` are new useful controls.

Bug/runtime mismatch:

- special key icons are not shown in design preview;
- old renderer generated those SVG/glyphs internally from keyboard roles, not from ordinary icon tokens;
- do not replace them with icon-theme tokens unless that is a deliberate product decision.

## Design preview/resolver caution

Useful idea:

- Component-class design preview should be web-rendered.
- `DesignPreviewPayload` carrying component-class context is directionally good.

Risk:

- `src/desktop-preview/renderDesignPreviewHtml.tsx` can become a parallel resolver if it keeps rebuilding theme, device, component, and module logic locally.

Rule:

- Prefer an explicit resolver pipeline:

```text
editable data
-> resolved data
-> frame/module/screen data
-> preview payload
-> web runtime rendering
```

Do not duplicate final runtime decisions in Avalonia or in a second TypeScript preview helper.

## Recommended order from here

1. Commit the current shell/dictionary cleanup once validated.
2. Run the field parity audit against React/source fields.
3. Add missing semantic `ValueKind`s before adding App/Module/Shot fields.
4. Migrate `App` scalar fields through `RecordClassFieldCatalog` and `RecordClassFieldValueService`.
5. Migrate `Shot` scalar fields, including owner actor/device option lists.
6. Migrate `Module` in small layers: generic identity first, then `core.chat` design fields.
7. Normalize component type naming before expanding component-class fields further.
8. Complete component-class field parity component by component.
9. Add screen/module/component instance editors as structured collection editors, not ad hoc scalar forms.
10. Introduce resolver/preview boundary before splitting `SpikeDatabase`.

## Do not copy forward blindly

Avoid reintroducing:

- field definitions directly in `MainWindow`;
- field persistence directly in `MainWindow`;
- ad hoc scalar controls in record-specific editors;
- local preview resolver logic that duplicates runtime behavior;
- component type renames without a single explicit mapping layer;
- one-off UI fixes inside a specific editor when the issue belongs to a dictionary control.

## Worth preserving

These ideas are aligned with the current architecture:

- `editor_shell_non_negotiables.md` and `editor_modernization_roadmap.md`;
- `RecordClassFieldCatalog` and `ComponentClassFieldCatalog`;
- `EditorFieldValueRouter`;
- dictionary-owned value controls;
- web-rendered design preview;
- usage markers and delete blocking based on reference usage;
- state preservation within the same editor type only.
