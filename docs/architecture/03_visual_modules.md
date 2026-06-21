# Visual modules

Visual modules are independent versioned modules selected through a registry. A screen module owns its data/config schemas and receives validated module JSON plus resolved common context and a frame. It returns a renderable animated graphic and never knows SQL, queries a repository, or resolves files directly.

## Screen modules

- `ChatScreen`
- `LockScreen`
- `NotificationStackScreen`
- `IncomingCallScreen`
- `InCallScreen`
- `HomeScreen`
- `CustomAppScreen`

Screen modules implement screen-level layout and behavior by composing atomic modules.

## Atomic modules

- `StatusBar`
- `Header`
- `MessageBubble`
- `NotificationCard`
- `Avatar`
- `Keyboard`
- `Clock`
- `AppIcon`
- `TypingText`

Atomic modules remain reusable across screen types. For example, `StatusBar` can serve lock, chat, home, and custom app screens.

## Contract and rules

```text
ScreenModuleInput<TData, TConfig> → RenderableNode
AtomicModule(resolvedProps, frame) → RenderableNode
```

- Inputs contain `module_id`, `module_schema_version`, validated module data/config, merged theme-mode tokens, device/runtime context, and resolved assets/icons.
- Modules never access SQL or fetch production data.
- Screen modules compose atomic modules rather than duplicating them.
- Output is renderable by the selected implementation adapter.
- Behavior must be deterministic for a given props/frame pair.
- Each module should be testable in isolation with fixtures, without a database.
- Unsupported module IDs or schema versions fail before rendering and must not mutate stored data.

Tool-specific rendering primitives may sit behind module or renderer adapters, but the module contract and resolved input model must remain portable.

Module-specific persistent animation/behavior rules live in module JSON. Resolvers provide resolved timings/events/config; modules own their interpretation and visual behavior. See `09_foundational_module_contracts.md` for the full boundary.
