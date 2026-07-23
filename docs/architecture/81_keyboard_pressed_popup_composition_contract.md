# Keyboard Pressed Popup Composition Contract

Status: normative.

This contract governs the pressed-character popup produced by the Keyboard
component when its selected Variant uses the `popup` pressed effect.

## 1. Ownership

Keyboard owns the complete pressed-key geometry. Its component renderable
converts the resolved Keyboard frame, resolved key box and current pressed
character into generic path and text primitives. No bridge, HTML adapter, SVG
adapter or renderer may identify Keyboard or reconstruct its popup.

The current width and connector proportions are component-owned visual
semantics. They are not persisted fields, inferred Variant values or Theme
tokens. Changing them does not authorize a database fallback or migration.

## 2. One continuous elevated key

The popup is one continuous silhouette containing:

- a head whose target width is `1.3 ×` the resolved key width;
- a connector whose upper edge occupies the complete lower width of the head
  and narrows towards the pressed key;
- the pressed key's complete base box.

The pressed key's normal background, glyph and shadow are omitted while this
silhouette is active. The enlarged glyph appears once in the popup head. One
outer path owns fill, optional border and the single shared shadow; no seam,
horizontal shelf beside the connector, second key shadow or duplicated lower
glyph remains.

Keys without an active popup retain their existing background, glyph, border
and optional shadow.

## 3. Keyboard-frame containment

The head is centered over the key when that fits. At the left or right edge it
translates horizontally so its outer bounds never exceed the resolved Keyboard
frame. The connector remains aimed at the resolved key center, so the unified
shape becomes asymmetric near an edge without moving the key.

If the Keyboard frame is narrower than the target head, the head is capped at
the frame width. Containment uses only the already resolved Keyboard and key
boxes. It does not inspect key labels, kinds, ids, row/column indices or source
positions to choose a special case.

## 4. Preserved boundaries

- `pressedKey` remains the one explicit frame-resolved state;
- `pressedEffect` remains Variant configuration;
- Keyboard continues to own its rows, key weights and popup semantics;
- Conversation continues to own the Keyboard's screen placement and time;
- Theme tokens continue to resolve colors, typography, radius, border and
  shadow before the renderer;
- Preview still receives a complete resolved Renderable tree;
- generic renderers continue to paint only generic path and text primitives.

## 5. Enforcement

Automated tests verify centered width, full-width connector origin, continuous
extent through the base key, left/right frame containment, connector targeting
and narrow-frame capping.
Architecture checks require the component-owned geometry helper, the early
popup composition that replaces the normal key nodes and the absence of the
retired separate popup/tail append.

Manual review covers a centered character and the leftmost/rightmost character
at normal and compact Preview sizes.
