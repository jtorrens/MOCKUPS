# Password

Status: functional System component on the component resolver -> renderable
route.

Source of truth: `src/desktop-preview/passwordComponentContract.ts`,
`passwordComponentResolver.ts` and `passwordComponentRenderable.ts`.

## Responsibility and composition

Password owns a vertical interaction composed, in order, from:

1. one state-selected Label;
2. one Code Indicator;
3. one Keypad;
4. one Icon Bar.

All four slots reference concrete child Variants and retain the shared Open and
Override route. The Password Variant owns the three Label strings and Variants,
the child Variants, its vertical anchor modes, tokenized gaps and the Icon Bar
height. The empty Icon Bar Variant hides that region without switches or a
component-specific exception.

The Password frame is the complete frame supplied by its parent. Keypad is
always centered horizontally and vertically in that frame. Label and Code
Indicator form one horizontally centered upper block; Icon Bar is the
horizontally centered lower block. Each block independently anchors either to
its container edge or to Keypad. Container anchoring uses `startGapToken` or
`endGapToken`; Keypad anchoring uses `upperGapToken` or `lowerGapToken`. The
upper block also owns `labelIndicatorGapToken`. No free placement coordinates
or Password-specific layout rule crosses the component boundary.

## Runtime inputs

- `expectedPassword`, containing digits only;
- `attemptPassword`, containing digits only and exactly the same length;
- `enabled`;
- `entryTiming`, using the generic `BehaviorTiming` value kind.

The Design Preview action owns calculated `entryTrigger` and `entryFrame`
fixtures. They are not separate public Test Values and do not introduce a
persistent start switch.

Every attempted digit must map to an enabled emitted value in the selected
Keypad Variant. A malformed value, length mismatch or unavailable digit is an
explicit resolver error; the resolver does not coerce or substitute values.

## Frame behavior

Fixed timing uses the authored frame duration. Natural timing uses the attempt
grapheme count, the Password-owned reference rate of 4 frames per digit and the
selected `theme.motion.naturalPace.*` multiplier. Password distributes digit
presses deterministically across that final duration.

Before the action it resolves the initial Label, an empty initial indicator and
no pushed key. During entry it remains in the initial state, advances the
filled count and supplies exactly one final `pushedKey` to Keypad for the
requested frame. At the final frame it fills the indicator, clears the pushed
key, compares the two explicit strings and selects the correct or incorrect
Label, indicator state and active Icon Bar state.

Its Design Preview action declares generic `completionBehavior: holdFinal`, so
the action host retains the resolved final frame after playback. Replaying the
action starts again at frame zero; Reset Test Values returns to the initial
state. Momentary actions declare `reset` explicitly.

There are no timers, CSS animations or renderer interpolation. The action and
production timeline request frames; Password resolves the complete state of
each child before render.

## Composition boundary

Password explicitly embeds Label, Code Indicator, Keypad and Icon Bar. Its
resolver owns validation, state selection and layout inputs; its renderable
owns their vertical placement. The bridge, generic renderer and `MainWindow`
contain no Password rules.
