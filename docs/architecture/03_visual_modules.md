# Visual modules

Visual modules are independent code modules or classes selected through a registry. A module receives resolved JSON props plus a frame and returns a renderable animated graphic. It does not know SQL, query the database, or fetch production data directly.

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
MessageBubble(resolvedProps, frame) → renderable
StatusBar(resolvedProps, frame) → renderable
ChatScreen(resolvedProps, frame) → renderable composed from atomic modules
```

- Inputs are fully resolved JSON props and the requested frame.
- Modules never access SQL or fetch production data.
- Screen modules compose atomic modules rather than duplicating them.
- Output is renderable by the selected implementation adapter.
- Behavior must be deterministic for a given props/frame pair.
- Each module should be testable in isolation with fixtures, without a database.

Tool-specific rendering primitives may sit behind module or renderer adapters, but the module contract and resolved input model must remain portable.
