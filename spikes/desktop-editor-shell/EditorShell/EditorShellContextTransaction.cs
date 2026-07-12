using Avalonia.Controls;
using Avalonia.Threading;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Diagnostics;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorShellContextTransaction : IDisposable
{
    private readonly string _id = Guid.NewGuid().ToString("N")[..12];
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly Control _window;
    private readonly Panel _navigationHost;
    private readonly Panel _editorHost;
    private readonly Control _previewHost;
    private readonly Func<string> _nativePreviewState;
    private readonly IDisposable _correlation;
    private bool _disposed;

    public EditorShellContextTransaction(
        string source,
        string targetId,
        string previousId,
        string workspace,
        Control window,
        Panel navigationHost,
        Panel editorHost,
        Control previewHost,
        Func<string> nativePreviewState)
    {
        _window = window;
        _navigationHost = navigationHost;
        _editorHost = editorHost;
        _previewHost = previewHost;
        _nativePreviewState = nativePreviewState;
        _correlation = PreviewDebugLog.BeginCorrelation(_id);
        Write("shell.context.start", source, targetId, previousId, workspace);
    }

    public void Checkpoint(string phase)
    {
        PreviewDebugLog.Write(
            "shell.context.checkpoint",
            ("phase", phase),
            ("elapsedMs", _stopwatch.Elapsed.TotalMilliseconds),
            ("navigationChildren", _navigationHost.Children.Count),
            ("editorChildren", _editorHost.Children.Count),
            ("windowBounds", Bounds(_window)),
            ("previewBounds", Bounds(_previewHost)),
            ("nativePreview", _nativePreviewState()));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Checkpoint("synchronous-commit");
        var transactionId = _id;
        var elapsed = _stopwatch.Elapsed.TotalMilliseconds;
        _ = Dispatcher.UIThread.InvokeAsync(
            () => PreviewDebugLog.Write(
                "shell.context.render-commit",
                ("transactionId", transactionId),
                ("elapsedMs", elapsed),
                ("navigationChildren", _navigationHost.Children.Count),
                ("editorChildren", _editorHost.Children.Count),
                ("windowBounds", Bounds(_window)),
                ("previewBounds", Bounds(_previewHost)),
                ("nativePreview", _nativePreviewState())),
            DispatcherPriority.Render);
        _correlation.Dispose();
    }

    private void Write(string eventName, string source, string targetId, string previousId, string workspace)
    {
        PreviewDebugLog.Write(
            eventName,
            ("source", source),
            ("targetId", targetId),
            ("previousId", previousId),
            ("workspace", workspace),
            ("navigationChildren", _navigationHost.Children.Count),
            ("editorChildren", _editorHost.Children.Count),
            ("windowBounds", Bounds(_window)),
            ("previewBounds", Bounds(_previewHost)),
            ("nativePreview", _nativePreviewState()));
    }

    private static string Bounds(Control control) =>
        $"{control.Bounds.X:0.##},{control.Bounds.Y:0.##},{control.Bounds.Width:0.##},{control.Bounds.Height:0.##}";
}
