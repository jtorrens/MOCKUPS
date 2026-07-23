# Authentication inputs

Status: functional System components on the component resolver -> renderable
route.

Sources of truth:

- `src/desktop-preview/fingerprintComponentContract.ts`;
- `src/desktop-preview/faceRecognitionComponentContract.ts`;
- `src/desktop-preview/drawPasswordComponentContract.ts`;
- their sibling resolver and renderable modules.

## Fingerprint and Face Recognition

Both components receive only the resolved `state` (`initial`, `active`,
`correct`, `incorrect`) and normalized `progress` for the requested frame.
Their Variants own size, icon, an icon-only size multiplier, geometry and state colors. The
multiplier scales the token-derived icon without changing the component frame. Fingerprint paints a
deterministic scan line over its icon. Face Recognition paints a deterministic
scan line inside its framing marks. Neither component owns a timer, credential
comparison or duration formula.

## Draw Password

Draw Password receives an explicit digit pattern, visible-node count and
resolved state. Its Variant owns grid dimensions, node size, tokenized row and
column gaps, line width and state colors. Pattern digits are one-based grid
node numbers, must be available in the configured grid and cannot repeat.
Rendering emits only generic paths and surfaces.

## Composition boundary

Password explicitly embeds these three System components. Password owns
credential validation, comparison, BehaviorTiming and frame distribution; the
selected child owns only its visual composition. The bridge, generic renderer
and `MainWindow` contain no authentication-mode rules.
