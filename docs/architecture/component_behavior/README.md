# Desktop Preview Behavior Reference

Status: canonical functional reference for active desktop-preview components and modules.

This directory describes the observable contract of each active module and
component. It complements, but does not replace, the TypeScript contract,
resolver and renderable that own the implementation.

## Reading a behavior sheet

- **Runtime inputs** are values supplied by a module instance, screen/shot, or
  the Design Test Values panel. Test Values must use the exact same public
  contract as production.
- **Variant/configuration** is reusable design data stored on a concrete
  component variant. A parent embeds a concrete child variant reference, not a
  child class.
- **Resolver** owns component-specific validation, state and composition for a
  requested frame. It emits generic resolved atoms.
- **Renderer** paints those atoms only. It does not resolve tokens, inherit
  variants, inspect database records, run timers, or make component decisions.
- **Parent** owns placement and contextual composition of a child. An embedded
  child owns its own internal layout.

## Shared rules

1. A component contract must distinguish runtime inputs from variant values.
2. Parent-to-child dependencies are declared in the parent component manifest
   and are concrete variant references at runtime.
3. All animation is resolved frame data. The parent/module supplies the frame
   or shared time; web preview paints the resolved frame without its own clock.
4. `theme.spacing.*` tokens represent all visual padding and gaps. Raw numeric
   padding is not part of a reusable component contract.
5. A missing required value is an error reported through the preview message
   surface. Do not conceal it with a plausible development fallback.
6. Full-frame overlays become siblings above their normal parent composition
   when their visual meaning requires it; a child cannot escape an ancestor
   clipping boundary through z-order alone.

## Coverage

| Area | Behavior sheet | Active status |
| --- | --- | --- |
| Conversation module | [conversation.md](conversation.md) | Functional, with noted alignment/input gaps |
| Bubble | [bubble.md](bubble.md) | Functional |
| Text Input Bar | [text_input_bar.md](text_input_bar.md) | Functional |
| Keyboard | [keyboard.md](keyboard.md) | Functional, parent integration gap noted |
| Media | [media.md](media.md) | Functional, video buffering remains a limitation |
| Audio | [audio.md](audio.md) | Functional |
| Component Stack | [component_stack.md](component_stack.md) | Structural/functional |
| Atoms | [atoms.md](atoms.md) | Functional/structural as stated per atom |
| System bars | [system_bars.md](system_bars.md) | Functional |

Cross-component discrepancies and pending rules are collected in
[open_items.md](open_items.md).

## Verification scope

The sheets were checked against the active desktop-preview contracts,
resolvers, renderables and registry on 2026-07-14. Each sheet records any
known mismatch between the canonical rule and the current implementation. A
mismatch is a backlog item, not an alternative contract.
