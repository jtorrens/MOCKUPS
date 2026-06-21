# Codex Response 0009A — Minimal Remotion proof of concept

## Summary

Added a minimal Remotion 4.0.481 proof of concept with a `ChatScreenPreview` composition. Each Remotion frame maps to the existing example shot timeline, then uses the in-memory repository, resolvers, renderer-agnostic visual modules/layout, and validated `RenderableNode` tree before the React adapter renders it.

## Files changed

- `package.json`
- `package-lock.json`
- `tsconfig.json`
- `remotion.config.ts`
- `src/remotion/index.ts`
- `src/remotion/Root.tsx`
- `src/remotion/ChatScreenPreview.tsx`
- `src/remotion/buildRenderableForFrame.ts`
- `src/remotion/RemotionRenderableAdapter.tsx`
- `PROJECT_STATUS.md`
- `docs/exchange/responses/0009A_remotion_poc_response.md`

## Questions / conflicts

None. Remotion is an adapter/view over the current renderable tree and does not query repositories, resolve domain data, or replace visual modules.

## Tests

- `npm test` — passed, including typecheck and all example/resolver/visual validation.
- `npm run remotion:check` — passed; lists `ChatScreenPreview`, 25 fps, 1290×2796, 100 frames / 4 seconds.
- `remotion still ... --frame=60` — rendered and visually inspected successfully; confirmed status bar, header, avatars, incoming/outgoing bubbles, and write-on text. A duplicate-text issue found in the first still was corrected and the still was rerendered.
- `npm audit` via install — no vulnerabilities reported.

## Preview / run commands

```bash
npm run remotion:studio
npm run remotion:preview
npm run remotion:check
```

Open the `ChatScreenPreview` composition in Studio. Its 100 Remotion frames map from shot frame 150 at 30 fps into a 25 fps, four-second composition, covering chat entry, partial write-on, and completed text.

For a temporary still:

```bash
./node_modules/.bin/remotion still src/remotion/index.ts ChatScreenPreview /tmp/mockups-remotion-poc.png --frame=60
```

## Notes

Remotion consumes only the `RenderableNode` tree produced by the existing pipeline. The bridge owns frame-rate/timeline mapping; the adapter owns only DOM/React translation.

Visual limitations: placeholder initials are used instead of missing avatar files; status icons use plain text glyphs; no device chrome, SF Symbols, advanced typography, blur, tail paths, or pixel-perfect iOS styling is attempted. Current font/layout tokens mix logical sizes with a 3× physical viewport, so typography appears underscaled in the PoC; this should be normalized in the renderer-agnostic metric contract rather than patched with duplicate adapter layout logic.

Currently unsupported or partial renderable fields: transform translation/scale/rotation (only opacity is applied), asset rendering beyond avatar placeholders, tail geometry paths, arbitrary style keys, and most metadata. Unknown node types degrade to generic positioned containers instead of failing.

Shortcuts used for visibility: one composition, one fixture shot, fixed deterministic frame mapping, CSS-only basic shapes, placeholder avatars, and recursive generic nodes. A formal renderer-adapter capability contract may be useful once a second adapter or more node types exist; it is intentionally not introduced for this PoC.

Remotion and `@remotion/cli` are pinned to 4.0.481. Zod was aligned from 4.4.3 to the exact 4.3.6 required by this Remotion release. The NodeNext `.js` import convention is preserved through a Remotion-only Webpack `extensionAlias`.

No Electron, SQLite, migration, persistence, editor UI, final image/video export pipeline, device-frame implementation, advanced animation system, lock-screen renderer, or asset pipeline was added.
