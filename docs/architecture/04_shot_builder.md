# ShotBuilder

`ShotBuilder` composes resolved screen instances into a device-screen frame. A shot is the sequence of actions shown by the device screen, not a phone placed inside an external UHD/video plate. External placement belongs to later compositing software.

Shots belong to episodes, and episodes belong to productions. The shot is the unit selected for frame resolution; the episode is editorial organization and does not change frame math.

A shot has an owner actor. That actor supplies the default device and theme for the shot's screen instances through actor defaults. A screen instance references a screen module/template, device state, theme mode, module data/config/token overrides, timing, and transforms, and may still carry explicit context overrides when needed. Instances may use different resources and may appear sequentially or overlap. This allows one shot to show, for example, lock screen → notification → unlock → chat, or multiple screens at once.

Versioned `module_data_json`, `module_config_json`, and `module_tokens_override_json` are the module boundary. Chat no longer supports generic `dataRef`/props at runtime; other not-yet-refactored modules may still use legacy fields. Normal composition uses the device design space mapped to device render pixels.

For a requested frame, the builder:

1. Selects active screen instances from their timing ranges.
2. Applies layer order and instance transforms.
3. Converts the requested shot frame to `local_frame = shot_frame - start_frame`, then passes each instance's resolved props and local frame to its registered screen module.
4. Composites returned renderables into the shot frame.

Example:

```text
Shot 010_020_A
 ├─ ScreenInstance: LockScreen, frames 0–150
 ├─ ScreenEvent: Notification appears at frame 75
 └─ ScreenInstance: ChatScreen, frames 150–300
```

The notification event belongs to its parent screen instance and affects that instance's resolved frame state. At a boundary or overlap, explicit timing and layer rules determine which instances are visible.

Shot placement fields (`start_frame`, `end_frame`) use shot-frame coordinates. Screen-event and resolved narrative timings use screen-instance-local coordinates. Resolvers perform this conversion before a visual module runs so modules need no shot or database access.
