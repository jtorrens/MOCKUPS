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
placement when embedded. **Runtime inputs.** Primary/subtext, literal/count-up/
count-down source and independent decimal size multipliers. **Variant/config.**
Typography chains, colors, alignment, tokenized vertical gap, subtext Top/Bottom
position and relative Left/Center/Right alignment, Surface and the global Text
shadow switch shared by Text/Subtext.

**Layout.** Empty subtext takes no layout space. Subtext is placed above or
below the primary text and aligned to its measured left edge, center or right
edge. The spacing token is the vertical distance between both texts. Text alignment is local to Label; parent placement
is separate. Parent can use Label's visual bounds for its own intrusion rules.

**Motion.** No independent clock. Calculated text resolves from the supplied
owner-local frame and FPS before rendering. **Limitations.** Font/emoji
measurement is shared with Text Box and has known preview fidelity work
remaining.

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

## Component Stack

**Purpose/ownership.** Generic vertical container for ordered stable slots.
Every slot owns an ordered State collection; every State owns a concrete
component Variant or explicit None, local Overrides, child Runtime Inputs,
Placement and Enter/Exit Motion. State 1 is the default Replace State; later
States add animatable Active and Replace/Overlay behavior. Sizing, Start/End
gaps, slots and their States are all Runtime Inputs. Its protected `Default`
Variant contains no composition.

**Layout.** `fill` consumes the parent box and distributes weighted reflow
space. `content` hugs its children. Every slot from the second onward owns its
gap before itself; the first and final boundaries are owned by the container.
State selection is independent per slot and State Placement resolves inside the
frame assigned to that slot.
See the full canonical contract in [component_stack.md](component_stack.md).

## Collection Stack

**Purpose/ownership.** Generic runtime collection for variable component
groups. Each item owns one concrete Variant, local Overrides, embedded Runtime
Inputs, alignment, Present and Presence Motion. It has no nested Component Stack
State model. Runtime distribution is `Flow` or `Stacked`; its protected
`Default` Variant contains no composition.

**Layout.** `Flow` uses the ordinary vertical gap/reflow model. `Stacked` places
all children in one region; item zero is foreground and a tokenized offset is
applied downwards or upwards. Intrinsic or Largest-item frames are explicit;
Stacked depth may reduce scale and opacity exponentially by collection index.
Each child owns animatable Present and one Presence Motion. After a completed
exit, Theme-timed Reflow moves the surviving resolved boxes. The atom owns no
notification semantics or clock. See [collection_stack.md](collection_stack.md).

## Shared atom motion and data boundary

Atoms do not own a wall-clock. A component or module supplies the requested
frame/state. Atom resolvers may calculate a visual phase from that data and
emit resolved atoms; generic web rendering only paints them.

Calculated Label values follow the same boundary: the owning component resolver
knows the declarative source and resolves its text for the requested frame.
Label receives the resulting text only; neither Label nor the renderer receives
callbacks, timers or component-specific source knowledge.
