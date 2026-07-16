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

- Conversation, Bubble, Text Input Bar, Keyboard, Keypad, Password, Media and Audio are active
  on the component resolver -> renderable -> generic renderer route.
- Lock Screen is active on its module resolver -> renderable route. It owns the
  composition of its runtime Component Stack and optional status/navigation
  slots, and consumes the Actor wallpaper contract without adding bridge or
  renderer rules. The Stack receives only the available box between visible
  bars. Lock Screen binds the Stack runtime contract as Variant data and
  forwards only explicitly promoted fields to its own runtime contract.
- Module Variants are implemented generically. Module classes own resolver and
  schema while protected `Default` and user Variants own concrete config
  snapshots. The tree selects concrete Variants, and Screen instances persist
  a full `moduleId::variant::variantId` reference. Runtime forwarding, preview,
  duration and animation use the selected Variant contract; no Actor/Device
  inference chooses module composition.
- The atoms and system bars are active on the same route.
- Keypad is a System component with Variant-owned ordered text/icon/spacer keys,
  generic structured collection editing and runtime active/pushed/disabled
  state. One shared Label owns key geometry and style; states override only
  background/text color and background/border alpha. Parent stacks own all
  surrounding display and action chrome.
- Password is a System component that composes concrete Label, Code Indicator,
  Keypad/Fingerprint/Face Recognition/Draw Password and Icon Bar Variants. It resolves fixed or Natural entry timing frame
  by frame, using a Password-owned reference rate of 4 frames per digit. Code
  Indicator is its reusable atom for initial/correct/incorrect empty and filled
  glyph surfaces. Keypad remains centered in the parent frame; the upper and
  lower blocks anchor independently to the container or selected input with spacing
  tokens. Its preview action explicitly holds the resolved final frame. Each
  non-PIN Variant selects the collapsed Code Indicator Variant explicitly.
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

## Desktop Editor UX/UI Baseline (2026-07-14)

The component, atom, system and Runtime Inputs editors now use one shared
metadata-driven organization system:

- `flatStack` for repeated collection instances on the parent surface;
- `verticalCards` for internal vertical navigation;
- `separatedSections` for continuous semantic field groups;
- per-group `presentation` for mixed cards;
- a generic `General` child for direct owner fields when sibling subcards exist;
- `pairLayout: sharedHeader` for compact two-column Light/Dark palette pairs.

Structured collections now use the same generic shell in Variant authoring,
Runtime Test Values and instance runtime. Component items embed the normal
grouped Runtime Inputs surface; only Variant authoring adds Forward triangles.
Complex dictionary controls occupy a full-width block below their label. Their
internal `verticalCards` navigation has a session-only splitter, natural height
and a content-minimum-driven horizontal fallback.

The same shared implementation is used by component classes, atom/system
classes, Theme groups and Runtime Inputs. No layout mode is selected from a
component name or editor type. New categories use reusable semantic SVG assets
from `assets/system/system_icons/components`.

All editor cards begin closed in a new application session. Card expansion,
internal selection and scroll state are retained only while the process is
running and are keyed by editor node. They are not part of persisted window
state. Embedded and runtime-root Override breadcrumbs restore that same return
context, including the selected internal tab and navigation width.

The editor shell also supports a generic Simplified projection declared in
`editor_layouts`. Keypad is the first configured component: Sizing, Key size,
the nested Label/Surface corner radius, and Key 10 Kind/Icon reuse the same
dictionary controls in Simplified and Complete. Complete exposes promotion
checkboxes. Embedded defaults are copied once into a parent-owned snapshot with
lock provenance; Password is the automated non-live-inheritance validation
case for its embedded Keypad. This first generic slice is implementation-complete
and remains open only for broader use testing before more components declare
their own projections.

Runtime Input Forwarding is active across nested component and collection
boundaries. Each parent design may explicitly keep a child input Runtime or
declare it Calculated; otherwise the input becomes Variant-owned at that new
boundary and may be promoted through an upward triangle. Forwarding preserves
structured arrays and record-reference metadata, resolves recursively before
component resolvers and removes its transient metadata before bridge/renderer
painting. No Actor or other runtime value propagates by matching name or type.

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

