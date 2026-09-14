using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteResourceOwner
{
    public PaletteColorSettings GetPaletteColorSettings(
        string projectId,
        string colorId)
    {
        return _paletteRepository.GetSettings(projectId, colorId);
    }

    public void UpdatePaletteColorField(
        string projectId,
        string colorId,
        string fieldId,
        string value)
    {
        if (fieldId != "palette.valueHex")
        {
            throw new System.InvalidOperationException(
                $"Production Palette exposes only its RGB value; field '{fieldId}' belongs to the System catalog.");
        }
        _paletteRepository.UpdateProductionValue(projectId, colorId, value);
    }

    public IReadOnlyList<FieldOption> GetPaletteColorOptions(string projectId)
    {
        return _paletteRepository.GetOptions(projectId)
            .Select((option) => new FieldOption(option.Id, option.Label, option.ColorHex, option.IsNeutral))
            .ToList();
    }

    public IReadOnlyDictionary<string, string> GetPaletteColorMap(string projectId)
    {
        return _paletteRepository.GetColorMap(projectId);
    }

    public IReadOnlyDictionary<string, bool> GetPaletteNeutralMap(string projectId)
    {
        return _paletteRepository.GetNeutralMap(projectId);
    }

    internal IReadOnlyList<ProductionPaletteColorRecord> QueryPaletteColorRows(SqliteConnection connection)
    {
        return _paletteRepository.QueryAllProductionValues(connection);
    }
}
