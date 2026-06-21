# Project vision

MOCKUPS generates final-render animated, diegetic phone and app screens for audiovisual shots. It is not a chat-only generator: supported screen types include chat, lock screens, notifications, incoming and active calls, home screens, custom apps, and future modules.

A production is the working scope and contains reusable resources such as actor profiles, style/theme packs, device packs, apps, media assets, animation presets, render presets, and screen templates. A shot is the central render unit. Each shot instantiates one or more screens, which may run sequentially or overlap.

The data and render architecture must remain independent of the final implementation tool. The same production, shot, screen-instance, and resolved-props model should support preview and output through Remotion, Electron, Canvas, AE/Fusion export, or another renderer.

The practical goal is repeatable production work: define reusable resources once, compose them per shot, preview the exact visual logic used for final output, and render deterministic frames.
