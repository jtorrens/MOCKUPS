# D01 desktop editor navigation blackout diagnosis

Date: 2026-07-12  
Baseline: `main` at `862c7f1cca50dd9f8832acc1a10a6e3377faa9dd`  
Platform exercised: macOS  
Status: reproduced diagnosis gate; no lifecycle or UX code changed

## Scope and invariant

This diagnosis covers the native editor blackout reported in the desktop UX/UI
audit. It does not cover animation and does not authorize a global spinner,
blanket loading overlay, component-contract change, Preview contract change or
Production transport change.

The acceptance invariant remains:

> The previously composed native shell remains visible until its replacement is
> ready. Twenty consecutive representative workspace, expansion and selection
> actions must produce no full-window black frame, while selection and focus
> continuity are preserved.

## Clean-start procedure

1. Pulled `main` with `--ff-only`; the branch was already current.
2. Confirmed starting commit `862c7f1c`.
3. Preserved the unrelated untracked `artifacts/` directory and
   `data/window-state.json`.
4. Stopped the existing desktop shell, renderer and preview-server processes.
5. Started a clean `npm run app` instance.

## Reproduction matrix

| Journey | Actions | Observation point | Result |
| --- | ---: | ---: | --- |
| Design to Production and back | 20 | capture 80 ms after each action | no black frame |
| Component Classes / Components expand-collapse | 20 | capture 80 ms after each action | no black frame |
| Workspace switch after 1 s settle | repeated | capture after settle | native navigation/editor remained composed |
| Selection continuity | workspace round trips | after each return | prior Design `Chat` and Production `Episode 1` selections restored |
| WebView continuity | workspace round trips | preview log and visible host | no renderer failure correlated with the actions |

The initial clean-start matrix did not reproduce the 2-4 second blackout from
`2026-07-12_desktop_editor_ux_ui_visual_audit.md`. The failure was reproduced
later while validating the next preview-state phase: an action originating in
the preview surface requested a selection/context transition. The native shell
disappeared into black while preview-host content remained visible. Replacing a
WebView navigation action with a native Avalonia button removed document
navigation from the path, but the resulting context-selection/recomposition
still reproduced the blackout. Windows parity was not available in this
environment and remains unverified.

This follow-up is important: ordinary workspace and tree actions are not a
sufficient reproduction matrix. A representative D01 run must also include a
preview-originated action that selects or opens another editor context.

## What the current evidence establishes

- The original audit observed the native navigation/editor surface disappear
  while WebView content remained visible. That symptom is consistent with a
  native-host composition or platform-airspace incident, not a TypeScript
  render failure.
- `NativeWebView` is a platform airspace surface. The code already documents
  that it paints above Avalonia siblings during raster playback.
- Workspace changes currently request preview work before the new workspace
  selection has been loaded: `MainWindow.SetWorkspace` calls
  `EditorPreviewController.SetWorkspace`, whose `Refresh()` runs immediately,
  and only then calls `LoadProjectTree()` and `ShowNode()`.
- `ShowNode()` synchronously rebuilds native editor content. The shared card host
  clears its current children before it adds the replacement cards.
- Navigation rebuilds use the same replace-in-place pattern.
- Preview logs describe renderer/WebView updates, but there are no matching
  native-shell lifecycle events for workspace transition start, old/new
  selection, card-host clear/build/commit, layout pass, focus owner or native
  WebView bounds. Existing logs cannot prove whether the observed blackout was
  caused by an empty native host, a delayed layout pass or incorrect airspace
  bounds.

These points establish the failing boundary: a preview-host interaction followed
by native selection/recomposition can leave the platform WebView/native-host
airspace composed while the surrounding Avalonia shell has not committed its
replacement frame. The remaining diagnostic work must distinguish whether the
decisive fault is stale/oversized native-host bounds or the non-atomic Avalonia
replacement beneath it. Changing either shared lifecycle is a deep change and
requires review.

## Candidate causes to distinguish

1. **Non-atomic native replacement.** Editor/navigation children are removed
   before their replacements are measured and attached.
2. **Two-step workspace refresh.** Preview refresh first sees the new workspace
   with the old selection, then refreshes again after the new selection is
   loaded; the intermediate state may force unnecessary host/layout work.
3. **NativeWebView airspace bounds.** During a parent layout transition, the
   platform view may temporarily retain stale or oversized bounds and cover the
   Avalonia surface.
4. **UI-thread blockage.** Synchronous database/layout/control construction may
   delay native recomposition after the old children have been removed.

The eventual correction depends on which checkpoint fails. These candidates
must not be collapsed into a spinner workaround.

## Required instrumentation before a fix

Add one generic, removable diagnostic transaction around workspace, expansion
and selection changes. Record monotonic timestamps and correlation ids for:

- requested workspace/action and previous/new selected node ids;
- native navigation/editor child counts before build, after candidate build and
  after committed replacement;
- layout updated/rendered callback after the replacement;
- window, preview host and `NativeWebView` bounds and visibility;
- keyboard focus owner before and after the transaction;
- preview request start/commit, using the same correlation id;
- UI-thread elapsed time for database load, editor build, navigation build and
  layout commit.

The diagnostic must not clear, hide or replace the existing surface and must not
change component, Preview or Production contracts.

## Proposed correction shape, pending reproduced evidence

If instrumentation confirms non-atomic native replacement, build the generic
navigation/editor replacement off-host and swap it only after it is ready for
layout, retaining the previous host until commit. If it confirms the two-step
workspace refresh, introduce one shared workspace-selection transaction and
issue preview refresh only after the new selection is established. If it
confirms stale native-view bounds, correct the generic native preview host's
layout/clip lifecycle without adding component knowledge or a global overlay.

Any of these changes materially affects shared shell lifecycle behavior and
therefore requires review before implementation.

## Gate decision

D01 is stopped at diagnosis. The clean-start workspace/tree matrix is green,
but preview-originated context selection reproduces the audited blackout and
current logging cannot distinguish native-host bounds from non-atomic shell
replacement. Do not implement a lifecycle correction until the user accepts
the instrumentation and shared-lifecycle correction step. Do not advance to
animation or continue D05 on top of the unstable transition.
