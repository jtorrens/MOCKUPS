# Runtime fallback audit

Date: 2026-07-05

This audit records places where the desktop editor spike still uses fallback
values. The rule is:

- current-model data must be created by seed, default normalization or
  migration;
- resolvers must not hide missing required data with plausible development
  defaults;
- defensive renderer fallbacks must be visually obvious, usually `debug_red` or
  an unsupported placeholder.

## Allowed

- Seed/default construction in `SpikeDatabase.*`.
  These are the source of current-model defaults, not runtime compatibility.
- Migration/default merge routines such as `EnsureThemeTokens` and
  `EnsureComponentClassConfigDefaults`.
  These exist so runtime code can be strict afterwards.
- Dictionary-control parse fallbacks while a user is typing.
  These are editing safeguards, not final resolver behavior.
- Diagnostic color fallback `#ff00ff` / `debug_red`.
  This is acceptable only when the result is visibly broken.

## Fixed in this pass

- `component.label` preview no longer falls back to plausible background/text colors.
  `backgroundColorToken` and `textColorToken` are required.
- `component.label` preview no longer falls back to a plausible text size number.
  `textSizeToken` is required and must resolve through generic preview helpers.
- `component.label` style radius no longer falls back to zero. The
  `cornerRadiusToken` must exist and resolve.
- Status/navigation design previews moved to the resolver/renderable/helper path
  and no longer keep a parallel Visual IR resolver.
- The desktop Visual IR spike has been removed. The web preview is the source of
  truth for design preview rendering.
- `component.avatar` now renders through the component resolver/renderable/helper
  path.
  Its embedded `component.label` is resolved from the label base class plus
  slot-local overrides; it is not duplicated into the avatar class.
- Embedded component override highlighting now tracks stored override presence,
  not effective-value differences against the base class.
- The design preview surface no longer invents a plausible light/dark
  background color when the theme background token is missing. Missing
  background now fails visibly.
- Device preview metrics no longer fall back to plausible `1080x1920`, `0`
  insets or derived scales during preview rendering. Device seeds/imports and
  DB initialization normalization must create complete metrics first; preview
  reads fail visibly when required metric paths are missing or non-numeric.

## Component Preview Boundary Audit

Active component class preview routes:

| Component | Status | Route |
| --- | --- | --- |
| `surface` | structurally migrated | `resolveSurfaceComponent` -> `surfaceComponentToRenderable` -> web renderer |
| `label` | migrated | `resolveLabelComponent` -> `labelComponentToRenderable` -> web renderer |
| `avatar` | migrated | `resolveAvatarComponent` -> `avatarComponentToRenderable` -> embedded label renderable |
| `buttonIcon` | migrated | `resolveButtonIconComponent` -> `buttonIconComponentToRenderable` -> embedded surface/label renderables |
| `audio` | migrated/evolving | `resolveAudioComponent` -> `audioComponentToRenderable` -> embedded surface/avatar/button icon renderables |
| `iconRow` | structurally migrated | `resolveIconRowComponent` -> `iconRowComponentToRenderable` -> embedded button icon renderables |
| `iconBar` | structurally migrated | `resolveIconBarComponent` -> `iconBarComponentToRenderable` -> embedded icon row renderables |
| `status_bar` | migrated | `resolveStatusBarComponent` -> `statusBarComponentToRenderable` -> web renderer |
| `navigation_bar` | migrated | `resolveNavigationBarComponent` -> `navigationBarComponentToRenderable` -> web renderer |
| `textInputBar` | structurally migrated | `resolveTextInputBarComponent` -> `textInputBarComponentToRenderable` -> web renderer |
| `keyboard` | structurally migrated | `resolveKeyboardComponent` -> `keyboardComponentToRenderable` -> web renderer |
| `media` | structurally migrated | `resolveMediaComponent` -> `mediaComponentToRenderable` -> embedded surface/icon bar renderables |

Known component classes must route through the manifest and
`componentClassRenderableRegistry.ts`. The magenta unsupported surface is only a
defensive result for an unknown or unrouted component type, not a normal
migration state for seeded desktop component classes.

Status/navigation no longer call the old web atomic modules from desktop
preview. Their resolvers own item visibility, zone assignment and ordering. The
component renderable modules use only common preview helpers for token/color/icon
resolution, pixel scaling and final renderable node creation. There is no shared
`systemBar*` contract, resolver or renderable route.

No active desktop preview path may use:

- Visual IR;
- Avalonia duplicate rendering;
- `message_bubble_*` render nodes for migrated component classes;
- raw component config reads inside the web renderer;
- editor inheritance or override logic inside the web renderer.

## Boundary Watch List

- `renderDesignPreviewHtml.tsx` should remain a generic render host. Component
  routing belongs in `componentClassRenderableRegistry.ts`, and concrete
  resolver/renderable work belongs in each component module.
- The old central `webPreviewBridge.ts` path has been removed. Component and
  category `system` component preview paths should stay behind explicit
  resolver/renderable modules plus a registry, not move back into a shared
  component-aware bridge.
- `MainWindow.axaml.cs` still hosts the generic embedded-editor navigation and
  card rebuilding. This is acceptable only while it remains generic shell
  orchestration. Component-specific embedded behavior belongs in
  `EmbeddedComponentSlotCatalog`, field catalogs, field value services and
  resolvers.
- Status/navigation collection rows allow item-type-dependent payloads for
  label/token/value. Keep that limited to each owning component resolver;
  required layout/style data should stay strict.

## Needs Follow-Up

- Device import and DB initialization still use heuristics to create or complete
  `metrics_json` from external/legacy data. That is accepted as normalization,
  not as preview/runtime fallback.
- The old runtime visual modules under `src/visual/modules/**` have been
  removed from this repository. If chat runtime behavior is needed later, use
  the previous React repository as visual reference and recreate the component
  graph through the current component/Variant/resolver/renderable route.
- Message bubble migration must be all-owned-subcomponents-at-once, following
  `docs/architecture/23_embedded_component_composition_contract.md`.

## Rule For New Work

When adding a new component/editor path:

1. Add or migrate required data first.
2. Use required accessors in the resolver.
3. If a value is missing, fail visibly.
4. Use `debug_red` or unsupported placeholders only for defensive rendering.
5. Do not pass plausible colors, sizes, radius, spacing, labels or layout values
   as runtime fallbacks.
6. Temporary development fallbacks are not part of the active architecture. If
   one is explicitly approved for a short-lived diagnostic, it must surface as a
   preview/editor warning through the generic messages path and be removed in
   the same cleanup phase.
