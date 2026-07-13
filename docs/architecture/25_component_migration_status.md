# Component Migration Status

This document tracks desktop design-preview component migration state.

It is intentionally operational: it records what is already on the new route,
what is only structurally migrated, and what still depends on legacy runtime
paths outside the desktop component preview.

## Canonical Behavior Reference

The concise migration state remains here. The canonical functional behavior,
runtime/configuration boundary, composition, layout, timeline and known gaps
for every active module/component is maintained in
[component_behavior/README.md](component_behavior/README.md).

Current summary:

- Conversation, Bubble, Text Input Bar, Keyboard, Media and Audio are active
  on the component resolver -> renderable -> generic renderer route.
- The atoms and system bars are active on the same route.
- The behavior reference records the remaining Conversation shared-time,
  message-schema, generic text/emoji and video-preview limitations.

## Definitions

### Structurally Migrated

A component is structurally migrated when it has:

- a component manifest entry in `src/desktop-preview/desktopPreviewComponents.ts`;
- an owning contract module;
- an owning resolver module;
- an owning renderable module;
- a registry route in `componentClassRenderableRegistry.ts`;
- dictionary-backed desktop fields;
- a seeded protected `Default` variant;
- no component-specific branches in central desktop preview helpers or renderer.

This means it can be selected and previewed without falling back to the magenta
unsupported placeholder.

### Functionally Defined

A component is functionally defined when its resolver/renderable contract matches
the intended production behavior for:

- runtime inputs;
- embedded component slots;
- sizing and placement rules;
- animation frame data;
- visual styling;
- edge cases.

Structural migration is allowed to precede functional definition when the goal
is to remove legacy routing risk first.

## Current Desktop Component Route

The executable source of truth for the status column is the component manifest
field `migrationStatus`.

## Desktop Editor UX/UI Baseline (2026-07-13)

The component, atom, system and Runtime Inputs editors now use one shared
metadata-driven organization system:

- `flatStack` for repeated collection instances on the parent surface;
- `verticalCards` for internal vertical navigation;
- `separatedSections` for continuous semantic field groups;
- per-group `presentation` for mixed cards;
- a generic `General` child for direct owner fields when sibling subcards exist;
- `pairLayout: sharedHeader` for compact two-column Light/Dark palette pairs.

The same shared implementation is used by component classes, atom/system
classes, Theme groups and Runtime Inputs. No layout mode is selected from a
component name or editor type. New categories use reusable semantic SVG assets
from `assets/system/system_icons/components`.

All editor cards begin closed in a new application session. Card expansion,
internal selection and scroll state are retained only while the process is
running and are keyed by editor node. They are not part of persisted window
state.

The retired singular token `theme.typography.size` has been removed from the
catalog, layout, default tokens and committed themes. Typography uses only
`theme.typography.sizes.*`, split into Font families, Text sizes and Style and
line heights vertical groups.

Component preset references in runtime input contracts use full references:

```text
componentClassId::preset::presetId
```

Contract-signature changes invalidate stale session-only Test Values for that
scope instead of coercing legacy short references.

