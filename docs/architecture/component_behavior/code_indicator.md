# Code Indicator

Status: functional atom on the component resolver -> renderable route.

Source of truth: `src/desktop-preview/codeIndicatorComponentContract.ts`,
`codeIndicatorComponentResolver.ts` and
`codeIndicatorComponentRenderable.ts`.

## Responsibility

Code Indicator paints an ordered row of anonymous glyphs used to represent a
fixed-size code. It does not know a password, digits, input order, comparison
result or time. A parent supplies the count, filled count and already resolved
state for the requested frame.

## Variant data

Each Variant owns a positive glyph size, a `theme.spacing.*` gap token and six
concrete Surface Variant slots: empty and filled for `initial`, `correct` and
`incorrect`. The Surface slots own fill, border, radius, alpha, relief and
shadow. This keeps the glyph shape extensible without adding visual rules to
the renderer.

## Runtime inputs

- `count`: positive number of positions;
- `filledCount`: integer from zero through `count`;
- `state`: `initial`, `correct` or `incorrect`.

Invalid counts or an unsupported state are resolver errors. The renderable
centres the row and emits only resolved Surface atoms.

## Composition boundary

Code Indicator explicitly embeds Surface. Password embeds Code Indicator as a
concrete Variant. No Code Indicator rule exists in the bridge, generic
renderer or `MainWindow`.
