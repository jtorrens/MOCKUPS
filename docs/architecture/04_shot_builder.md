# ShotBuilder

`ShotBuilder` composes resolved screen instances into a final shot frame. It controls instance timing, layer order, visibility, and transforms. It does not draw status bars, message bubbles, notifications, or other UI details; those belong to visual modules.

Each screen instance can reference a screen template, owner actor, device, theme, `dataRef`, props/overrides, timing, and transforms. Instances may use different resources and may appear sequentially or overlap. This allows one shot to show, for example, lock screen → notification → unlock → chat, or multiple screens at once.

For a requested frame, the builder:

1. Selects active screen instances from their timing ranges.
2. Applies layer order and instance transforms.
3. Passes each instance's resolved props and local frame to its registered screen module.
4. Composites returned renderables into the shot frame.

Example:

```text
Shot 010_020_A
 ├─ ScreenInstance: LockScreen, frames 0–150
 ├─ ScreenEvent: Notification appears at frame 75
 └─ ScreenInstance: ChatScreen, frames 150–300
```

The notification event belongs to its parent screen instance and affects that instance's resolved frame state. At a boundary or overlap, explicit timing and layer rules determine which instances are visible.
