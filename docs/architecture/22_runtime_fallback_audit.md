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
  `textSizeToken` is required and must resolve through the web bridge.
- `component.label` style radius no longer falls back to zero. The
  `cornerRadiusToken` must exist and resolve.
- Status/navigation design previews moved to the web resolver/bridge path and
  no longer keep a parallel Visual IR resolver.
- The desktop Visual IR spike has been removed. The web preview is the source of
  truth for design preview rendering.
- `component.avatar` now renders through the component resolver/web bridge path.
  Its embedded `component.label` is resolved from the label base class plus
  slot-local overrides; it is not duplicated into the avatar class.
- Embedded component override highlighting now tracks stored override presence,
  not effective-value differences against the base class.
- The design preview surface no longer invents a plausible light/dark
  background color when the theme background token is missing. Missing
  background now fails visibly.

## Component Preview Boundary Audit

Active component class preview routes:

| Component | Status | Route |
| --- | --- | --- |
| `component.label` | migrated | `resolveLabelComponent` -> `labelComponentToRenderable` -> web renderer |
| `component.avatar` | migrated | `resolveAvatarComponent` -> `avatarComponentToRenderable` -> embedded label bridge |
| other component classes | blocked intentionally | `component_preview_unsupported` with `debug_red` |

System bar preview routes:

| Preview | Status | Route |
| --- | --- | --- |
| status bar | migrated for desktop preview | `resolveStatusBar` -> normalized status atoms -> `statusBarToRenderable` -> web renderer |
| navigation bar | migrated for desktop preview | `resolveNavigationBar` -> normalized navigation atoms -> `navigationBarToRenderable` -> web renderer |

Status/navigation no longer call the old web atomic modules from desktop
preview. Their resolvers own item visibility, zone assignment and ordering. The
bridge owns token/color/icon resolution, pixel scaling and final renderable node
creation.

No active desktop preview path may use:

- Visual IR;
- Avalonia duplicate rendering;
- `message_bubble_*` render nodes for migrated component classes;
- raw component config reads inside the web renderer;
- editor inheritance or override logic inside the web renderer.

## Boundary Watch List

- `renderDesignPreviewHtml.tsx` should remain a dispatcher. It may select the
  component resolver by type and wrap the result in the preview surface, but it
  must not grow component-specific field logic.
- The old central `webPreviewBridge.ts` path has been removed. Component and
  system-bar preview paths should stay behind explicit resolver/renderable
  modules plus a registry, not move back into a shared bridge.
- `MainWindow.axaml.cs` still hosts the generic embedded-editor navigation and
  card rebuilding. This is acceptable only while it remains generic shell
  orchestration. Component-specific embedded behavior belongs in
  `EmbeddedComponentSlotCatalog`, field catalogs, field value services and
  resolvers.
- `systemBarPreviewResolver.ts` uses optional item fields for label/token/value
  because those collection rows allow item-type-dependent payloads. Keep that
  limited to collection normalization; required layout/style data should stay
  strict.

## Needs Follow-Up

- Device metric parsing still has defensive defaults in `DeviceMetricRules`.
  These should be audited separately when imported devices become part of the
  trusted data contract.
- The old runtime visual modules under `src/visual/modules/**` have been
  removed from this repository. If chat runtime behavior is needed later, use
  the previous React repository as visual reference and recreate the component
  graph through the current component/preset/resolver/renderable route.
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
6. If a temporary fallback is explicitly approved, it must go through
   `RuntimeValueGuard.UseFallback` so the shell can surface a warning instead of
   hiding the decision in a field-specific branch.
