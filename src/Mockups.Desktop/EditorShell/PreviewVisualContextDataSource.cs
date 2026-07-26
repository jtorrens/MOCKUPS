using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class PreviewVisualContextDataSource
{
    private readonly IPreviewInputRepository _database;
    private readonly IProjectSettingsQuery _projects;

    public PreviewVisualContextDataSource(
        IPreviewInputRepository database,
        IProjectSettingsQuery projects)
    {
        _database = database;
        _projects = projects;
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
        return _projects.GetProjectSettings(projectId).MediaRoot;
    }

    public DevicePreviewMetrics DeviceMetrics(string deviceId)
    {
        return _database.GetDevicePreviewMetrics(deviceId);
    }
}
