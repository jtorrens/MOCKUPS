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

## Button

**Purpose/ownership.** Generic action atom with `icon`, `text` and `iconText`
content modes. It is the sole button atom; the former Button Icon class is retired.

**Runtime inputs.** Text, icon token, icon/text size tokens and persistent state: `normal`, `active`,
`pushed` or `disabled`. **Variant/config.** Content mode, content/fixed sizing,
spacing-token padding and gap, plus four visual state blocks. Every state block
owns its Surface variant, Label variant and icon color. Surface exclusively
owns state opacity through its background, border and other alpha values. The
variant also stores the `theme.motion.buttonPushedDurationMs` timing-token
reference.

**Layout.** Button owns horizontal icon/text composition and internal centering.
Content mode hugs the resolved child bounds plus token padding; fixed mode uses
the configured frame.

**States/motion.** The resolver receives the state for the requested frame. The
declarative `Push` Test Values action temporarily overrides it with `pushed`
for `theme.motion.buttonPushedDurationMs`, then restores the previous state.
The generic action session resolves that Theme duration in milliseconds;
neither Button nor the web renderer starts a timer.

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

**Purpose/ownership.** Ordered horizontal/vertical collection of Button entries.
**Runtime inputs.** Stable Button items with independent content/state/actions.
**Variant/config.** Gap/orientation plus `shared` or `perButton` size ownership.
Shared mode is the default and sends one icon/text size pair to every Button.

**Layout.** Slot count derives from icon list; no arbitrary fixed max is needed.
The parent places/alines the resulting row. Action override affects only its
designated entry and preserves the Button Icon variant otherwise.

## Icon Bar

**Purpose/ownership.** Three-zone (`left`, `center`, `right`) two-state holder
for Icon Rows. **Runtime inputs.** Per-state row data and active/idle state.
**Variant/config.** Concrete Icon Row variants plus `shared` or `perRow` size
ownership. Shared mode sends one icon/text size pair to all three rows.

**Layout/z-order.** Icon Bar resolves zone contents; parent assigns its frame
and edge padding. It is used by Text Input Bar, Keyboard and Media without
component-specific renderer logic.

## Shared atom motion and data boundary

Atoms do not own a wall-clock. A component or module supplies the requested
frame/state. Atom resolvers may calculate a visual phase from that data and
emit resolved atoms; generic web rendering only paints them.

Calculated Label values follow the same boundary: the owning component resolver
knows the declarative source and resolves its text for the requested frame.
Label receives the resulting text only; neither Label nor the renderer receives
callbacks, timers or component-specific source knowledge.
