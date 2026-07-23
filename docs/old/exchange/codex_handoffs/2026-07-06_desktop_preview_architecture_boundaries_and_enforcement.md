# Desktop preview architecture boundaries and enforcement

Date: 2026-07-06  
Branch: `codex/editor-modernization-rules`  
Relevant commits:

- `80025c7 Remove central design preview bridge`
- `ac7331f Add desktop preview architecture guardrails`

This document summarizes the current desktop preview architecture, the rules we want to preserve, and the enforcement script that prevents the preview/render path from drifting back into a central component-aware bridge.

It is written for external review. Please look specifically for inconsistent responsibilities, hidden coupling, naming that could mislead future contributors, or missing enforcement rules.

## 1. Main architectural goal

The desktop editor should edit data and request a preview frame. It should not duplicate final rendering logic in Avalonia.

The web preview is the source of visual truth for design preview. The desktop app sends a complete frame request. The preview renderer paints that frame. It must not infer device semantics or component semantics that were not supplied by the resolver/renderable layer.

Target direction:

```text
Avalonia editor
  -> preview payload JSON
  -> design preview registry
  -> component/system resolver
  -> component/system renderable module
  -> common preview helpers
  -> generic web renderable tree
  -> generic React renderable adapter
  -> HTML shown in WebView
```

The important boundary is:

```text
component-specific knowledge stays in the component module
generic rendering stays generic
registry only routes
```

## 2. Current file layout

Current relevant files under `src/desktop-preview`:

```text
renderDesignPreviewHtml.tsx
designPreviewPayload.ts
designPreviewRenderableRegistry.ts
componentClassRenderableRegistry.ts

componentRenderableCommon.ts
componentResolverCommon.ts
componentPreviewDefaults.ts

labelComponentContract.ts
labelComponentResolver.ts
labelComponentRenderable.ts

avatarComponentContract.ts
avatarComponentResolver.ts
avatarComponentRenderable.ts

buttonIconComponentContract.ts
buttonIconComponentResolver.ts
buttonIconComponentRenderable.ts

audioComponentContract.ts
audioComponentResolver.ts
audioComponentRenderable.ts

systemBarPreviewResolver.ts
systemBarRenderables.ts
```

There is no longer a `webPreviewBridge.ts`. That file is intentionally forbidden.

## 3. Layer responsibilities

### 3.1 Avalonia host

Main file:

- `spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs`

Responsibilities:

- collect the selected preview payload;
- pass theme mode, marks flag, theme tokens, palette colors, icon mappings and media roots;
- pass frame geometry:
  - `canvasWidth`
  - `canvasHeight`
  - `screenX`
  - `screenY`
  - `screenWidth`
  - `screenHeight`
  - `scaleToPixels`
- invoke `src/desktop-preview/renderDesignPreviewHtml.tsx`;
- return generated HTML to the WebView.

Explicit non-responsibilities:

- no component layout;
- no component-specific defaults;
- no SVG/icon interpretation beyond passing roots/mappings;
- no status bar or navigation bar metric injection.

Important recent correction:

`statusBarHeight` and `safeAreaBottom` were removed from the desktop preview payload. A preview frame receives frame dimensions only. If status bar or navigation bar need height/layout, that belongs to their own resolver/contract, not to global device metrics.

### 3.2 `renderDesignPreviewHtml.tsx`

Responsibilities:

- read the JSON payload path passed by Avalonia;
- ask `designPreviewPayloadToRenderable(payload)` for a child renderable;
- wrap the child in a generic `design_preview_surface`;
- resolve only the surface background color for the whole preview surface;
- validate the renderable tree with `RenderableNodeSchema`;
- render using `RenderableReactAdapter`.

Explicit non-responsibilities:

- no knowledge of `label`, `avatar`, `buttonIcon`, `audio`, `statusBar`, `navigationBar`;
- no component imports;
- no component branch logic;
- no timers or animation logic;
- no inheritance/override logic;
- no DB/editor knowledge.

The architecture check forbids component names in this file.

### 3.3 `designPreviewRenderableRegistry.ts`

Responsibilities:

- route by top-level preview kind:
  - `componentClass`
  - `statusBar`
  - `navigationBar`
- call the correct top-level resolver/renderable entry point.

Allowed knowledge:

- top-level preview kinds;
- which module owns each kind.

Forbidden knowledge:

- layout decisions;
- token resolution;
- defaults;
- renderable construction details.

Review question:

Status/navigation bars currently have their own top-level registry entries. We have been treating them conceptually like components, but they are still represented as top-level `kind` values in `DesignPreviewPayload`. This may be acceptable during migration, but it is worth reviewing whether system bars should later move into the same component registry pattern or remain distinct system preview types.