| Component type | Category | Status | Notes |
| --- | --- | --- | --- |
| `surface` | atom | Structural reference | Reusable visual surface appearance. Size is supplied as runtime input/parent box; variant owns background, alphas, border, radius, shadow, relief and optional tail geometry. |
| `cursor` | atom | Structurally migrated | Reusable text cursor atom. Height is supplied as runtime input; variant owns theme color token, width, minimum fade alpha and fade frame timing. Animation remains resolver frame data, not renderer state. |
| `textBox` | atom | Structurally migrated | Reusable text field atom. Size, text, placeholder and internal left/right icon row data are runtime inputs; variant owns surface, padding, typography, text colors, alignment, overflow mode and embedded cursor variant. |
| `iconRow` | atom | Structurally migrated | Reusable row/column of stable `button` instances. Every item independently owns runtime content mode, state, icon/text and Push action. `textBox` still uses internal left/right icon rows for icons inside the text field. |
| `iconBar` | atom | Structurally migrated | Reusable left/center/right grouping of `iconRow` instances with idle/active states. Parent components provide the outer frame; the bar aligns rows inside that frame and keeps state-specific row variants/settings together. `textInputBar` and `keyboard` use this instead of owning direct icon rows. |
| `label` | atom | Functional reference | Text/subtext, sizing, typography tokens and text align are on the new route. Visual surface is an embedded `surface` variant. |
| `avatar` | component | Functional reference | Embeds `label`; actor input and label/subtext sample values work through the generic input path. |
| `button` | atom | Functional reference | Generic action atom with `icon`, `text` and `iconText` runtime content modes. Embeds state-specific `surface` and `label` variants, supports content/fixed sizing, exposes `normal`, `active`, `pushed` and `disabled` runtime states, and provides a declarative `Push` action timed by `theme.motion.buttonPushedDurationMs`. It replaces the retired `buttonIcon` class. |
| `audio` | component | Functional reference, still evolving | Embeds `surface`, `avatar` and a `button` badge fixed by the parent to icon/normal runtime inputs; playback inputs are generic frame inputs, exposed through a preview action. Future work may refine waveform behavior, badge semantics and animation details. |
| `status_bar` | system | Structural/functional enough for current preview | Routed as a system component with its own resolver/renderable. Further device-specific variant work may refine it. |
| `navigation_bar` | system | Structural/functional enough for current preview | Routed as a system component with its own resolver/renderable. Further device-specific variant work may refine it. |
| `textInputBar` | system | Structurally migrated | Preview renders a screen-width bar with embedded `surface`, `textBox` and one `iconBar` slot. `idle` vs `typing` is derived from whether runtime text is empty. Icon token lists are variant data inside the embedded `iconBar`, not runtime preview inputs. Text content is runtime data passed into the embedded `textBox`; placeholder is fixed by the parent variant through generic input bindings. |
| `keyboard` | system | Structurally migrated | Preview renders a generic keyboard surface, keys and bottom icons. It is the first component using component-level motion intent plus theme timing tokens; preview animation is resolved as frame data and launched from a generic preview action before the generic web renderer paints it. Final keyboard layout model and interaction states still need definition. |
| `media` | component | Structurally migrated | Replaces the old desktop `video` component route. Preview renders an embedded `surface` variant plus separate inline and full-screen top/center/bottom `iconBar` overlays. Runtime inputs cover image/video source type, viewport size, decimal media scale/offset, play/pause, current/duration time, full screen on/off and fullframe orientation. Component motion is the inline-to-fullframe transition, not component entrance, and is launched through a separate generic `Full screen` preview action. Video playback is not owned by the web preview; local video sources are resolved at the requested time into cached frame files and then painted through the same generic image primitive used by still images. |
| `bubble` | component | Structurally migrated | Embeds `surface`, `textBox`, optional actor `label`, optional actor `avatar`, and an optional selected media attachment slot. Variant data selects `none`, `image`, `video` or `audio`, stores one variant slot for each media kind, and chooses media position relative to text (`top`, `bottom`, `left`, `right`); image/video resolve through `media`, audio resolves through `audio`. Runtime inputs define state (`incoming`, `system`, `outgoing`), max text width as a screen-width percentage, text, actor, write-on timing in frames, status text/state, media source/viewport/playback and media actions. Variant data owns text padding, per-state light/dark palette color pairs, status icons/colors/icon-size token/text-size token, actor label placement and avatar placement. Status text and icons are composed as one bottom-right status block. The parent will own message alignment. The bubble resolver owns state-to-tail mapping: incoming = left tail, system = no tail, outgoing = right tail; tail vertical/geometry stays in the selected surface variant. Text reveal currently uses the shared simple write-on frame helper and is prepared for richer future typing plans. |

## Known Preview Issues

- Message text may occasionally differ between the Test Values control and the
  rendered preview, especially around emoji or the final wrapped segment. Do
  not hide it with a truncation fallback. Revisit the generic text measurement,
  wrapping and clipping path together after the current Production UX pass.

## Legacy Runtime Paths Removed

The React/debug/remotion runtime route has been removed from this repository.
Removed paths include:

- `src/debug-ui`;
- `src/debug-server`;
- `src/electron`;
- `src/remotion`;
- `src/visual/adapters/react`;
- `src/visual/layout`;
- `src/visual/modules`;
- `src/visual/validation`.

