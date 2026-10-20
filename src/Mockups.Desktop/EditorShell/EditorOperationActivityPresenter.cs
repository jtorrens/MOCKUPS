using Avalonia.Threading;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorOperationActivityPresenter : IDisposable
{
    private readonly EditorOperationCoordinator _operations;
    private readonly EditorLoadingScrim _scrim;
    private readonly Dictionary<long, string> _active = [];
    private bool _disposed;

    public EditorOperationActivityPresenter(
        EditorOperationCoordinator operations,
        EditorLoadingScrim scrim)
    {
        _operations = operations;
        _scrim = scrim;
        EditorModalWindowScope.ModalActivityChanged += Refresh;
        _operations.ActivityChanged += OnActivityChanged;
        _operations.OperationMeasured += OnOperationMeasured;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _operations.ActivityChanged -= OnActivityChanged;
        _operations.OperationMeasured -= OnOperationMeasured;
        EditorModalWindowScope.ModalActivityChanged -= Refresh;
        _active.Clear();
        _scrim.Hide();
    }

    private static void OnOperationMeasured(EditorOperationMeasurement measurement) =>
        PreviewDebugLog.Write("editor.operation", ("name", measurement.Name),
            ("queueMs", measurement.QueueMs), ("workMs", measurement.WorkMs),
            ("success", measurement.Succeeded));

    private void OnActivityChanged(EditorOperationActivity activity)
    {
        if (Dispatcher.UIThread.CheckAccess()) Apply(activity);
        else Dispatcher.UIThread.Post(() => Apply(activity), DispatcherPriority.Input);
    }

    private void Apply(EditorOperationActivity activity)
    {
        if (_disposed)
        {
            return;
        }
        if (activity.IsActive)
        {
            _active[activity.Id] = activity.Message;
        }
        else
        {
            _active.Remove(activity.Id);
        }

        Refresh();
    }

    private void Refresh()
    {
        if (_disposed) return;
        var current = _active
            .OrderByDescending((entry) => entry.Key)
            .Select((entry) => entry.Value)
            .FirstOrDefault();
        if (current is null || EditorModalWindowScope.IsModalActive)
        {
            _scrim.Hide();
            return;
        }
        _scrim.Show(current, cancel: null, takeFocus: false);
    }
}