### 3.4 `componentClassRenderableRegistry.ts`

Responsibilities:

- route component class type to the owning module:
  - `label`
  - `avatar`
  - `buttonIcon`
  - `audio`
- call:
  - resolver for that component;
  - renderable for that component.

Allowed knowledge:

- component type names;
- module entry points.

Forbidden knowledge:

- component layout;
- style calculation;
- token resolution;
- embedded component composition rules;
- defaults.

This is intentionally the only central file allowed to name component classes for routing.

### 3.5 Component contract files

Examples:

- `labelComponentContract.ts`
- `avatarComponentContract.ts`
- `buttonIconComponentContract.ts`
- `audioComponentContract.ts`

Responsibilities:

- declare the already-resolved contract shared by resolver and renderable;
- avoid renderable importing types from resolver or resolver importing types from renderable.

Why this exists:

Initially renderables imported `LabelDesignContract` etc. from resolver files. The first architecture check failed on that. The fix was to split contracts into neutral files:

```text
labelComponentContract.ts
  used by:
    labelComponentResolver.ts
    labelComponentRenderable.ts
```

This keeps the contract explicit and removes resolver-renderable coupling.

### 3.6 Component resolver files

Examples:

- `labelComponentResolver.ts`
- `avatarComponentResolver.ts`
- `buttonIconComponentResolver.ts`
- `audioComponentResolver.ts`

Responsibilities:

- read `DesignPreviewPayload` and component config JSON;
- merge base config plus embedded-component overrides;
- validate required fields;
- fail visibly on missing required data;
- produce the component contract.

Resolver owns component semantics:

- which children exist;
- how embedded slots are assembled;
- what component fields mean;
- how declared component input state affects the component contract;
- frame-specific state for animation.

Examples:

- `labelComponentResolver.ts` owns `text`, `subtext`, `textGap`, `textAlign`.
- `avatarComponentResolver.ts` owns whether a label slot exists and how label overrides are merged.
- `audioComponentResolver.ts` owns waveform/playback fields and embedded avatar/badge slots.

Explicit non-responsibilities:

- no final HTML;
- no React;
- no generic renderer internals;
- no central routing.

Allowed embedded imports:

```text
avatar resolver -> label resolver
button icon resolver -> label resolver
audio resolver -> avatar resolver
audio resolver -> button icon resolver
```

These imports are allowed because those parent components explicitly own those child slots.

### 3.7 Component renderable files

Examples:

- `labelComponentRenderable.ts`
- `avatarComponentRenderable.ts`
- `buttonIconComponentRenderable.ts`
- `audioComponentRenderable.ts`

Responsibilities:

- convert a component contract into generic `RenderableNode` objects;
- use common helpers for:
  - token to color;
  - palette neutral tint;
  - alpha;
  - scale to pixels;
  - boxes and placement;
  - shadows;
  - icon masks;
- call child component renderables only when the parent has an explicit embedded slot.

Explicit non-responsibilities:

- no reading editor controls;
- no DB;
- no inheritance/override storage;
- no routing registry decisions;
- no web rendering implementation details beyond generic renderable nodes.

Allowed embedded imports:

```text
avatar renderable -> label renderable
button icon renderable -> label renderable
audio renderable -> avatar renderable
audio renderable -> button icon renderable
```

### 3.8 Common preview helpers

Files:

- `componentRenderableCommon.ts`
- `componentResolverCommon.ts`

Responsibilities:

- generic token/color helpers;
- generic alpha/neutral tint helpers;
- generic numeric parsing and validation helpers;
- generic surface/shadow helpers;
- generic placement/box math;
- generic icon URI helper.

Explicit non-responsibilities:

- no concrete component names;
- no imports from concrete component resolvers/renderables;
- no component-specific field names like `waveform`, `badge`, `labelSlot`, etc.;
- no layout decisions that only apply to one component.

The architecture check forbids component terms in `componentRenderableCommon.ts`.

Review question:

`componentRenderableCommon.ts` currently includes `iconUriForToken`, which resolves an icon token to a data URI using `iconMappingJson`, `iconAssetRoot`, and filesystem reads. This is generic enough for now because any icon-bearing component can use it. However, it is worth reviewing whether this should eventually move to a more general asset helper module rather than living under "component renderable common".

### 3.9 Generic web renderer

Main file:

- `src/visual/adapters/react/RenderableReactAdapter.tsx`

Responsibilities:

- paint generic renderable nodes;
- support generic primitives and styles;
- render bounds/marks when requested.

Explicit non-responsibilities:

- no component class config;
- no theme token names;
- no palette token names;
- no database records;
- no inheritance/overrides;
- no component-specific layout rules;
- no timers or CSS animations for component behavior.

If a component needs a new visual primitive, the primitive must be generic and receive fully resolved data.

## 4. Animation rule

Animation is frame data.

The web preview/render layer must not run timers, CSS animations, countdowns, or component-specific interpolation.

Correct model:

```text
requested frame/time
  -> resolver computes component state for that frame
  -> renderable module emits resolved nodes for that frame
  -> web renderer paints that one frame
```

For example, audio play/pause/progress should be resolved before rendering the frame. The web renderer should not internally advance the audio animation.

## 5. Embedded component model

Embedded components are recursive component slots, not copied field groups.

Example:

```text
avatar
  owns label slot
  merges base label config + avatar-local label overrides
  calls label resolver
  calls label renderable
```

Example:

```text
audio
  owns avatar slot
  owns badge slot
  merges base avatar/buttonIcon config + audio-local overrides
  calls avatar/buttonIcon resolvers
  calls avatar/buttonIcon renderables
```

Important override rule:

An override exists because a value was consciously set on the embedded instance. It does not disappear automatically just because a future parent/base value becomes equal by coincidence. Restore/inherit must remove the override entry explicitly.

## 6. Current allowed dependency graph

Top-level:

```text
renderDesignPreviewHtml.tsx
  -> designPreviewRenderableRegistry.ts
  -> componentClassRenderableRegistry.ts
  -> component modules
```

Component registry:

```text
componentClassRenderableRegistry.ts
  -> label resolver/renderable
  -> avatar resolver/renderable
  -> buttonIcon resolver/renderable
  -> audio resolver/renderable
```

Allowed embedded component dependencies:

```text
avatar resolver/renderable
  -> label resolver/renderable

buttonIcon resolver/renderable
  -> label resolver/renderable

audio resolver/renderable
  -> avatar resolver/renderable
  -> buttonIcon resolver/renderable
```

Forbidden examples:

```text
renderDesignPreviewHtml.tsx -> labelComponentResolver.ts
componentRenderableCommon.ts -> audioComponentRenderable.ts
componentResolverCommon.ts -> avatarComponentResolver.ts
labelComponentRenderable.ts -> avatarComponentRenderable.ts
systemBarRenderables.ts -> audioComponentRenderable.ts
```

## 7. Enforcement script

Script:

```text
scripts/checkDesktopPreviewArchitecture.ts
```

Package script:

```json
"check:architecture": "tsx scripts/checkDesktopPreviewArchitecture.ts"
```

`npm test` now includes:

```text
npm run typecheck
npm run check:architecture
npm run validate:examples
npm run validate:resolver
npm run validate:visual
npm run validate:sqlite
```

### 7.1 What the script checks

1. `src/desktop-preview/webPreviewBridge.ts` must not exist.

2. `renderDesignPreviewHtml.tsx` must not contain central preview terms:

```text
label
avatar
buttonIcon
audio
statusBar
navigationBar
component_label
component_avatar
component_button
component_audio
status_bar
navigation_bar
```

3. `componentRenderableCommon.ts` must not contain component terms:

```text
label
avatar
buttonIcon
audio
statusBar
navigationBar
waveform
badge
component_label
component_avatar
component_button
component_audio
```

4. Central/common files must not import concrete component or system bar modules:

```text
componentRenderableCommon.ts
componentResolverCommon.ts
designPreviewPayload.ts
renderDesignPreviewHtml.tsx
```

Forbidden import pattern:

```text
*ComponentResolver.js
*ComponentRenderable.js
systemBar*.js
```

5. Concrete component imports are allowed only in:

```text
componentClassRenderableRegistry.ts
designPreviewRenderableRegistry.ts
```

or when explicitly declared as an embedded-component dependency.

Current embedded allowlist:

```text
avatarComponentResolver.ts
  -> ./labelComponentResolver.js

avatarComponentRenderable.ts
  -> ./labelComponentRenderable.js

buttonIconComponentResolver.ts
  -> ./labelComponentResolver.js

buttonIconComponentRenderable.ts
  -> ./labelComponentRenderable.js

audioComponentResolver.ts
  -> ./avatarComponentResolver.js
  -> ./buttonIconComponentResolver.js

audioComponentRenderable.ts
  -> ./avatarComponentRenderable.js
  -> ./buttonIconComponentRenderable.js
```

### 7.2 What the script cannot prove

The script is intentionally simple and conservative. It does not prove semantic correctness.

Limitations:

