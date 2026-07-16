# Password

Status: functional System component on the component resolver -> renderable
route.

Source of truth: `src/desktop-preview/passwordComponentContract.ts`,
`passwordComponentResolver.ts` and `passwordComponentRenderable.ts`.

## Responsibility and composition

Password owns a vertical interaction composed, in order, from:

1. one state-selected Label;
2. one Code Indicator;
3. one mode-selected input component;
4. one Icon Bar.

The input component is Keypad, Fingerprint, Face Recognition or Draw Password
according to the Password Variant's explicit `mode`. All slots reference
concrete child Variants and retain the shared Open and
Override route. The Password Variant owns the three Label Variants, the child
Variants, its vertical anchor modes, tokenized gaps and the Icon Bar height.
The three Label strings are runtime content. The empty Icon Bar Variant hides
that region without switches or a component-specific exception.

The four input slots are presented in one `Modes` editor card using the shared
vertical-card navigation: Keypad, Fingerprint, Face Recognition and Draw
Password. This is layout metadata only; it does not alter composition.

The Password frame is the complete frame supplied by its parent. The selected
input component is always centered horizontally and vertically in that frame. Label and Code
Indicator form one horizontally centered upper block; Icon Bar is the
horizontally centered lower block. Each block independently anchors either to
its container edge or to the input component. Container anchoring uses
`startGapToken` or `endGapToken`; input anchoring uses `upperGapToken` or
`lowerGapToken`. The
upper block also owns `labelIndicatorGapToken`. No free placement coordinates
or Password-specific layout rule crosses the component boundary.

## Runtime inputs

- `initialText`, `correctText` and `incorrectText`;
- `expectedPassword`, containing digits only;
- `attemptPassword`, containing digits only and exactly the same length;
- `enabled`;
- `entryTiming`, using the generic `BehaviorTiming` value kind.

The action contract also owns `entryTrigger` and `entryFrame`. They are
action-only runtime fields: Test Values presents them as one Play/Restore
control rather than raw fields, while a containing component or module may
Forward them with the other action dependencies. In a Shot instance the play
field is authored as an ordinary v2 animation track and the owner timeline
resolves `entryFrame` for every requested frame.

At every parent boundary all these fields become Variant values by default.
The parent designer chooses which ones to promote through the same generic
Forward control used by Label and every other embedded component. Password,
Lock Screen, the editor shell and the timeline have no special propagation
path.

Every attempted digit must map to an enabled emitted value in the selected
Keypad Variant. A malformed value, length mismatch or unavailable digit is an
explicit resolver error; the resolver does not coerce or substitute values.

PIN accepts digits, requires equal expected/attempt lengths and validates every
attempt digit against the selected Keypad. Draw Password accepts unique digits
1-9 as one-based nodes in its 3x3 grid. Fingerprint and Face Recognition accept
explicit non-empty credential strings. All modes compare the two explicit
values only at the final frame.

The seeded Password Variants are PIN (`Default`), Fingerprint, Face Recognition
and Draw Password. The three non-PIN Variants explicitly select the `Empty`
Code Indicator Variant, whose `displayMode: collapsed` removes both its visual
box and the adjacent Label/Indicator gap. This is stored composition, not a
mode-specific visibility branch, and can be replaced through the normal
embedded Variant/override route.

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

Password explicitly embeds Label, Code Indicator, Keypad, Fingerprint, Face
Recognition, Draw Password and Icon Bar. Its
resolver owns validation, state selection and layout inputs; its renderable
owns their vertical placement. The bridge, generic renderer and `MainWindow`
contain no Password rules.
