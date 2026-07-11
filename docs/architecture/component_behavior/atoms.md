# Atoms

Status: active atom set. Each atom is owned by its own contract/resolver/
renderable module and may be embedded only through declared parent dependency.

Source of truth: each matching `*ComponentContract.ts`,
`*ComponentResolver.ts` and `*ComponentRenderable.ts` under
`src/desktop-preview/`, plus `desktopPreviewComponents.ts` for declared
dependencies.

## Surface

**Purpose/ownership.** Generic rectangular visual container with background,
border, corner radius, alpha, shadow, relief and optional tail.

**Runtime inputs.** Required size pair (width/height) when a parent needs a
specific frame. **Variant/configuration.** Visual tokens, alpha, tail and
surface style.

**Layout.** The parent supplies the frame; Surface paints it and clips its own
contents/corners where requested. Tail follows the same visual surface. Shadow
and relief are generic style concerns, not parent-specific effects.

**States/motion.** No temporal state. **Limitations.** The rare combination of
large border + relief + tail is not fully visually tuned; ordinary borders are
supported.

## Cursor

**Purpose/ownership.** Generic text insertion cursor. **Runtime inputs.**
Height, visibility/phase/frame as supplied by text owner. **Variant/config.**
Color token, design width, minimum fade alpha and fade speed.

**Layout.** Parent Text Box places it at the resolved text caret. **Motion.**
The parent supplies frame state; Cursor does not own a clock.

## Label

**Purpose/ownership.** One or two text lines with optional Surface and
placement when embedded. **Runtime inputs.** Primary/subtext and, where used,
actor-derived label data. **Variant/config.** Typography chains, colors,
alignment, gap, Surface and style settings.

**Layout.** Empty subtext takes no layout space. Text alignment is local to
Label; parent placement is separate. Parent can use Label's visual bounds for
its own vertical intrusion rules.

**Motion.** No independent clock. **Limitations.** Font/emoji measurement is
shared with Text Box and has known preview fidelity work remaining.

## Avatar

**Purpose/ownership.** Actor image/initials/default preview plus optional
embedded Label. **Runtime inputs.** Actor record/name/image/initials data.
**Variant/config.** Size, image transform, surface/style, label variant,
label/subtext visibility and placement.

**Layout/z-order.** Avatar owns image clipping within its visual frame. Its
embedded Label is placed relative to Avatar. A parent places Avatar relative to
the parent component and determines whether it affects parent sizing.

**Motion.** No independent clock.

## Button Icon

**Purpose/ownership.** Icon-oriented action surface, optionally with embedded
Label. **Runtime inputs.** Icon token and any externally supplied text.
**Variant/config.** Surface, icon sizing mode, glyph/padding, theme colors and
label variant.

**Layout.** In fixed sizing mode, size refers to button frame; in glyph plus
padding mode, size refers to glyph. It owns internal centering. **Motion.** No
independent clock.

## Text Box

**Purpose/ownership.** Text content frame with Surface, Typography, Cursor and
optional left/right Icon Rows. **Runtime inputs.** Text, placeholder, size
constraints/mode, max lines and current caret/reveal state. **Variant/config.**
Surface, typography, padding spacing pair, alignment, overflow mode and child
variant slots.

**Layout.** Supports fixed/hug/grow policies, wrapping and clip/scroll behavior.
When scrolling, the resolved text/caret must use the same scroll offset. Parent
supplies available size; Text Box resolves text frame and internal children.

**Known limitation.** Exact browser measurement of the contract production text
and emoji fonts can still differ from the conservative estimator. Runtime
rendering must never substitute host-system text or emoji fonts to hide that
difference. Do not compensate in a specific parent.

## Icon Row

**Purpose/ownership.** Ordered horizontal/vertical run of Button Icon entries.
**Runtime inputs.** Icon list and optional one action-icon override.
**Variant/config.** Button Icon variant, icon size token, gap spacing token and
orientation.

**Layout.** Slot count derives from icon list; no arbitrary fixed max is needed.
The parent places/alines the resulting row. Action override affects only its
designated entry and preserves the Button Icon variant otherwise.

## Icon Bar

**Purpose/ownership.** Three-zone (`left`, `center`, `right`) two-state holder
for Icon Rows. **Runtime inputs.** Per-state icon lists and active/idle state.
**Variant/config.** Concrete Icon Row variants and action overrides.

**Layout/z-order.** Icon Bar resolves zone contents; parent assigns its frame
and edge padding. It is used by Text Input Bar, Keyboard and Media without
component-specific renderer logic.

## Shared atom motion and data boundary

Atoms do not own a wall-clock. A component or module supplies the requested
frame/state. Atom resolvers may calculate a visual phase from that data and
emit resolved atoms; generic web rendering only paints them.
