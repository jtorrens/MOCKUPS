# Project vision

MOCKUPS generates final-render animated, diegetic phone and app screens for audiovisual shots. It is not a chat-only generator: supported screen types include chat, lock screens, notifications, incoming and active calls, home screens, custom apps, and future modules.

A production is the working scope and contains reusable resources such as actor profiles, style/theme packs, device packs, apps, app/screen tokens, animation presets, and render presets. Productions also contain editorial episodes, and episodes contain shots. A shot is the central render unit: the timeline of actions shown by a device screen. Each shot instantiates one or more screens, which may run sequentially or overlap.

The first local app shell follows this mental model. The Project workspace presents `Production → Episode → Shot → Screen` as the main editing hierarchy, while reusable resources live in a separate Library workspace.

The data and render architecture must remain independent of the final implementation tool. The same production, shot, screen-instance, and resolved-props model should support preview and output through Remotion, Electron, Canvas, AE/Fusion export, or another renderer.

The practical goal is repeatable production work: define reusable resources once, compose them per shot, preview the exact visual logic used for final output, and render deterministic frames.

MOCKUPS normally renders at device-screen resolution (optionally scaled by a render preset). Compositing the result into an external filmed/UHD plate is intentionally delegated to AE, Fusion, Resolve, Nuke, or similar software.
