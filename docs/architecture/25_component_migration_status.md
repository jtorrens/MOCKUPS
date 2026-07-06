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
- a seeded protected `Default` preset;
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
| `label` | atom | Functional reference | Text/subtext, sizing, typography tokens, alpha, text align and surface style are on the new route. |
| `avatar` | component | Functional reference | Embeds `label`; actor input and label/subtext sample values work through the generic input path. |
| `buttonIcon` | atom | Functional reference | Embeds `label`; icon input and optional label are on the recursive route. |
| `audio` | component | Functional reference, still evolving | Embeds `avatar` and `buttonIcon`; playback inputs are generic frame inputs. Future work may refine waveform behavior, badge semantics and animation details. |
| `status_bar` | system | Structural/functional enough for current preview | Routed as a system component with its own resolver/renderable. Further device-specific preset work may refine it. |
| `navigation_bar` | system | Structural/functional enough for current preview | Routed as a system component with its own resolver/renderable. Further device-specific preset work may refine it. |
| `textInputBar` | system | Structurally migrated | Preview renders a generic input surface, text and cursor. Final text-entry behavior, accessory slots and runtime input contract still need definition. |
| `keyboard` | system | Structurally migrated | Preview renders a generic keyboard surface, keys and bottom icons. Final keyboard layout model and interaction states still need definition. |
| `video` | component | Structurally migrated | Preview renders a generic video surface, status row and play overlay. Final media, timeline, controls and embedding semantics still need definition. |

## Legacy Runtime Paths Still Present

The React runtime still contains modules and renderer support for older runtime
nodes such as:

- `text_input_bar`;
- `keyboard`;
- `keyboard_key`;
- `message_bubble_video_*`;
- other `message_bubble_*` nodes.

These are outside the desktop component-class preview route. They must not be
used as shortcuts for migrated desktop components.

When a runtime module is moved to the new component route, migrate its owned
component graph together. In particular, the bubble migration must move bubble,
labels, avatar, media, audio, video, icon button, tail/chrome and status pieces
as one planned phase, not as mixed legacy/new fragments.

## Enforcement

`npm run check:architecture` currently verifies:

- no central `webPreviewBridge.ts` exists;
- central desktop preview renderer/common helpers do not contain component names;
- manifest entries point to real contract/resolver/renderable modules;
- registry routes every manifest component;
- manifest entries and desktop component seeds match;
- component renderables emit only allowed generic primitive node types;
- concrete component imports only follow declared manifest ownership;
- legacy `audio_message`, `button_icon`, `text_input_bar` and `video_message`
  names do not return to the desktop preview manifest or registry.

## Next Safe Work

1. Keep strengthening architecture checks when a rule becomes concrete.
2. Define `textInputBar` functionality first; it is the smallest structurally
   migrated component.
3. Define `keyboard` after text input, because it has more internal layout but
   few embedded dependencies.
4. Define `video` after deciding how media inputs, status row and play controls
   should compose with bubble/runtime data.
5. Defer bubble migration until its full owned component graph is ready.
