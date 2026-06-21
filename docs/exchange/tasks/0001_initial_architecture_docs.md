# Codex Task 0001 — Initial architecture documentation

## Goal

Create the initial architecture documentation for the `MOCKUPS` project.

This task is documentation-only. Do not implement application code yet.

The project is a production-scoped system for generating final-render animated phone/app screens for audiovisual shots. It started from a chat JSON generator, but the architecture must support multiple screen types: chat, lock screen, notifications, incoming call, in-call, home screen, custom apps, and future screen types.

## Core architecture decisions

* Production is the root entity.
* Shot is the central render unit.
* A shot contains one or more screen instances.
* A screen instance references:

  * screen template / visual module
  * owner actor
  * device
  * theme
  * dataRef
  * timing
  * transform
  * props / overrides
* Chat is only one screen type, not the root concept.
* SQL stores stable relationships.
* JSON stores flexible visual props, theme tokens, device metrics, event payloads and module configs.
* Visual modules are independent code modules/classes.
* Visual modules receive resolved JSON props.
* Visual modules must not query the database directly.
* Resolvers convert DB/JSON data into resolved props.
* ShotBuilder composes screen instances into the final shot render.
* Renderer converts resolved visual output into frames/video.
* The architecture should remain independent from the final implementation tool: Remotion, Electron, Canvas, AE/Fusion export, etc.

## Create these files

```text
docs/architecture/00_project_vision.md
docs/architecture/01_data_model.md
docs/architecture/02_render_architecture.md
docs/architecture/03_visual_modules.md
docs/architecture/04_shot_builder.md
docs/architecture/05_decisions_log.md
docs/architecture/06_codex_workflow.md
PROJECT_STATUS.md
```

## Content requirements

Keep the docs concise, implementation-oriented and easy for Codex to read in future tasks.

---

## 00_project_vision.md

Explain the project in practical terms:

* It generates animated diegetic phone/app screens for shots.
* A production contains reusable resources.
* A shot instantiates one or more screens.
* Screen types include chat, lock screen, notifications, calls, home screens and custom apps.
* The system should support style packs, device packs and actor profiles.
* The same underlying data model should be usable regardless of the final renderer.

---

## 01_data_model.md

Describe the main entities:

* productions
* shots
* screen_templates
* screen_instances
* screen_events
* themes
* devices
* device_states
* actors
* apps
* media_assets
* animation_presets
* render_presets
* conversations
* messages
* notifications
* calls
* data_sources

Explain that SQL should hold relationships and stable fields, while JSON should hold flexible configuration.

Required conceptual hierarchy:

```text
Production
 ├─ Resources
 │   ├─ Themes
 │   ├─ Devices
 │   ├─ DeviceStates
 │   ├─ Actors
 │   ├─ Apps
 │   ├─ Assets
 │   ├─ AnimationPresets
 │   ├─ RenderPresets
 │   └─ ScreenTemplates
 │
 ├─ Data
 │   ├─ Conversations
 │   │   └─ Messages
 │   ├─ Notifications
 │   ├─ Calls
 │   └─ CustomDataSources
 │
 └─ Shots
     └─ ScreenInstances
         └─ ScreenEvents
```

Clarify that a shot should not be tied to a single chat or a single device. A shot may contain multiple screen instances, sequentially or overlapping.

---

## 02_render_architecture.md

Explain the render flow:

```text
Production DB / JSON
  ↓
ShotResolver
  ↓
ScreenInstanceResolver
  ↓
ResolvedProps
  ↓
VisualModuleRegistry
  ↓
Screen modules
  ↓
Atomic visual components
  ↓
ShotBuilder composition
  ↓
Renderer / export
```

The central operation should be:

```text
renderShot(productionId, shotId, frame)
```

not:

```text
renderChat(chatId)
```

Explain that preview and final render should ideally share the same resolved props and visual modules. The final render must be frame-based and deterministic.

---

## 03_visual_modules.md

Explain the visual module system.

Screen modules:

* ChatScreen
* LockScreen
* NotificationStackScreen
* IncomingCallScreen
* InCallScreen
* HomeScreen
* CustomAppScreen

