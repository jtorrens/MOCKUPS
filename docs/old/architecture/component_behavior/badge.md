# Badge

Badge is a reusable Atom that overlays a compact circular status marker on the frame supplied by its parent component.

## Contract

- Runtime `contentMode` selects `icon` or `text`; the corresponding runtime value is `iconToken` or `text`.
- Runtime `size` is the explicit circular diameter. The resolver never infers or falls back to a size from the selected content.
- Runtime `backgroundPaletteColor` and `contentPaletteColor` reference project palette colors directly. The latter colors both icon and text.
- Variant `paddingToken` uses `theme.spacing.*` and defines the icon's inner content area inside the explicit diameter. Variant `textTypography` defines text presentation.
- `placement` is the generic alignment placement relative to the parent frame. A parent such as Avatar may own an explicit slot placement override because placement belongs to the composition boundary. Badge never expands or reflows that frame.
- Only the value selected by `contentMode` is required by the resolver.

Badge has no visibility field. The parent owns a runtime `showBadge` input and decides whether to resolve the embedded Badge slot. Button and Avatar expose explicit runtime content, size and palette colors. At a composition boundary such as Notification these child runtime inputs enter as Variant values and can be forwarded individually through the generic Component Input Bindings control. Notifications derives the text deterministically from its resolved collection length while keeping size and colors explicit in its Variant data.

All composition remains in the owning component resolver/renderable. The bridge and web renderer receive only resolved generic surface, icon and text nodes.
