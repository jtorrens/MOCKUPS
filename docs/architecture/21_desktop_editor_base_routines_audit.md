# Desktop editor base routines audit

Status: active cleanup checklist for `codex/editor-modernization-rules`.

## Rule

Reusable routines must live in common/base code before they are reused by
editors, repositories, preview bridges, renderers, importers or dialogs.

Local helpers are allowed only when the behavior is truly local to that class.
If the same algorithm appears in a second place, extract it before continuing.

## Extracted in this phase

- `Common/JsonPath.cs`
  - safe object parsing;
  - path read/write for strings, numbers, booleans and nodes;
  - number-node creation;
  - merge-missing behavior for generic JSON defaults.
- `Common/ColorValue.cs`
  - hex normalization;
  - safe hex parsing;
  - alpha application;
  - contrast text brush calculation;
  - `#RRGGBBAA` parsing.
- `Common/NumericText.cs`
  - invariant decimal parsing;
  - clamp helpers;
  - stable numeric formatting;
  - slider snap step derived from semantic scale.
- `Common/BooleanText.cs`
  - tolerant string-to-bool;
  - stable bool-to-storage string.
- `Common/ProjectPathService.cs`
  - project-relative path normalization;
  - media-root absolute path resolution;
  - safe combine inside project/media roots.
- `Common/SearchText.cs`
  - lowercase/diacritic-insensitive token normalization;
  - reusable whitespace tokenization.
- `Common/IconTokenRules.cs`
  - icon token derivation from text;
  - category derivation from icon tokens;
  - mapping token list parsing;
  - icon theme mapping/category JSON rebuilding;
  - SVG token set discovery when it is filesystem-only and DB-free.
- `Common/DeviceMetricRules.cs`
  - normalized device metrics JSON construction;
  - preview metric fallbacks;
  - scale-to-pixels resolution;
  - default scale guesses, including PPI-aware catalog guesses.
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

## Remaining extraction candidates

No high-priority duplicated base routine remains from this audit.

Keep repository writes, UI row construction and Suki-specific styling in their
owning classes unless a second non-UI owner appears.

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

Completed:

1. JSON path helpers.
2. Color/hex helpers and IR color resolver.
3. Numeric/boolean text helpers.
4. Project/media path helpers.
5. Search text and icon token rules.
6. Device metric rules.

Next low-risk pass:

1. Audit remaining private helpers with `rg` before extracting anything else.
2. Leave UI-specific row layout, repository writes and transitional component layout in their owning classes.
3. Treat any new reusable algorithm as common/shared before adding a second copy.

Each step should compile and keep the app usable before moving to the next.
