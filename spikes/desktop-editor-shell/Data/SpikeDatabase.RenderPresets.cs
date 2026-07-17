using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public RenderPresetSettings GetRenderPresetSettings(string renderPresetId)
    {
        return _renderPresetRepository.GetSettings(renderPresetId);
    }

    public void UpdateRenderPresetField(string renderPresetId, string fieldId, string value)
    {
        _renderPresetRepository.UpdateField(renderPresetId, fieldId, value);
    }

    public IReadOnlyList<FieldOption> GetRenderPresetOptions(string projectId)
    {
        var options = _renderPresetRepository.GetOptions(projectId)
            .Select((option) => new FieldOption(option.Value, option.Label))
            .ToList();
        options.Insert(0, new FieldOption("", "None"));
        return options;
    }

    private IReadOnlyList<RenderPresetRecord> QueryRenderPresetRows(SqliteConnection connection)
    {
        return _renderPresetRepository.QueryAll(connection);
    }
}
