using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteResourceOwner
{
    public PaletteColorSettings GetPaletteColorSettings(string colorId)
    {
        return _paletteRepository.GetSettings(colorId);
    }

    public void UpdatePaletteColorField(string colorId, string fieldId, string value)
    {
        if (fieldId == "palette.token")
        {
            RenamePaletteToken(colorId, value);
            return;
        }

        _paletteRepository.UpdateField(colorId, fieldId, value);
    }

    private void RenamePaletteToken(string colorId, string token)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var palette = _paletteRepository.RequireRecord(connection, colorId);
        if (palette.Token.Equals(token, System.StringComparison.Ordinal))
        {
            transaction.Commit();
            return;
        }

        _paletteRepository.RenameToken(
            connection,
            transaction,
            colorId,
            token);
        transaction.Commit();
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

    internal IReadOnlyList<PaletteColorRecord> QueryPaletteColorRows(SqliteConnection connection)
    {
        return _paletteRepository.QueryAll(connection);
    }
}