When chat/screen module rendering is rebuilt, use the previous React app only as
an external reference for behavior and visual parity. Do not restore old runtime
modules or `message_bubble_*`, `text_input_bar`, `keyboard_key`,
`status_bar_item`, or `navigation_bar_item` render nodes. In particular, the
bubble migration must move bubble, labels, avatar, media, audio, icon
button, tail/chrome and status pieces as one planned phase, not as mixed
legacy/new fragments.

## Enforcement

`npm run check:architecture` currently verifies:

- no central `webPreviewBridge.ts` exists;
- removed React/debug/remotion legacy route directories do not reappear;
- removed renderable fallback helpers do not reappear under
  `src/visual/renderable/helpers.ts`;
- desktop design preview uses the clean desktop HTML adapter instead of the
  legacy React renderable adapter;
- central desktop preview renderer/common helpers do not contain component names;
- manifest entries point to real contract/resolver/renderable modules;
- registry routes every manifest component;
- manifest entries and desktop component seeds match;
- component renderables emit only allowed generic primitive node types;
- the shared `renderableNodeTypes` list matches the explicit primitive
  allowlist, and `RenderableNodeType`, the renderable runtime schema and the
  HTML adapter supported type list derive from it;
- component renderables do not emit component identity metadata into the final
  paint tree;
- the desktop HTML adapter supports only the generic paint primitive list and
  does not keep compatibility support for removed semantic/special node types;
- paint tree metadata may not spread component contracts or carry component
  semantic keys such as kind, zone, order, value or componentType;
- concrete component imports only follow declared manifest ownership;
- no shared `systemBar*` route exists; status/navigation are normal manifest
  components with category `system`;
- the desktop spike cannot seed, query or edit legacy `status_bars` /
  `navigation_bars` rows;
- legacy `audio_message`, `button_icon`, `text_input_bar` and `video_message`
  names do not return to the desktop preview manifest, registry, domain
  component schema, domain field options, domain fixtures, chat resolver
  component lookup, or SQLite component seeds;
- migrated desktop component record-class ids use the current component names
  such as `component.button` and `component.textInputBar`, not legacy
  snake-case ids.
- the `textBox` atom stays on its own resolver/renderable route and is not
  folded into `textInputBar` until it is embedded as a normal component slot.
- component runtime inputs use generic `recordReference` + `tableId`, not
  specialized record input kinds such as `ActorReference`.
- retired radius tokens (`control`, `card`, `panel`, `surface`, `pill`,
  `avatar`) do not remain in committed theme/component data.
- retired keyboard-owned color/style fields do not remain in committed
  keyboard component data; keyboard colors resolve from `theme.keyboard.*`.
- runtime collections with `sourceCollectionJsonKey` have stable item ids,
  reject stale test-value overrides, and keep the test payload synchronized
  with the declared source collection.
- preview actions are declarative payload data with required `id`, `label` and
  timeline fields for duration-driven actions; the editor triggers the payload
  action instead of knowing module-specific behavior.
- every component class uses its `Default` variant as the canonical
  configuration; `config_json` is a synchronized parity copy and the
  architecture check rejects any divergence between them.

`npm run test` is the active desktop validation path. It builds the desktop
preview bundle, runs TypeScript, runs this architecture check, and builds the
Avalonia/Suki desktop editor. Older TypeScript domain, resolver and SQLite
validations are still available under `legacy:` script names only as historical
reference while their source files live under `archive/react-legacy/`.

Compatibility note:

- schema v1 removes legacy `status_bars` and `navigation_bars` physical tables
  from the committed desktop DB. Status/navigation are ordinary `system`
  component classes and variants.

## Next Safe Work

- Add `Promote variant to Default` beside `Save variant`: after confirmation,
  copy the selected variant configuration into `Default` without deleting or
  renaming the source variant.

1. Keep strengthening architecture checks when a rule becomes concrete.
2. Define `textInputBar` functionality first; it is the smallest structurally
   migrated component.
3. Define `keyboard` after text input, because it has more internal layout but
   few embedded dependencies.
4. Define `media` behavior after validating viewport/fullframe controls and
   deciding how media source/frame extraction should compose with bubble/runtime
   data.
5. Continue bubble migration in phases: media/audio/status/avatar attachment can be added after the text bubble route is stable.
