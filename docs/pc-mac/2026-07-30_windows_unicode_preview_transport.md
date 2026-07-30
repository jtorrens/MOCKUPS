# Windows Unicode Preview transport handoff

**Date:** 2026-07-30  
**Reported from:** Windows PC validation  
**Repository:** `main` at `ebba9358 Add detachable Preview controls`  
**Scope:** diagnosis only. No application code, parity data, or database was changed on this PC.

## Symptom

The Windows desktop Preview does not preserve or render accented characters and
emoji consistently. This is separate from the previously reported resident
Preview commit timeout.

## Status of the earlier Preview timeout

The previous WebView2 result-normalization correction is active in the rebuilt
Windows executable. The current log records successful DOM patch commits in
approximately 12–70 ms:

```text
preview.webview.dom-patch.wait  patch=1151  result=commit  ms=58.997
preview.webview.update          route=dom-patch
```

There are no current five-second `body-commit-failed` timeouts. Do not conflate
the Unicode problem with the resolved WebView2 patch-status problem.

## Confirmed facts

1. The committed SQLite database contains Unicode text; querying it through
   Node and `better-sqlite3` returns accented content.
2. The Preview request is serialized by .NET and passed to the persistent Node
   renderer over redirected standard input.
3. The persistent Node renderer reads the request with `readline` from
   `process.stdin`, parses JSON, renders HTML, and returns JSON on standard
   output.
4. Windows uses a code page by default for redirected child-process streams
   unless the .NET `ProcessStartInfo` explicitly sets an encoding.

## Root cause

`DesktopChildProcess.CreateHiddenStartInfo` enables redirected standard output
and standard error, but sets no explicit text encoding. The persistent Preview
renderer subsequently enables redirected standard input and sends JSON through:

```csharp
await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(...));
```

No `StandardInputEncoding`, `StandardOutputEncoding`, or
`StandardErrorEncoding` is configured.

On macOS this generally works because UTF-8 is the normal process locale. On
Windows, the redirected stream may instead use the current legacy code page.
Node treats standard input/output as UTF-8. Consequently, characters outside
the active Windows code page are corrupted before Node parses the JSON or when
the generated HTML returns to the host:

- accented characters can become mojibake;
- emoji, which cannot be represented in legacy code pages, are lost or replaced;
- the failure is specific to text transport, not the database, renderer logic,
  asset resolution, or WebView DOM patching.

## Relevant implementation

- `src/Mockups.Application/DesktopChildProcess.cs`
  - `CreateHiddenStartInfo` redirects output/error but does not configure UTF-8.
- `src/Mockups.Desktop/EditorShell/WebDesignPreviewRenderer.cs`
  - persistent renderer writes the request JSON to `StandardInput` and reads the
    response from `StandardOutput`.
- `src/desktop-preview/renderDesignPreviewHtmlServer.ts`
  - Node reads `process.stdin` and writes the response to `process.stdout`.

## Required correction

Set UTF-8 explicitly at the shared child-process boundary. The shared helper is
the appropriate owner; do not add an encoding workaround only to one Preview
call site.

At minimum, `ProcessStartInfo` for Node-backed tools must define:

```csharp
StandardInputEncoding = Encoding.UTF8
StandardOutputEncoding = Encoding.UTF8
StandardErrorEncoding = Encoding.UTF8
```

The helper must add `using System.Text;`. Confirm the settings are valid for all
callers of `DesktopChildProcess.CreateHiddenStartInfo`, including the Preview
renderer and icon-theme scripts.

Keep JSON and HTML as Unicode strings end-to-end. Do not attempt to replace
emoji, HTML-encode text as a transport workaround, or change the SQLite data.

## Windows validation after the fix

1. Rebuild `out\\desktop\\win-x64\\Mockups.Desktop.Host.exe`.
2. Launch the rebuilt executable.
3. Enter and display Spanish text containing at least `áéíóúüñ¿¡`.
4. Enter and display an emoji such as `😀`, plus a multi-code-point emoji such
   as `👨‍👩‍👧‍👦`.
5. Change Preview selection repeatedly and confirm the exact text remains in the
   editor and Preview after each update.
6. Verify `logs\\desktop-preview-debug.log` continues to report
   `result=commit` without `body-commit-failed`.
7. Run the relevant validation suite and publish the Windows executable again.

## Evidence retained on the PC

- `logs/desktop-preview-debug.log` (ignored local diagnostic log)
- `data/mockups.sqlite` (read only during this diagnosis)
