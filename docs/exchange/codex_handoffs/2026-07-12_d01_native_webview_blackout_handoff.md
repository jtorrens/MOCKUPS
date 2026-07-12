# Handoff: D01 native WebView / shell blackout

Date: 2026-07-12  
From: desktop editor UX/UI implementation thread  
To: WebView / Preview lifecycle debugging thread  
Current `main`: `18de46ae`  
Original UX baseline: `862c7f1c`  
Platform reproduced: macOS  
Status: diagnosis accepted; implementation intentionally stopped

## Requested outcome

Diagnose and correct the shared native-host lifecycle defect that can make the
Avalonia navigation/editor shell disappear into black while preview-host content
remains visible during a context transition.

Do not hide the symptom with a global spinner or opaque loading mask. The
required invariant is:

> The previous composed shell remains visible until the replacement shell and
> preview context are ready. No full-window black frame may occur across 20
> consecutive representative actions, including an action originating inside
> the Preview surface that opens/selects another editor context.

This work must complete before the UX/UI thread resumes D05 contextual preview
states or any animation work.

## Repository state and related commits

- `862c7f1c` — accepted UX/UI audit and implementation plan baseline.
- `2c46a90b` — initial D01 clean-start diagnosis.
- `18de46ae` — diagnosis updated after the failing Preview-originated journey was
  reproduced.
- There is no retained D05 implementation and no uncommitted D01 code.
- `artifacts/` and `data/window-state.json` are unrelated local files and must
  remain outside commits.

Primary diagnosis:

- `docs/audits/2026-07-12_d01_desktop_editor_navigation_blackout_diagnosis.md`

Required architecture context remains the repository `AGENTS.md` reading list,
especially:

- `docs/architecture/26_desktop_preview_pipeline.md`;
- `docs/architecture/28_static_preview_update_experience.md`;
- `docs/architecture/24_desktop_preview_component_architecture.md`.

## What was observed

### Original audit symptom

The UX audit recorded repeated 2-4 second intervals in which navigation and
editor panels disappeared into black while WebView content remained visible.
The shell later returned without an application crash.

### Clean-start actions that did not reproduce it

After stopping all shell/renderer processes and starting `npm run app` from
`862c7f1c`:

| Journey | Count | Capture delay | Result |
| --- | ---: | ---: | --- |
| Design ↔ Production | 20 actions | 80 ms | no black frame |
| Component Classes / Components expand-collapse | 20 actions | 80 ms | no black frame |
| Design ↔ Production with settle | repeated | 1 s | no black frame |

Design `Chat` and Production `Episode 1` selection were restored correctly in
that matrix. This is why workspace switching alone is insufficient as a future
acceptance test.

### Journey that reproduced it

The failing boundary was discovered while prototyping the authorized D05 state
`Episode 1 no tiene preview directo` with a deterministic action
`Ver elementos renderizables`.

Two variations were exercised:

1. **WebView navigation variation**
   - The empty-state action used a custom URL inside `NativeWebView`.
   - `NavigationStarted` intercepted the URL and cancelled navigation.
   - On activation, the Avalonia shell disappeared into black and WebView-host
     content remained visible.
   - Returning to another workspace/context forced a later recomposition and
     restored the shell.
   - This variation was removed immediately and was never committed.

2. **Native action / context-selection variation**
   - The WebView was hidden for a non-renderable state.
   - A native Avalonia button was placed in the shared preview pane.
   - Its action selected the first deterministic renderable descendant through
     the existing generic selection callback.
   - Activating the context transition again produced the black native shell.
   - This variation was also fully removed and never committed.

The second variation means the defect is not limited to a bad custom URL. The
common factor is a Preview-originated action followed by selection, Preview host
visibility/layout changes and shell recomposition.

No exception appeared in the running terminal and the process remained alive.
The existing preview debug log did not contain enough native-layout information
to correlate the black interval.

## Important implementation observations

### Native airspace is already a known constraint

`spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs` explicitly documents
that `NativeWebView` owns platform airspace and paints above Avalonia siblings.
Raster playback therefore hides the WebView while the native bitmap surface is
active.

Relevant existing operations include:

```text
HideRasterFrame
  -> native raster frame hidden
  -> WebView.IsVisible = true

PlayRasterFrames
  -> native raster frame shown
  -> WebView.IsVisible = false
```