The dictionary `ComponentPreset` control presents these references in two
levels: Component first, then only the Variants owned by that component. The
compound control still commits one full reference and does not persist its
intermediate Component selection separately. Compound selectors reserve fixed
space for Open/Override/chevron actions and ellipsize their flexible text.
`IntegerPair` and `ThemeTokenPair` reflow from two columns to two rows when the
value column becomes too narrow; the calling editor does not own that behavior.

| Component type | Category | Status | Notes |
| --- | --- | --- | --- |
| `surface` | atom | Structural reference | Reusable visual surface appearance. Size is supplied as runtime input/parent box; variant owns background, alphas, border, radius, shadow, relief and optional tail geometry. |
| `cursor` | atom | Structurally migrated | Reusable text cursor atom. Height is supplied as runtime input; variant owns theme color token, width, minimum fade alpha and fade frame timing. Animation remains resolver frame data, not renderer state. |
| `textBox` | atom | Structurally migrated | Reusable text field atom. Size, text, placeholder and internal left/right icon row data are runtime inputs; variant owns surface, padding, typography, text colors, alignment, overflow mode and embedded cursor variant. |
| `iconRow` | atom | Structurally migrated | Reusable row/column of stable `button` instances. Every item independently owns runtime content mode, state, icon/text and Push action. `textBox` still uses internal left/right icon rows for icons inside the text field. |
| `iconBar` | atom | Structurally migrated | Reusable left/center/right grouping of `iconRow` instances with idle/active states. Parent components provide the outer frame; the bar aligns rows inside that frame and keeps state-specific row variants/settings together. `textInputBar` and `keyboard` use this instead of owning direct icon rows. |
| `componentStack` | atom | Migrated | Generic ordered runtime collection of slots. Every slot owns an ordered nested state collection through the recursive generic Structured Collection dictionary control. State 1 is the default; later states expose an animatable Active boolean, Replace/Overlay behavior, explicit empty Component support, concrete Variant/Overrides/Inputs and generic Enter/Exit Motion. A forwarded collection projection can expose those slots at the next boundary as vertical runtime tabs; each slot receives a generic target-state action whose combo options are derived from its nested state collection, followed by compact Play/Restore controls. The resolver evaluates both stable-id v2 tracks and preview state actions per requested frame, applies deterministic collection-order selection, restarts child-local time on entry and keeps a frozen outgoing child resolved for its finite exit. Outer alignment and fixed/reflow gap-before semantics remain unchanged. The flat runtime item shape is explicitly migrated into a slot whose first state contains the former component. No Stack rule exists in the bridge, renderer or MainWindow. |
| `collectionStack` | atom | Migrated | Generic ordered runtime collection for variable groups. Its single protected Default Variant has no composition; Flow/Stacked distribution, tokenized boundaries, stack direction/offset, Intrinsic/Largest-item sizing, depth scale/opacity ratios and all concrete child Variants/overrides/inputs are Runtime Inputs. Every stable item owns animatable Present plus generic Presence Motion. A false item remains renderable through its finite reversed exit; afterwards Theme-timed generic Reflow recursively interpolates the boxes of surviving stable-id renderables. Embedded animatable runtime-state changes use the same Reflow geometry route, including the changed child's internal Surface/content geometry rather than a uniform scale approximation. No timer or Notification rule exists in the atom, bridge or renderer. |
| `badge` | atom | Functionally migrated | Circular overlay with runtime Icon/Text mode, content, explicit diameter and palette background/content colors; Variant owns tokenized inner padding, text typography and generic placement. Visibility is an explicit parent runtime input. It never changes the parent layout bounds. |
| `notification` | component | Structurally migrated | One notification item composed from explicit Surface, Avatar and Summary/Detail Label Variant slots. General retains the standard identity fields; Layout owns Fixed size or Content + padding, tokenized padding/gap and Surface; dedicated Avatar and Label cards own their Variant and placement relative to the padded Surface. Avatar Badge settings use the generic child-input editor and may be forwarded individually. Placement determines order and gap is the minimum horizontal separation without moving `insideEdge` children outside padding. Runtime data owns Max width %, Actor, Summary/Detail state and text. In Content mode the resolver converts the percentage against resolved screen width, subtracts padding, Avatar and gap, constrains the selected Label, and receives deterministic measured lines and height before emitting generic primitives; Fixed mode ignores that runtime constraint. Summary/Detail is a target-state action whose size and label substitution resolve frame by frame with `theme.motion.reflow*`. |
| `notifications` | component | Migrated | Runtime notification collection built on an internal Collection Stack plus common Notification and Badge Variants. The mandatory General card remains; Layout exposes vertical Stack/Notification/Badge/Motion tabs. The Notifications Variant owns stack geometry, common item alignment/gap/Presence Motion, closed-item limit, initial distribution, common child Variants/overrides, Badge visibility and Distribution Motion; runtime Distribution is the closed/expanded action. Items contain only stable identity, Actor, Summary/Detail state and text plus Present, and inherit every other value from the common Variant. The closed limit never truncates the collection or Badge present-count; the Badge is absent in Flow. Distribution, presence, label substitution and subsequent Reflow are all resolved per requested frame before preview. |
| `label` | atom | Functional reference | Text/subtext, sizing, typography tokens and text align are on the new route. Subtext uses an explicit tokenized vertical gap, Top/Bottom position and Left/Center/Right alignment against the measured primary-text bounds; generic subtext placement is explicitly retired and migrated. Text and Subtext independently expose runtime literal/count-up/count-down sources plus decimal size multipliers applied after token resolution. Calculated text uses the explicit owner-local frame and FPS and reaches the renderer as final per-frame text; invalid clock formats are errors. A parent-owned maximum-width constraint can request deterministic production-font wrapping without becoming Label Variant data; Label emits one resolved text atom per line. Visual surface is an embedded `surface` variant. |
| `avatar` | component | Functional reference | Embeds `label` and an optional runtime-controlled `badge`; actor input and label/subtext sample values work through the generic input path. |
| `button` | atom | Functional reference | Generic action atom with `icon`, `text` and `iconText` runtime content modes. Embeds state-specific `surface` and `label` variants plus an optional runtime-controlled `badge`, supports content/fixed sizing, exposes `normal`, `active`, `pushed` and `disabled` runtime states, and provides a declarative `Push` action timed by `theme.motion.buttonPushedDurationMs`. It replaces the retired `buttonIcon` class. |
| `audio` | component | Functional reference, still evolving | Embeds `surface`, `avatar` and a `button` badge fixed by the parent to icon/normal runtime inputs; playback inputs are generic frame inputs, exposed through a preview action. Future work may refine waveform behavior, badge semantics and animation details. |
| `status_bar` | system | Structural/functional enough for current preview | Routed as a system component with its own resolver/renderable. Further device-specific variant work may refine it. |
| `navigation_bar` | system | Structural/functional enough for current preview | Routed as a system component with its own resolver/renderable. Further device-specific variant work may refine it. |
| `textInputBar` | system | Structurally migrated | Preview renders a screen-width bar with embedded `surface`, `textBox` and one `iconBar` slot. `idle` vs `typing` is derived from whether runtime text is empty. Icon token lists are variant data inside the embedded `iconBar`, not runtime preview inputs. Text content is runtime data passed into the embedded `textBox`; placeholder is fixed by the parent variant through generic input bindings. |
| `keyboard` | system | Structurally migrated | Preview renders a generic keyboard surface, keys and bottom icons. It is the first component using component-level motion intent plus theme timing tokens; preview animation is resolved as frame data and launched from a generic preview action before the generic web renderer paints it. Final keyboard layout model and interaction states still need definition. |
| `keypad` | system | Structurally migrated | Generic ordered grid of `text`, `icon` and `spacer` keys. Layout and Keys are independent first-level editor cards; Normal, Active, Pushed and Disabled share vertical state navigation. One embedded Label Variant owns common key geometry and style, while each state overrides only background/text color and background/border alpha. Runtime inputs provide available width, active key, pushed key and enabled state; the generic Push key action selects its destination and uses `theme.motion.buttonPushedDurationMs`. Surrounding chrome remains parent-owned. |
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
  action instead of knowing module-specific behavior. Their shared Test Values
  surface provides Play/Restore and optional toggle/option destinations;
  per-action source snapshots are session-only.
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
