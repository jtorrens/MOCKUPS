using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record PreviewVisualContextSnapshot(
    string ProjectId,
    IReadOnlyList<FieldOption> DeviceOptions,
    IReadOnlyList<FieldOption> ThemeOptions,
    IReadOnlyDictionary<string, DevicePreviewMetrics> DeviceMetricsById,
    string MediaRoot)
{
    public DevicePreviewMetrics DeviceMetrics(string deviceId)
    {
        return DeviceMetricsById.TryGetValue(
            deviceId,
            out var metrics)
                ? metrics
                : throw new InvalidOperationException(
                    $"Preview device '{deviceId}' is not part of prepared Project '{ProjectId}'.");
    }
}

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

    public PreviewVisualContextSnapshot LoadSnapshot(
        string projectId)
    {
        var deviceOptions =
            _database.GetDeviceOptions(projectId).ToArray();
        var themeOptions =
            _database.GetThemeOptions(projectId).ToArray();
        return new PreviewVisualContextSnapshot(
            projectId,
            deviceOptions,
            themeOptions,
            deviceOptions.ToDictionary(
                (option) => option.Value,
                (option) =>
                    _database.GetDevicePreviewMetrics(
                        option.Value),
                StringComparer.Ordinal),
            _projects.GetProjectSettings(projectId).MediaRoot);
    }
}