`LoadHtml` calls `NativeWebView.NavigateToString(...)`. Initial, empty-payload
and incompatible-shell routes still use full document loads.

### Workspace transition is currently two-step

`MainWindow.SetWorkspace` currently performs:

```text
persist workspace
-> EditorPreviewController.SetWorkspace(workspace)
   -> Refresh() immediately
-> update workspace buttons
-> LoadProjectTree()
   -> select new workspace node
   -> ShowNode()
   -> rebuild native editor
   -> refresh Preview again
```

The first Preview refresh can therefore see the new workspace with the old
selected node. This creates an intermediate payload/host/layout state before the
new selection exists.

### Native editor replacement is not atomic

`MainWindow.ShowNode` delegates to the shared editor content builder.
`EditorCardHostController.Clear()` performs:

```csharp
_cards.Clear();
_wrappers.Clear();
_host.Children.Clear();
```

Replacement cards are added afterward. Navigation rebuilds follow an analogous
replace-in-place pattern. If the UI thread or native host layout is delayed, the
previous native content is already gone.

### Preview logs cover web commits, not native composition

The current log can report renderer duration, full-load/DOM-patch choice,
assets, patch commit and retained-last-good behavior. It does not currently
record:

- window, preview host or `NativeWebView` bounds;
- WebView adapter/native handle visibility;
- Avalonia layout/render commit after selection;
- navigation/editor child counts before and after replacement;
- focus owner;
- a correlation id shared by selection, native layout and preview request.

Therefore renderer logs alone cannot determine whether the black interval is an
oversized/stale airspace surface or an empty Avalonia surface underneath it.

### Web-message detail learned during the discarded experiment

The Avalonia WebView package injects `invokeCSharpAction(data)` after navigation
completion on macOS; internally it posts to the configured WKScript message
handler and raises `WebMessageReceived`. Generic preview actions should use that
message route if a future web-resident state needs to call the host. They must
not navigate to custom action URLs. This is secondary to D01: even the native
button variation reproduced the broader context-transition defect.

## Working diagnosis

The failing boundary is:

```text
Preview-originated action
-> selected editor/preview context changes
-> native editor/navigation replacement and Preview refresh overlap
-> NativeWebView visibility, document or native bounds participate in layout
-> platform airspace remains composed while Avalonia replacement is absent or
   has not committed
-> user sees black shell with surviving preview-host content
```

The evidence supports two closely related candidates that must be instrumented
before choosing the correction:

1. **Stale or oversized native-host bounds.** The platform WebView keeps or
   temporarily receives incorrect bounds during the parent layout transition
   and covers the shell.
2. **Non-atomic shell replacement underneath airspace.** Native editor/tree
   children are cleared before replacements are measured, while Preview refresh
   or WebView visibility/layout prevents a timely Avalonia frame commit.

A third contributing factor is the two-step workspace/selection refresh, which
creates an unnecessary intermediate context and can amplify either candidate.

## Proposed diagnostic instrumentation

Add a generic correlation id for every workspace, selection, expansion/open and
Preview-originated navigation transaction. Use the existing debug log plumbing;
do not add component-specific logging.

Record monotonic timestamps for:

1. action received, source (`tree`, `workspace`, `preview-context`) and requested
   target id;
2. previous/new workspace and selected node ids;
3. Preview refresh requested and payload kind/identity or empty state;
4. editor/navigation host child counts before clear/build/swap;
5. `MainWindow`, shell columns, Preview host and `NativeWebView` bounds;
6. WebView visibility, adapter availability and whether document navigation was
   requested;
7. first Avalonia `LayoutUpdated` after the transaction;
8. first dispatcher/render callback after replacement;
9. Preview full-load/DOM-patch/retained-last-good commit;
10. keyboard focus owner before and after;
11. total UI-thread time for database load, native control construction and
    layout commit.

Capture the bounds at least at action start, immediately before native content
replacement, immediately after replacement and at the first layout callback.
The key proof is whether the WebView/native host covers the window or the
surrounding Avalonia hosts are genuinely empty during the black interval.

Instrumentation must not hide, clear or replace any surface and should be easy
to remove or keep behind the existing preview debug facility.

## Proposed correction

Implement only after the measurements identify the failing checkpoint. The
preferred correction shape is an atomic generic context transaction:

