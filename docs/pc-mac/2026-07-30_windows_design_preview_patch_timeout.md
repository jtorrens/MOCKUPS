# Windows design preview patch timeout

**Date:** 2026-07-30  
**Reported from:** Windows PC validation  
**Repository:** `main` at `7a669304 Add Windows PC test handoff`  
**Scope:** diagnosis only; no application code, parity data, or database was changed on this PC.

## User-visible failure

When selecting a Design item with a renderable Preview, the application reports:

```text
Design preview · InvalidOperationException:
The resident preview update could not be committed;
the last valid preview was retained.
```

The Preview correctly retains the previous valid content, but the next Preview
does not become visible. This happens for the Chat module and for individual
components such as Audio and Button.

## What succeeds

The failure is not in the Preview renderer or in the project data:

1. The persistent Node renderer starts and returns HTML successfully.
2. The generated Preview body is delivered to the resident WebView.
3. Preview assets are registered successfully.
4. `window.mockupsSetPreviewBody(...)` returns a valid numeric patch id.

Examples from the PC log:

```text
preview.renderer.persistent  id=1  htmlChars=37512  newAssets=12
preview.render.body          route=rendered  component=module.core.chat
preview.webview.dom-patch    success=true  patch=1  wait=true
```

The same failure also occurs for Button, whose update has no assets. This rules
out missing images, fonts, SVGs, database values, or the Node renderer as the
root cause.

## Exact failure path

The host waits up to five seconds for a resident DOM patch to report `commit`:

```text
ReplacePreviewBodyAsync
  -> WaitForPatchCommitAsync
  -> window.mockupsPreviewPatchStatus(patchId)
  -> compare returned value with `commit`
```

On this Windows PC, every update reaches the timeout:

```text
preview.webview.dom-patch.wait  patch=1  result=timeout  ms=5035.653
preview.webview.update          route=retain-last-good
                                 reason=body-commit-failed
```

No `preview.webview.patch-event` entries are recorded, despite the browser-side
code recording `request`, `commit`, `skip`, and `stale` events. This is the
important clue.

## Root cause

The Windows WebView2 implementation returns JavaScript string results as JSON
string literals. In practical terms, a browser return value of `commit` reaches
the C# side as `"commit"`, not `commit`.

`WaitForPatchCommitAsync` currently obtains the result with:

```csharp
status = result?.ToString() ?? "";
```

and then compares it literally with:

```csharp
if (status == "commit")
```

That comparison fails on Windows because the returned value still includes JSON
quoting. The corresponding event-drain code has the same boundary issue: the
browser returns `JSON.stringify(events)`, which reaches the host as a JSON
string literal rather than a JSON array. It is parsed as a scalar JSON string,
so no patch events are emitted to the debug log.

This explains all observed symptoms:

- numeric patch ids work because numbers do not require JSON string decoding;
- the patch is accepted by the browser but its completion is never recognized;
- no browser patch events appear in the host log;
- every resident update waits the complete five-second timeout;
- the safety path retains the prior valid Preview.

## Likely correction for the Mac task

Normalize string results returned by `WebView.InvokeScript` before comparing or
parsing them. The owner should use one shared helper at the WebView boundary,
not one-off trimming at individual call sites.

The helper should:

1. accept the raw `InvokeScript` result;
2. convert it to text;
3. attempt `JsonSerializer.Deserialize<string>(...)` when the result is a JSON
   string literal;
4. fall back to the original text when the result is already plain text;
5. preserve non-string JSON values such as numeric patch ids.

Apply that normalized value before:

- comparing `mockupsPreviewPatchStatus` with `commit`, `skip`, and `stale`;
- parsing `mockupsDrainPreviewPatchEvents` and
  `mockupsMissingPreviewAssets` as JSON arrays;
- consuming other JavaScript APIs that explicitly return `JSON.stringify(...)`.

Do not alter Preview timing or simply increase the five-second timeout: the
browser-side commit is being missed, not taking too long.

## Relevant files

- `src/Mockups.Desktop/EditorShell/WebPreviewPanes.cs`
  - `ReplacePreviewBodyAsync`
  - `WaitForPatchCommitAsync`
  - `DrainPreviewPatchEventsOnceAsync`
  - JavaScript definitions for `mockupsPreviewPatchStatus` and
    `mockupsDrainPreviewPatchEvents`
- `logs/desktop-preview-debug.log` on the Windows PC (local evidence; ignored)

## Reproduction on the Windows PC

1. Launch `out\\desktop\\win-x64\\Mockups.Desktop.Host.exe`.
2. In Design, open a renderable Module or Component.
3. Observe the error after approximately five seconds.
4. Choose another renderable Component (Button reproduces without image assets).
5. Inspect `logs\\desktop-preview-debug.log` for
   `reason=body-commit-failed` and `result=timeout`.

## Validation after the fix

On Windows, verify that:

1. a Chat Module Preview becomes visible;
2. Audio and Button component Previews update without the error;
3. the debug log records `preview.webview.dom-patch.wait` with `result=commit`;
4. the debug log records browser patch events;
5. repeated selection changes do not add five-second pauses;
6. existing Preview architecture and cross-platform behavior remain intact.
