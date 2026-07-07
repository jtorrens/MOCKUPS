# Component Migration Status

This document tracks desktop design-preview component migration state.

It is intentionally operational: it records what is already on the new route,
what is only structurally migrated, and what still depends on legacy runtime
paths outside the desktop component preview.

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

| Component type | Category | Status | Notes |
| --- | --- | --- | --- |
| `surface` | atom | Structural reference | Reusable visual surface appearance. Size is supplied as runtime input/parent box; variant owns background, alphas, border, radius, shadow and relief. |
| `cursor` | atom | Structurally migrated | Reusable text cursor atom. Height is supplied as runtime input; variant owns theme color token, width, minimum fade alpha and fade frame timing. Animation remains resolver frame data, not renderer state. |
| `textBox` | atom | Structurally migrated | Reusable text field atom. Size, text, placeholder and internal left/right icon row data are runtime inputs; variant owns surface, padding, typography, text colors, alignment, overflow mode and embedded cursor variant. |
| `iconRow` | atom | Structurally migrated | Reusable row/column of `buttonIcon` instances. Its inputs can stay runtime or be fixed by a parent variant. In `textInputBar`, each icon row state stores size/gap/orientation/icons/action styling as parent variant data. |
| `label` | atom | Functional reference | Text/subtext, sizing, typography tokens and text align are on the new route. Visual surface is an embedded `surface` variant. |
| `avatar` | component | Functional reference | Embeds `label`; actor input and label/subtext sample values work through the generic input path. |
| `buttonIcon` | atom | Functional reference | Embeds `surface` and `label`; icon input and optional label are on the recursive route. |
| `audio` | component | Functional reference, still evolving | Embeds `surface`, `avatar` and `buttonIcon`; playback inputs are generic frame inputs. Future work may refine waveform behavior, badge semantics and animation details. |
| `status_bar` | system | Structural/functional enough for current preview | Routed as a system component with its own resolver/renderable. Further device-specific variant work may refine it. |
| `navigation_bar` | system | Structural/functional enough for current preview | Routed as a system component with its own resolver/renderable. Further device-specific variant work may refine it. |
| `textInputBar` | system | Structurally migrated | Preview renders a screen-width bar with embedded `surface`, `textBox` and state-specific left/right `iconRow` slots. `idle` vs `typing` is derived from whether runtime text is empty. Icon token lists are variant data on each row state, not runtime preview inputs. Text content is runtime data passed into the embedded `textBox`; placeholder is fixed by the parent variant through generic input bindings. |
| `keyboard` | system | Structurally migrated | Preview renders a generic keyboard surface, keys and bottom icons. Final keyboard layout model and interaction states still need definition. |
| `video` | component | Structurally migrated | Preview renders an embedded `surface` variant, status row and play overlay. It currently declares a duration text runtime input; final media, timeline, controls and embedding semantics still need definition. |

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
bubble migration must move bubble, labels, avatar, media, audio, video, icon
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
  such as `component.buttonIcon` and `component.textInputBar`, not legacy
  snake-case ids.
- the `textBox` atom stays on its own resolver/renderable route and is not
  folded into `textInputBar` until it is embedded as a normal component slot.
- component runtime inputs use generic `recordReference` + `tableId`, not
  specialized record input kinds such as `ActorReference`.

Compatibility note:

- legacy `status_bars` and `navigation_bars` physical tables still exist in the
  TypeScript persistence/runtime compatibility layer. They are not part of the
  desktop editor or desktop preview component route. Removing them belongs to a
  future screen/module runtime rebuild, not to component preview cleanup.

## Next Safe Work

1. Keep strengthening architecture checks when a rule becomes concrete.
2. Define `textInputBar` functionality first; it is the smallest structurally
   migrated component.
3. Define `keyboard` after text input, because it has more internal layout but
   few embedded dependencies.
4. Define `video` after deciding how media inputs, status row and play controls
   should compose with bubble/runtime data.
5. Defer bubble migration until its full owned component graph is ready.
