using Avalonia.Threading;
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
        _operations.ActivityChanged += OnActivityChanged;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _operations.ActivityChanged -= OnActivityChanged;
        _active.Clear();
        _scrim.Hide();
    }

    private void OnActivityChanged(EditorOperationActivity activity)
    {
        Dispatcher.UIThread.Post(
            () => Apply(activity),
            DispatcherPriority.Input);
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

        var current = _active
            .OrderByDescending((entry) => entry.Key)
            .Select((entry) => entry.Value)
            .FirstOrDefault();
        if (current is null)
        {
            _scrim.Hide();
            return;
        }
        _scrim.Show(current, cancel: null);
    }
}
