using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public PaletteColorSettings GetPaletteColorSettings(string colorId)
    {
        return _paletteRepository.GetSettings(colorId);
    }

    public void UpdatePaletteColorField(string colorId, string fieldId, string value)
    {
        _paletteRepository.UpdateField(colorId, fieldId, value);
    }

    public IReadOnlyList<FieldOption> GetPaletteColorOptions(string projectId)
    {
        return _paletteRepository.GetOptions(projectId)
            .Select((option) => new FieldOption(option.Token, option.Label, option.ColorHex, option.IsNeutral))
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

    private IReadOnlyList<PaletteColorRecord> QueryPaletteColorRows(SqliteConnection connection)
    {
        return _paletteRepository.QueryAll(connection);
    }
}