Atomic modules:

* StatusBar
* Header
* MessageBubble
* NotificationCard
* Avatar
* Keyboard
* Clock
* AppIcon
* TypingText

Rules:

* Modules receive resolved JSON props.
* Modules do not know SQL.
* Modules do not fetch production data directly.
* Modules should be testable in isolation.
* A module receives props + frame and returns a renderable animated graphic.
* Screen modules compose atomic modules.
* Atomic modules should remain reusable across different screen types.

Example concept:

```text
MessageBubble(resolvedProps, frame) → renderable
StatusBar(resolvedProps, frame) → renderable
ChatScreen(resolvedProps, frame) → renderable composed from atomic modules
```

---

## 04_shot_builder.md

Explain:

* ShotBuilder composes screen instances.
* It does not draw individual UI details.
* It resolves timing, layer order and transforms.
* Multiple screens may appear in one shot.
* A shot may include lock screen → notification → unlock → chat.
* Screen instances can overlap or be sequential.
* A screen instance can reference a template, owner actor, device, theme, dataRef, props and transforms.

Example:

```text
Shot 010_020_A
 ├─ ScreenInstance: LockScreen, frames 0–150
 ├─ ScreenEvent: Notification appears at frame 75
 └─ ScreenInstance: ChatScreen, frames 150–300
```

---

## 05_decisions_log.md

Create accepted decisions using this format:

```md
## D001 — Production is the root entity

Status: accepted

Production is the root scope for resources, reusable presets, actors, devices, themes, assets, data and shots.

Implications:
- All reusable resources should belong to a production.
- Shots always belong to a production.
```

Create these decisions:

* D001 — Production is the root entity.
* D002 — Shot is the central render unit.
* D003 — Chat is only one screen type.
* D004 — SQL for stable relationships, JSON for flexible config.
* D005 — Visual modules do not access the database directly.
* D006 — Resolvers create resolved props.
* D007 — ShotBuilder composes screens but does not draw them.
* D008 — Modules receive props + frame and return renderables.
* D009 — The renderer should be frame-based and deterministic.

---

## 06_codex_workflow.md

Document the workflow:

* This repo is the source of truth.
* Architecture decisions go in `docs/architecture/05_decisions_log.md`.
* Current status goes in `PROJECT_STATUS.md`.
* Exchange tasks from ChatGPT to Codex go in `docs/exchange/`.
* Codex responses, summaries or handoffs can go in `docs/exchange/codex_responses/`.
* Each Codex task should be small and scoped.
* Codex should not change architecture without creating an Architecture Question.
* After each task, Codex should summarize changed files and update `PROJECT_STATUS.md`.

Include this rule:

```text
Codex may implement, adapt and detect conflicts.
Codex must not silently change architecture.
If architecture conflicts appear, Codex should stop and create an Architecture Question.
```

Recommended Codex response format:

```md
## Summary

## Files changed

## Questions / conflicts

## Tests

## Notes
```

---

## PROJECT_STATUS.md

Create this initial status file:

```md
# PROJECT_STATUS

## Current phase

Initial architecture documentation.

## Current implementation state

No application code implemented yet.

## Accepted architecture

- Production is root.
- Shot is render unit.
- Shots contain screen instances.
- Screen instances reference visual modules.
- Resolvers transform DB/JSON into resolved props.
- Visual modules are independent and receive props + frame.
- SQL stores stable relationships.
- JSON stores flexible visual/config data.

## Next recommended task

Define the initial database schema and TypeScript/Zod schemas, but only after the architecture docs are reviewed.
```

## Do not implement yet

Do not create:

* Electron app
* Remotion app
* renderer
* SQLite migrations
* UI
* export pipeline
* package dependencies

This task is documentation only.

## Acceptance criteria

* All requested architecture docs exist.
* `PROJECT_STATUS.md` exists.
* Docs reflect the architecture described in this task.
* No application code is implemented.
* No package dependencies are added.
* Codex final response summarizes files created.
* Codex identifies any architecture conflicts instead of inventing new decisions.
