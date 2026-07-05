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

## Needs Follow-Up

- Device metric parsing still has defensive defaults in `DeviceMetricRules`.
  These should be audited separately when imported devices become part of the
  trusted data contract.

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