```text
receive requested workspace/selection
-> resolve the final selection and Preview context first
-> build replacement navigation/editor content off-host or in a retained
   candidate container
-> keep the previous composed shell and resident Preview visible
-> commit native selection/editor/navigation together on the UI thread
-> allow layout to establish final Preview-host/WebView bounds
-> issue one Preview refresh for the final context
-> dispose/remove the old native surface only after commit
```

Specific recommendations:

1. **Remove the intermediate workspace Preview refresh.** Let
   `MainWindow.SetWorkspace` establish the new workspace and selected node, then
   call one Preview refresh. `MainWindow` remains orchestration-only; the
   transaction mechanics should live in a shared shell controller.
2. **Do not clear visible editor/navigation hosts first.** Build generic
   replacement content in a detached/candidate host, then swap once ready. If
   Avalonia cannot safely detach/reparent these controls, retain two generic host
   layers and switch only after the candidate has measured.
3. **Stabilize NativeWebView bounds across the swap.** Keep the resident WebView
   attached and at its last valid bounds unless the final context truly changes
   preview surface type. Apply visibility/bounds changes only after the parent
   layout has valid nonzero dimensions.
4. **Never use document navigation for shell actions.** Use the host message
   channel for web-resident actions. Navigation interception must not become a
   control-communication mechanism.
5. **Preserve last-good Preview behavior.** Do not regress resident static DOM
   patches, asset/font commit ordering, stale-work suppression or Production
   global/local frame resolution.
6. **Do not add a global loading mask.** If later UX needs progress, it must be
   local to Preview and the previous resolved preview must remain visible.

If measurements prove the primary defect is in Avalonia's native control host or
the WebView package rather than application ordering, contain the workaround in
the shared generic WebView host. Document the platform condition and validate it
on Windows before merging.

## Boundaries that must remain unchanged

- `MainWindow` remains shell-only; it may orchestrate but not implement Preview
  internals or editor-specific behavior.
- No component names or component-specific branches in shared shell, bridge,
  helpers or web renderer.
- No changes to component contracts, Preview payload contracts or Production
  transport controls/units/scopes unless a separately reviewed defect proves
  they are necessary.
- No animation work.
- No compatibility fallback or silent data rewrite.
- No changes to `data/desktop-editor-spike.sqlite` or parity assets are expected.
- Preserve `artifacts/` and `data/window-state.json` as unrelated local changes.

## Required verification matrix

### Reproduction and continuity

- 20 alternating Design/Production switches.
- 20 Component Classes/Components expansion and selection actions.
- 20 Preview-originated deterministic context actions alternating with tree
  selection.
- Episode → Shot → Screen and back, including a non-renderable Episode owner.
- Component class → protected Default variant → ordinary variant and back.
- A renderable context with a resident WebView followed by a non-renderable
  context and then another renderable context.

For every action:

- no full-window black frame;
- previous shell stays composed until replacement commit;
- previous valid preview remains visible while a new render is pending;
- selected node and active workspace are correct;
- focus moves predictably or is restored;
- no intermediate old-workspace/new-workspace payload is presented.

### Preview lifecycle regression checks

- compatible static changes still use resident DOM patch;
- first load and genuinely incompatible shells may still full-load without
  covering unrelated native panels;
- failed render retains last good preview;
- raster playback still hides native WebView only for the documented playback
  interval and restores it afterward;
- stopped Production navigation remains a static update;
- Design Test Values never leak into Production.

### Platforms and layouts

- macOS is mandatory because the defect is reproduced there.
- Windows native-host parity must be checked before closing the phase.
- normal and narrow three-panel widths;
- Light and Dark;
- keyboard-only activation of the Preview-originated action.

### Automated checks

Run and report:

```text
npm run check:architecture
npm run test
git diff --check
```

Add focused tests for transaction ordering where possible: one final selection
before Preview refresh, no visible-host clear before candidate commit, and no
Preview-originated action implemented as WebView navigation.

## Expected delivery back to the UX/UI thread

Please return:

1. measured root cause with correlated lifecycle timestamps and bounds;
2. focused shared-lifecycle correction commit;
3. before/after reproduction matrix, including Preview-originated actions;
4. macOS result and Windows parity result or an explicit unavailable-platform
   limitation;
5. confirmation that Preview/component/Production contracts and transport were
   unchanged;
6. `npm run check:architecture` and `npm run test` results;
7. exact commit on `main`.

Once integrated, the UX/UI thread will pull `main`, rerun the visual invariant
and resume Phase 2 D05 from a clean implementation rather than restoring the
discarded prototype.
