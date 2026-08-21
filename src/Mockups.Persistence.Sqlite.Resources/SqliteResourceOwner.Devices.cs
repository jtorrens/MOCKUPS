using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteResourceOwner
{
    public DevicePreviewMetrics GetDevicePreviewMetrics(string deviceId)
    {
        return DeviceSettingsFieldContract.PreviewMetrics(
            GetDeviceSettings(deviceId));
    }

    public DeviceSettings GetDeviceSettings(string deviceId)
    {
        return _deviceRepository.GetSettings(deviceId);
    }

    public void UpdateDeviceField(string deviceId, string fieldId, string value)
    {
        _deviceRepository.UpdateField(deviceId, fieldId, value);
    }

    public string GetDeviceMetricFieldValue(string deviceId, string fieldId)
    {
        var settings = GetDeviceSettings(deviceId);
        return DeviceSettingsFieldContract.FieldValue(
            settings,
            fieldId);
    }

    public IReadOnlyList<FieldOption> GetDeviceOptions(string projectId)
    {
        return _deviceRepository.GetOptions(projectId)
            .Select((option) => new FieldOption(option.Value, option.Label))
            .ToList();
    }

    internal IReadOnlyList<DeviceRecord> QueryDeviceRows(SqliteConnection connection)
    {
        return _deviceRepository.QueryAll(connection);
    }
}
