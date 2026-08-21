using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class DeviceRepository : IDeviceRepository
{
    private readonly SqliteProjectContext _context;

    public DeviceRepository(SqliteProjectContext context)
    {
        _context = context;
    }

    public DeviceSettings GetSettings(string deviceId)
    {
        using var connection = _context.OpenConnection();
        return GetSettings(connection, deviceId);
    }

    public void UpdateField(string deviceId, string fieldId, string value)
    {
        using var connection = _context.OpenConnection();
        var next = DeviceSettingsFieldContract.ApplyField(
            GetSettings(connection, deviceId),
            fieldId,
            value);
        _context.Execute(
            connection,
            """
            UPDATE devices
            SET manufacturer = $manufacturer,
                model = $model,
                os_family = $osFamily,
                metrics_json = $metricsJson
            WHERE id = $id
            """,
            ("$id", deviceId),
            ("$manufacturer", next.Manufacturer),
            ("$model", next.Model),
            ("$osFamily", next.OsFamily),
            ("$metricsJson", next.MetricsJson));
    }

    public IReadOnlyList<ResourceOption> GetOptions(string projectId)
    {
        using var connection = _context.OpenConnection();
        return QueryAll(connection)
            .Where((device) => device.ProjectId == projectId)
            .OrderBy((device) => device.Name)
            .Select((device) => new ResourceOption(device.Id, device.Name))
            .ToList();
    }

    public IReadOnlyList<DeviceRecord> QueryAll(SqliteConnection connection)
    {
        var rows = new List<DeviceRecord>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, manufacturer, model, os_family, metrics_json FROM devices ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new DeviceRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                SqliteCommandExecutor.ReadString(reader, 3),
                SqliteCommandExecutor.ReadString(reader, 4),
                SqliteCommandExecutor.ReadString(reader, 5),
                reader.GetString(6)));
        }

        return rows;
    }

    public DeviceRecord Create(SqliteConnection connection, string projectId)
    {
        var index = SqliteCommandExecutor.ScalarLong(
            connection,
            "SELECT COUNT(*) FROM devices WHERE project_id = $projectId",
            ("$projectId", projectId)) + 1;
        var id = $"device_{Guid.NewGuid():N}";
        var name = $"Device {index}";
        var metricsJson = DeviceMetricRules.CreateMetricsJson(1170, 2532);
        _context.Execute(
            connection,
            """
            INSERT INTO devices (id, project_id, name, manufacturer, model, os_family, metrics_json)
            VALUES ($id, $projectId, $name, '', '', 'ios', $metricsJson)
            """,
            ("$id", id),
            ("$projectId", projectId),
            ("$name", name),
            ("$metricsJson", metricsJson));
        return new DeviceRecord(id, projectId, name, "", "", "ios", metricsJson);
    }

    public DeviceRecord CreateImported(
        SqliteConnection connection,
        string projectId,
        string name,
        string manufacturer,
        string model,
        string osFamily,
        string metricsJson)
    {
        _ = DeviceMetricRules.PreviewValues(
            JsonPath.ParseRequiredObject(metricsJson, "Imported Device metrics_json"));
        var id = $"device_{Guid.NewGuid():N}";
        _context.Execute(
            connection,
            """
            INSERT INTO devices (id, project_id, name, manufacturer, model, os_family, metrics_json)
            VALUES ($id, $projectId, $name, $manufacturer, $model, $osFamily, $metricsJson)
            """,
            ("$id", id),
            ("$projectId", projectId),
            ("$name", name),
            ("$manufacturer", manufacturer),
            ("$model", model),
            ("$osFamily", osFamily),
            ("$metricsJson", metricsJson));
        return new DeviceRecord(id, projectId, name, manufacturer, model, osFamily, metricsJson);
    }

    public DeviceRecord Duplicate(SqliteConnection connection, string sourceId, string copyName)
    {
        var source = QueryAll(connection).SingleOrDefault((device) => device.Id == sourceId)
            ?? throw new InvalidOperationException($"Missing device '{sourceId}'.");
        var copy = source with { Id = $"device_{Guid.NewGuid():N}", Name = copyName };
        _context.Execute(
            connection,
            """
            INSERT INTO devices (id, project_id, name, manufacturer, model, os_family, metrics_json)
            VALUES ($id, $projectId, $name, $manufacturer, $model, $osFamily, $metricsJson)
            """,
            ("$id", copy.Id),
            ("$projectId", copy.ProjectId),
            ("$name", copy.Name),
            ("$manufacturer", copy.Manufacturer),
            ("$model", copy.Model),
            ("$osFamily", copy.OsFamily),
            ("$metricsJson", copy.MetricsJson));
        return copy;
    }

    public void Delete(SqliteConnection connection, string deviceId)
    {
        _context.Execute(connection, "DELETE FROM devices WHERE id = $id", ("$id", deviceId));
    }

    public void Rename(SqliteConnection connection, string deviceId, string name)
    {
        _context.Execute(
            connection,
            "UPDATE devices SET name = $name WHERE id = $id",
            ("$id", deviceId),
            ("$name", name));
    }

    private static DeviceSettings GetSettings(SqliteConnection connection, string deviceId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT project_id, name, manufacturer, model, os_family, metrics_json FROM devices WHERE id = $id";
        command.Parameters.AddWithValue("$id", deviceId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing device '{deviceId}'.");
        }

        var settings = new DeviceSettings(
            reader.GetString(0),
            reader.GetString(1),
            SqliteCommandExecutor.ReadString(reader, 2),
            SqliteCommandExecutor.ReadString(reader, 3),
            SqliteCommandExecutor.ReadString(reader, 4),
            reader.GetString(5));
        _ = DeviceMetricRules.PreviewValues(
            JsonPath.ParseRequiredObject(settings.MetricsJson, $"Device '{deviceId}' metrics_json"));
        return settings;
    }
}
