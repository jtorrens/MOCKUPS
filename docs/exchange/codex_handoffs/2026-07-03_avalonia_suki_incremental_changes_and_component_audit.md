# 2026-07-03 — Incremental handoff: Avalonia/Suki changes and component-class audit

This document captures work and findings made after the previous stable cut, for a parallel refactor thread that started from an earlier commit.

It is intentionally not a full project handoff. It focuses on:

- local changes made after the last commit in this thread;
- architectural rules added or reinforced;
- component-class editor discrepancies found by comparing the old React/Electron editor against the new Avalonia/Suki spike;
- bugs observed during that comparison.

## Current branch and state at time of writing

Workspace:

```text
/Volumes/SD_02/PROYECTOS/MOCKUPS
```

Branch:

```text
codex/editor-modernization-rules
```

Recent commits visible in this thread:

```text
ab813aa a
eb37538 Connect desktop design preview resolver
e4a5dda Speed up shell expander transitions
e1d28cd Add component class editors and token pickers
ff19532 Refactor desktop editor shell classes
b54cd79 Implement desktop navigation bar editor
a97fb2a Implement desktop status bar editor
8a35a58 Implement desktop icon theme management
```

There were uncommitted changes when this document was created.

Tracked modified files:

```text
AGENTS.md
docs/architecture/editor_shell_non_negotiables.md
spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs
spikes/desktop-editor-shell/EditorShell/DictionaryFieldControl.cs
spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs
spikes/desktop-editor-shell/MainWindow.axaml.cs
src/desktop-preview/renderDesignPreviewHtml.tsx
src/visual/modules/atomic/NavigationBarModule.ts
```

Untracked files present:

```text
docs/architecture/editor_modernization_roadmap.md
spikes/desktop-editor-shell/EditorShell/RecordClassFieldCatalog.cs
```

## Important architectural rules reinforced

The root `AGENTS.md` was updated to require reading:

```text
docs/architecture/editor_shell_non_negotiables.md
docs/architecture/editor_modernization_roadmap.md
```

`docs/architecture/editor_shell_non_negotiables.md` was updated with the same extra reference.

Core rules to preserve in the other refactor thread:

1. `spikes/desktop-editor-shell/MainWindow.axaml.cs` must remain shell-only.
2. Editable scalar fields must go through:

```text
editor layout metadata
→ FieldDefinition
→ ValueKind
→ DictionaryFieldControl / registered dictionary control
→ generic commit path
→ repository/database
```

3. If a control does not exist, extend the dictionary/value-kind layer first. Do not add ad hoc `TextBox`, `ComboBox`, color picker, icon picker, font picker, etc. inside an editor.
4. Collection editors may have custom row chrome, but scalar values inside those rows must still use dictionary fields.

## New roadmap draft added locally

Untracked file:

```text
docs/architecture/editor_modernization_roadmap.md
```

Purpose:

- defines cleanup direction from the current spike toward a more data-driven editor shell;
- orders the cleanup in small phases;
- explicitly discourages broad rewrites before field catalogs and field access are extracted.

Key target architecture from that draft:

```text
editor layout metadata
  -> field catalog
  -> FieldDefinition
  -> ValueKind
  -> dictionary control registry
  -> shared commit coordinator
  -> field value service
  -> repository/database
  -> resolver
  -> preview payload/frame model
```

Recommended use in the other thread:

- keep the document if the branch continues the same modernization strategy;
- if the other branch has already restructured this differently, port only the guardrails and cleanup phases.

## Partial field-catalog extraction added locally

Untracked file:

```text
spikes/desktop-editor-shell/EditorShell/RecordClassFieldCatalog.cs
```

Current content covers only low-risk fields:

- `project.slug`
- `project.defaultFps`
- `project.mediaRoot`
- `episode.slug`
- `episode.sortOrder`
- `palette.token`
- `palette.valueHex`
- `palette.isNeutral`
- `palette.source`
- `palette.protected`
- `palette.hiddenFromPickers`
- `palette.note`

`MainWindow.axaml.cs` was partially changed to use `RecordClassFieldCatalog.Get(fieldId)` for project, episode, and palette scalar fields instead of constructing those field definitions inline.

Status:

