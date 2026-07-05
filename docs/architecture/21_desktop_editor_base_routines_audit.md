# Desktop editor base routines audit

Status: active cleanup checklist for `codex/editor-modernization-rules`.

## Rule

Reusable routines must live in common/base code before they are reused by
editors, repositories, preview bridges, renderers, importers or dialogs.

Local helpers are allowed only when the behavior is truly local to that class.
If the same algorithm appears in a second place, extract it before continuing.

## Extracted in this phase

- `Common/SvgMarkupNormalizer.cs`
  - tintable SVG normalization;
  - XML/doctype stripping for inline SVG;
  - root SVG sizing normalization.
- `Common/SvgReplacementService.cs`
  - SVG validation;
  - replacement modal geometry parsing;
  - positive/negative SVG transform generation;
  - paint normalization for icon replacement.
- `Common/GeneratedSvgPrimitives.cs`
  - generated status signal SVG;
  - generated status battery SVG;
  - generated navigation button SVG.
- `Common/ThemeColorTokenCatalog.cs`
  - shared theme color token path catalog;
  - alpha paths for status/navigation backgrounds.
- `Common/PaletteAlphaPair.cs`
  - `color|color||alpha|alpha` parsing;
  - alpha clamping, formatting and slider snapping.

## High-priority remaining extractions

### JSON path helpers

Current locations:

- `Data/SpikeDatabase.cs`
- `Preview/Bridges/DesignPreviewToVisualIrBridge.cs`
- `Preview/Resolvers/DesignPreviewFrameResolver.cs`
- `Data/SpikeDatabase.IconThemeSearch.cs`
- `EditorShell/IconSlotsControl.cs`

Target:

- `Common/JsonPath.cs`

Move:

- safe object parsing;
- path read for string/number/bool;
- path write;
- number node creation;
- merge-missing behavior if it remains generic.

Do not move record-specific default JSON builders.

### Color and hex helpers

Current locations:

- `Data/SpikeDatabase.Palette.cs`
- `EditorShell/DictionaryFieldColorValue.cs`
- `EditorShell/HexColorPickerDialog.cs`
- `EditorShell/EditorNavigationVisuals.cs`
- `EditorShell/ActorAvatarPreviewFactory.cs`
- `EditorShell/PaletteColorPickerDialog.cs`
- `EditorShell/ThemeTokenPickerDialog.cs`
- `Preview/Avalonia/AvaloniaVisualIrDebugRenderer.cs`
- `Preview/Bridges/DesignPreviewToVisualIrBridge.cs`

Target:

- `Common/ColorValue.cs`
- `VisualIr/VisualIrColorResolver.cs` for IR-specific variant selection.

Move:

- hex normalization;
- safe hex parsing;
- alpha application;
- contrast text brush calculation;
- `#RRGGBBAA` parsing.

Do not move Suki/Avalonia theme resource assignment into common base routines.

### Numeric parsing, formatting and step rules

Current locations:

- `EditorShell/DictionaryIntegerControl.cs`
- `EditorShell/DictionaryDecimalControl.cs`
- `EditorShell/HueDegreesControl.cs`
- `EditorShell/DictionaryImageFileControl.cs`
- `EditorShell/ActorAvatarPreviewFactory.cs`
- `EditorShell/IconThemeSvgReplaceDialog.cs`
- `Preview/Resolvers/DesignPreviewFrameResolver.cs`
- `Data/SpikeDatabase.cs`

Target:

- `Common/NumericText.cs`
- `Common/SliderStepRules.cs`

Move:

- invariant decimal parsing;
- clamp helpers;
- stable numeric formatting;
- slider snap step derived from semantic scale.

### Boolean text conversion

Current locations:

- `Data/SpikeDatabase.cs`
- `EditorShell/DictionaryBooleanControl.cs`
- `EditorShell/StatusBarItemsCollectionEditor.cs`
- `EditorShell/RecordClassFieldValueService.cs`

Target:

- `Common/BooleanText.cs`

Move:

- tolerant string-to-bool;
- stable bool-to-storage string.

### Project/media path helpers

Current locations:

- `EditorShell/MediaPathService.cs`
- `EditorShell/EditorAddChildWorkflow.cs`
- `Data/SpikeDatabase.cs`
- `Data/SpikeDatabase.IconThemeAssets.cs`
- `Data/SpikeDatabase.IconThemes.cs`
- `Preview/Resolvers/DesignPreviewFrameResolver.cs`

Target:

- `Common/ProjectPathService.cs`

Move:

- project-relative path normalization;
- media-root absolute path resolution;
- safe combine inside project/media roots.

Keep database table-specific queries in repositories.

### Search text normalization

Current locations:

- `EditorShell/EditorSearchMatcher.cs`
- `EditorShell/IconThemeSearchDialog.cs`
- `Data/SpikeDatabase.IconThemeSearch.cs`

Target:

- `Common/SearchText.cs`

Move:

- lowercase/diacritic-insensitive token normalization;
- token/category derivation if reused by icon/theme searches.

Provider-specific API parsing stays inside the provider.

### Device metric normalization

Current locations:

- `Preview/Resolvers/DesignPreviewFrameResolver.cs`
- `EditorShell/DeviceImportMapper.cs`
- `EditorShell/PhoneSpecsDeviceCatalogProvider.cs`
- `Data/SpikeDatabase.Devices.cs`

Target:

- `Common/DeviceMetricRules.cs`

Move:

- design-unit conversion from pixel metrics;
- default scale guesses;
- sane metric fallbacks.

Do not move provider-specific raw field extraction from external APIs.

### Icon theme mapping helpers

Current locations:

- `Data/SpikeDatabase.IconThemes.cs`
- `EditorShell/IconThemeSearchDialog.cs`
- `EditorShell/IconThemeTokensCollectionEditor.cs`
- `EditorShell/IconTokenPickerDialog.cs`
- `EditorShell/EditorIcons.cs`

Target:

- `Common/IconTokenRules.cs`

Move:

- token/category derivation;
- mapping token list parsing;
- SVG token set discovery when it is filesystem-only and DB-free.

Repository writes and UI row construction stay where they are.

## Visual IR-specific cleanup

Keep Visual IR base routines in `VisualIr/` when they are part of the IR
contract rather than editor-wide utilities.

Candidates:

- variant color selection;
- IR color validation;
- static/variant color serialization;
- renderer-independent IR diagnostics.

Do not put component names, database concepts, theme ids or editor field ids in
`VisualIr/`.

## Not generic

Do not extract these just because they are helper methods:

- status/nav/component layout decisions inside transitional resolvers;
- editor card composition;
- modal row layout;
- Suki-specific control styling;
- provider-specific phone catalog parsing;
- repository SQL and table-specific business rules.

They can be moved later only if a second owner appears.

## Suggested order

1. JSON path helpers.
2. Color/hex helpers and IR color resolver.
3. Numeric/boolean text helpers.
4. Project/media path helpers.
5. Search text and icon token rules.
6. Device metric rules.

Each step should compile and keep the app usable before moving to the next.