- string checks are lexical;
- it checks current central files, not every possible future central file unless added to the script;
- it cannot detect a generic helper that is technically generic in naming but semantically tailored to one component;
- it does not inspect runtime behavior;
- it does not validate visual parity;
- it does not enforce folder structure beyond imports;
- it does not know whether `systemBarRenderables.ts` should eventually become component-like.

The script is a guardrail, not a replacement for architectural review.

## 8. How to add a new component correctly

Example: adding `video`.

1. Add contract:

```text
videoComponentContract.ts
```

2. Add resolver:

```text
videoComponentResolver.ts
```

Resolver may import:

- its contract;
- `DesignPreviewPayload`;
- resolver common helpers;
- declared child component resolvers if it owns embedded slots.

3. Add renderable:

```text
videoComponentRenderable.ts
```

Renderable may import:

- its contract;
- `DesignPreviewPayload`;
- `RenderableNode` types;
- renderable common helpers;
- declared child component renderables if it owns embedded slots.

4. Add routing only in:

```text
componentClassRenderableRegistry.ts
```

5. If it embeds another component, add that dependency to:

```text
scripts/checkDesktopPreviewArchitecture.ts
```

6. Run:

```text
npm run check:architecture
npm run typecheck
npm run validate:visual
dotnet build spikes/desktop-editor-shell/Mockups.DesktopEditorShell.csproj
```

## 9. Known migration status

Migrated to the new component route:

- Label
- Avatar
- Button Icon
- Audio

System bars:

- Status Bar
- Navigation Bar

Status/navigation bars have resolver/renderable modules and no central bridge. They are currently routed by top-level preview kind rather than through the component class registry.

Not yet fully migrated:

- Bubble and bubble-owned subcomponents;
- remaining legacy runtime chat/message render paths;
- future web preview batch/render pipeline.

Important rule for Bubble:

When Bubble is migrated, migrate Bubble and all owned subcomponents together. Do not partially migrate Bubble while leaving actor label, avatar, media, audio, video, icon button, tail/chrome, or status subcomponents on legacy render paths.

## 10. Review checklist for ChatGPT or another reviewer

Please review:

1. Does the layer split avoid circular or hidden dependencies?

2. Is `componentClassRenderableRegistry.ts` acceptable as the only central component name catalog, or should routing be data-driven/configured differently?

3. Should Status Bar and Navigation Bar become normal component classes, or remain top-level system preview kinds?

4. Is `componentRenderableCommon.ts` too broad? Should token/color/icon/geometry helpers be split into smaller modules?

5. Is resolving icon files inside renderable common acceptable, or should icon URI resolution happen earlier?

6. Should contracts be colocated next to resolver/renderable, or moved into a `contracts/` subfolder?

7. Is the current enforcement script too lexical and brittle? Should we use TypeScript AST/module graph analysis instead of string matching?

8. Are embedded component imports better modeled as an explicit registry/manifest rather than a hardcoded allowlist inside the script?

9. Should the script also enforce naming conventions:

```text
*ComponentContract.ts
*ComponentResolver.ts
*ComponentRenderable.ts
```

10. Should the script fail if a component renderable imports a sibling component contract without importing its renderable/resolver, or is that harmless?

11. Should `DesignPreviewPayload.device` be renamed to avoid implying actual device semantics now that it only carries frame geometry and scale?

12. Should generic renderable node `type` values like `component_label` be renamed to avoid component-specific node types in the generic renderer?

Resolved after review: yes. Migrated component renderable modules must emit generic primitive types such as `group`, `surface`, `text`, `avatar`, `icon_token`, and `waveform_bar`.

## 11. Resolved component node-type inconsistency

The architecture says the web renderer must paint generic primitives and not know component classes.

Earlier migrated component renderable nodes used type strings such as:

```text
component_label
component_avatar
component_button_icon
component_audio
component_audio_waveform_bar
```

Those names were removed from migrated component output and from `RenderableReactAdapter`. The agreed direction is:

Move toward generic renderable node types:

```text
surface
text
image
icon
group
bar
circle
path/svg
```

and use metadata only for debugging:

```text
metadata: { componentType: "label" }
```

The enforcement script now rejects those component-specific renderable node types if a component module tries to emit them again.

## 12. Current validation commands

Before closing preview/component migration phases, run:

```text
npm run check:architecture
npm run typecheck
npm run validate:visual
dotnet build spikes/desktop-editor-shell/Mockups.DesktopEditorShell.csproj
git diff --check
```

Current expected architecture check output:

```text
Desktop preview architecture boundaries validated.
```

Known unrelated build warning:

```text
SQLitePCLRaw.lib.e_sqlite3 2.1.11 has a known high severity vulnerability warning.
```