- This is directionally correct.
- It is incomplete.
- It still leaves many editor-specific field branches in `MainWindow`.
- Treat this as a salvageable first step, not as complete architecture.

## Local design-preview changes made after previous cut

Files touched:

```text
spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs
spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs
src/desktop-preview/renderDesignPreviewHtml.tsx
src/visual/modules/atomic/NavigationBarModule.ts
```

### What changed

`DesignPreviewPayload` was extended with:

```text
ComponentType
DesignPreviewJson
```

`DesignPreviewPayloadFactory` now emits a design preview payload for:

```text
ProjectTreeNodeKind.ComponentClass
```

`WebDesignPreviewRenderer` forwards `componentType` and `designPreviewJson` into the web renderer.

`src/desktop-preview/renderDesignPreviewHtml.tsx` was expanded heavily to render component-class design previews using existing web/runtime modules where possible:

- `AvatarModule`
- `TextInputBarModule`
- `KeyboardModule`
- `StatusBarModule`
- `NavigationBarModule`

It adds local helper functions for:

- reading config values;
- resolving theme tokens;
- resolving palette values;
- generating a generic avatar image;
- building component preview renderable nodes.

`src/visual/modules/atomic/NavigationBarModule.ts` had a defensive change so navigation bar input is read safely from `input.navigationBar`.

### Status / caution

These design-preview changes are useful as a prototype, but they should not be treated as final architecture.

Known concern:

- some design preview logic is being reconstructed inside `renderDesignPreviewHtml.tsx`;
- final direction should reuse the same resolver/runtime path as normal preview as much as possible;
- avoid duplicating device frame, scaling, theme mode, or component rendering rules.

Recommendation for the other thread:

1. Keep the payload extension idea.
2. Keep the concept that component class design preview should be web-rendered.
3. Rework the implementation so design preview calls the same runtime/resolver routines as normal preview, instead of creating another parallel resolver.

## Local HexColor picker change and bug

File touched:

```text
spikes/desktop-editor-shell/EditorShell/DictionaryFieldControl.cs
```

What changed:

- previous inline `ColorPicker` for `ValueKind.HexColor` was replaced by:
  - swatch;
  - hex textbox;
  - `Pick` button;
  - modal `Window` containing `ColorView`.

Observed bug:

- clicking `Pick` in Palette opens a blank/white popup with only action buttons visible;
- the color picker itself is not visible/usable.

Important classification:

- this is a dictionary-control bug for `ValueKind.HexColor`;
- it is not a Palette editor bug;
- do not fix it locally in Palette.

Likely directions:

- verify Avalonia ColorPicker package styles/resources are loaded correctly;
- use the correct Avalonia `ColorPicker` / `ColorView` template setup;
- or replace this with a Suki-compatible modal/picker implementation owned entirely by the dictionary control.

## Component Classes: editor-by-editor comparison

Source of truth for old React/Electron component editor:

```text
src/debug-ui/editors/ComponentClassRecordEditor.tsx
src/domain/repository/fixtures/exampleDataset.ts
src/domain/resolvers/resolveChatScreen.ts
```

New Avalonia/Suki component class files:

```text
spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs
spikes/desktop-editor-shell/Data/SpikeDatabase.cs
src/desktop-preview/renderDesignPreviewHtml.tsx
```

## Generic dictionary/value-kind gaps

Before filling individual component fields, add or normalize these dictionary kinds:

```text
fontFamily
fontWeight
fontStyle
decimal
iconToken            // single icon token
iconTokenList / slots // list/slots when needed
themeRadiusToken     // if radii are theme tokens
surfaceStyle         // if style remains a grouped embedded type
```

Current `ValueKind` has:

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

Missing dictionary kinds are the main reason some component-class fields were omitted or represented incorrectly.

## Component type naming mismatch

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

New Avalonia spike currently uses mixed/new names in places:

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

- old resolver functions still query `button_icon`, `audio_message`, `video_message`, `text_input_bar`;
- new `component_type` names may not be found by existing runtime/resolver code unless explicitly mapped.

Recommendation:

- either preserve old runtime names in the database, or create one explicit normalization layer;
- do not let each editor invent its own component type naming.

## Avatar component discrepancies

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

