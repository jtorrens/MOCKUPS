using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class PreviewVisualContextDataSource
{
    private readonly SpikeDatabase _database;

    public PreviewVisualContextDataSource(SpikeDatabase database)
    {
        _database = database;
    }

    public IReadOnlyList<FieldOption> DeviceOptions(string projectId)
    {
        return _database.GetDeviceOptions(projectId);
    }

    public IReadOnlyList<FieldOption> ThemeOptions(string projectId)
    {
        return _database.GetThemeOptions(projectId);
    }

    public string ProjectMediaRoot(string projectId)
    {
        return _database.GetProjectSettings(projectId).MediaRoot;
    }

    public DevicePreviewMetrics DeviceMetrics(string deviceId)
    {
        return _database.GetDevicePreviewMetrics(deviceId);
    }
}
