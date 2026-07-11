# Time units contract

The production contract uses one canonical unit per domain.

| Domain | Unit | Examples |
| --- | --- | --- |
| Shot/module timeline and authored animation events | frames | Shot duration, module duration, keyframes, Conversation timing, write-on and finite message playback |
| Physical audio/video position and source duration | seconds | `currentTimeSeconds`, `durationSeconds`, `targetSeconds` |
| Reusable UI motion, fades, blink and continuous effects | milliseconds | `durationMs`, `delayMs`, `motionElapsedMs`, `controlsElapsedMs`, `fadeDurationMs`, `blinkDurationMs` |

Frames require the effective Shot FPS. Reusable components and Theme motion
tokens must not depend on that FPS. The parent timeline converts its current
frame to elapsed milliseconds before invoking a motion resolver. Media source
position remains seconds because media metadata and decoders use physical time.

Field and runtime-input definitions carry a `unit` label (`frames`, `fps`, `s`
or `ms`). The shared dictionary control renders it beside the logical label.
Editors must not hardcode unit text into component-specific controls.

This migration has no compatibility aliases. Retired fields are invalid:

- `fadeFrames` -> `fadeDurationMs`;
- `cursorBlinkFrames` -> `cursorBlinkDurationMs`;
- `blinkFrames` -> `blinkDurationMs`;
- `motionTimeSeconds` -> `motionElapsedMs`;
- `textAnimationTimeSeconds` -> `textAnimationElapsedMs`;
- `composerTransitionTimeSeconds` -> `composerTransitionElapsedMs`.

Declarative preview actions support `frames`, `seconds` and `milliseconds`.
Their declared time field is written in that unit; scheduler conversions remain
internal and never change the payload contract.