- `defaultSize` did not exist in the old component class; size was mostly module/context driven.
- `cornerRadius` numeric became `cornerRadiusToken`.
- `shadowToken` is missing.
- `surfaceReliefEnabled` is represented as `style.reliefEnabled`, not the old field.

Decision needed:

- confirm whether avatar component should keep `defaultSize`;
- decide if radii are now always theme tokens or if component should still support numeric radius.

## Button Icon component discrepancies

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

Some of these may be intentionally covered by `style.*`, but the mapping is not explicit enough yet.

## Label component discrepancies

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

- `width`/`height` became `size` pair. This is probably a good improvement.
- `paddingX`/`paddingY` became `padding` pair. This is probably a good improvement.
- `fontSize` became `textSize`.
- `sizingMode` became `dimensionMode`.
- border/corner/shadow/relief moved conceptually into `style.*`.

Recommendation:

- keep pair controls if desired;
- add missing typography fields through dictionary kinds;
- explicitly map old names to new config shape if old resolver is reused.

## Audio component discrepancies

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

- `width`/`height` became `size` pair. This is fine if resolver supports it.
- `progressKnobSize` became `knobSize`.
- old style fields are partly under `style.*`.

Recommendation:

- add the missing audio-specific fields;
- keep `size` pair if preferred, but map it to `width`/`height` before runtime if the existing renderer expects old names.

## Video component discrepancies

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

Recommendation:

- decide whether video status is a single icon or slots;
- if slots are kept, runtime adapter must convert old/new shape deliberately;
- add missing sizing/color fields.

## Text Input Bar component discrepancies

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
- `height` was added in the new version.

Recommendation:

- add icon slots for idle/typing left/right, likely using a generic icon-slot dictionary control;
- add field appearance fields or map them to `style.*` if that is the chosen unified model.

## Keyboard component discrepancies

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

Bug / runtime mismatch:

- special key icons are not shown in design preview;
- old renderer generated those SVG/glyphs internally from keyboard roles, not from ordinary icon tokens;
- do not replace them with icon-theme tokens unless that is a deliberate product decision.

Recommendation:

- add missing typography fields first by adding dictionary value kinds;
- restore `pressedEffect` option names or map them explicitly;
- use the existing keyboard resolver/layout path for special keys.

## Palette picker / color controls notes

Observed:

- palette pair selectors work but look grey and not fully Suki-native;
- text became readable after prior fixes, but the control still does not look like Suki documentation examples.

Classification:

- this belongs to dictionary control implementation for `PaletteColorToken` / `PaletteColorPair`;
- not an Actor-specific or Theme-specific issue.

Recommendation:

- once the other thread reaches dictionary controls, replace local ComboBox styling with a clean standard Suki/Avalonia ComboBox or a custom picker modal owned by the value kind;
- do not patch individual editors.

## Recommended order for the other refactor thread

1. Keep `MainWindow` shell-only before adding more editor features.
2. Add/normalize missing dictionary value kinds:
   - decimal;
   - font family;
   - font weight;
   - font style;
   - single icon token;
   - icon token list/slots;
   - semantic theme token variants if needed.
3. Fix `HexColor` picker at dictionary-control level.
4. Normalize component type naming.
5. Complete `ComponentClassFieldCatalog` against the old editor, component by component.
6. Only then wire component design preview, preferably through the same web resolver/render path as normal preview.
7. Avoid hand-fixing component preview scale/theme/color logic in Avalonia or in a second ad hoc TS resolver.

## What not to copy forward blindly

Do not copy these local changes as-is without review:

- the blank `ColorView` modal implementation;
- large standalone component preview resolver logic in `renderDesignPreviewHtml.tsx`;
- component type renames unless resolver/runtime is updated systematically;
- field construction still present in `MainWindow.axaml.cs`.

## What is worth rescuing

The following ideas are aligned with the target architecture:

- explicit `editor_modernization_roadmap.md`;
- `RecordClassFieldCatalog` as first small step toward removing field definitions from `MainWindow`;
- `DesignPreviewPayload` carrying component-class context;
- web-rendered design preview rather than Avalonia-drawn component preview;
- dictionary-level handling for color/icon/theme/font controls instead of editor-specific controls.

