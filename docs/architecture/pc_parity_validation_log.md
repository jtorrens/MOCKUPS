# PC parity validation log

This file records Windows/PC parity validation runs. The PC environment is used for validation only; code fixes remain owned by the Mac development flow unless explicitly agreed otherwise.

## 2026-07-07 20:00:37 +02:00 - `codex/editor-modernization-rules` @ `48e561f`

### Environment

- Windows validation path: `D:\PROYECTOS\MOCKUPS`
- Branch: `codex/editor-modernization-rules`
- Commit: `48e561f Add desktop parity database and PC validation guide`
- Node.js: `v24.13.0`
- npm: `11.6.2`
- .NET SDK: `10.0.301 [C:\Program Files\dotnet\sdk]`
- WebView2 Runtime: installed, version `150.0.4078.48`
- `sqlite3` CLI: not installed

### Parity files

- `data/desktop-editor-spike.sqlite`: present
- `assets/FOQN_S2`: present
- `assets/system/system_icons`: present

### Passed checks

- `npm.cmd install`: passed after allowing network access for native dependency download.
- `npm.cmd run typecheck`: passed.
- `npm.cmd run validate:sqlite`: passed.
- `npm.cmd run app:check`: passed after installing `.NET SDK 10.0.301`.

Known non-blocking warning during `app:check`:

```text
NU1903: SQLitePCLRaw.lib.e_sqlite3 2.1.11 has a known high severity vulnerability.
```

This warning is documented in `docs/architecture/26_pc_parity_validation.md` as not being a PC parity failure by itself.

### Failed checks

`npm.cmd run check:architecture` failed.

Summary:

- Multiple component renderable/resolver imports are not declared as embedded-component dependencies.
- `previewAssetResolver.ts` and `renderDesignPreviewHtml.tsx` access filesystem APIs outside the allowed preview asset/request boundary helpers.
- Shared preview helper implementations are still local in preview helper files instead of imported from common helpers.

Full architecture check output:

```text
Desktop preview architecture check failed:
- src\desktop-preview\audioComponentRenderable.ts: concrete component import "./avatarComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\audioComponentRenderable.ts: concrete component import "./buttonIconComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\audioComponentRenderable.ts: concrete component import "./surfaceComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\audioComponentResolver.ts: concrete component import "./avatarComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\audioComponentResolver.ts: concrete component import "./buttonIconComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\audioComponentResolver.ts: concrete component import "./surfaceComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\avatarComponentRenderable.ts: concrete component import "./labelComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\avatarComponentResolver.ts: concrete component import "./labelComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\buttonIconComponentRenderable.ts: concrete component import "./labelComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\buttonIconComponentResolver.ts: concrete component import "./labelComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\buttonIconComponentResolver.ts: concrete component import "./surfaceComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./audioComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./audioComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./avatarComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./avatarComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./buttonIconComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./buttonIconComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./cursorComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./cursorComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./labelComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./labelComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./keyboardComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./keyboardComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./iconRowComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./iconRowComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./navigationBarComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./navigationBarComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./statusBarComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./statusBarComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./surfaceComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./surfaceComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./textBoxComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./textBoxComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./textInputBarComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./textInputBarComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./videoComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\componentClassRenderableRegistry.ts: concrete component import "./videoComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\iconRowComponentRenderable.ts: concrete component import "./buttonIconComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\iconRowComponentResolver.ts: concrete component import "./buttonIconComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\labelComponentRenderable.ts: concrete component import "./surfaceComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\labelComponentResolver.ts: concrete component import "./surfaceComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\previewAssetResolver.ts: filesystem access "node:fs" belongs in preview asset/request boundary helpers only
- src\desktop-preview\previewColorHelpers.ts: shared preview helper "applyNeutralTint" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewColorHelpers.ts: shared preview helper "colorForMode" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewColorHelpers.ts: shared preview helper "cssColorWithAlpha" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewColorHelpers.ts: shared preview helper "resolvePaletteColor" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewColorHelpers.ts: shared preview helper "selectedColor" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewColorHelpers.ts: shared preview helper "tokenValueForMode" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewColorHelpers.ts: shared preview helper "variants" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewGeometryHelpers.ts: shared preview helper "renderScale" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewJsonHelpers.ts: shared preview helper "asRecord" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewJsonHelpers.ts: shared preview helper "parseObject" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewValueHelpers.ts: shared preview helper "numberValue" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewValueHelpers.ts: shared preview helper "requiredAlpha" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewValueHelpers.ts: shared preview helper "requiredBoolean" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewValueHelpers.ts: shared preview helper "requiredNumber" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewValueHelpers.ts: shared preview helper "requiredNumberPair" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewValueHelpers.ts: shared preview helper "requiredNumberValue" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewValueHelpers.ts: shared preview helper "requiredPlacement" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewValueHelpers.ts: shared preview helper "requiredRecord" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewValueHelpers.ts: shared preview helper "requiredString" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewValueHelpers.ts: shared preview helper "resolveSurfaceStyle" must be imported from common helpers, not redefined locally
- src\desktop-preview\previewValueHelpers.ts: shared preview helper "stringValue" must be imported from common helpers, not redefined locally
- src\desktop-preview\renderDesignPreviewHtml.tsx: filesystem access "node:fs/promises" belongs in preview asset/request boundary helpers only
- src\desktop-preview\textBoxComponentRenderable.ts: concrete component import "./iconRowComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\textBoxComponentRenderable.ts: concrete component import "./surfaceComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\textBoxComponentResolver.ts: concrete component import "./cursorComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\textBoxComponentResolver.ts: concrete component import "./iconRowComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\textBoxComponentResolver.ts: concrete component import "./surfaceComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\textInputBarComponentRenderable.ts: concrete component import "./iconRowComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\textInputBarComponentRenderable.ts: concrete component import "./surfaceComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\textInputBarComponentRenderable.ts: concrete component import "./textBoxComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\textInputBarComponentResolver.ts: concrete component import "./iconRowComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\textInputBarComponentResolver.ts: concrete component import "./surfaceComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\textInputBarComponentResolver.ts: concrete component import "./textBoxComponentResolver.js" is not a declared embedded-component dependency
- src\desktop-preview\videoComponentRenderable.ts: concrete component import "./surfaceComponentRenderable.js" is not a declared embedded-component dependency
- src\desktop-preview\videoComponentResolver.ts: concrete component import "./surfaceComponentResolver.js" is not a declared embedded-component dependency
```

### Mac action required

- Fix the architecture violations in the Mac development environment.
- Re-run `npm run check:architecture` on Mac before pushing.
- After the branch is updated, pull on PC and append a new timestamped entry to this log.
