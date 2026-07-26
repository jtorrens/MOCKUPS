using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteEditorLayoutStore : IEditorLayoutStore
{
    private readonly SqliteProjectContext _context;

    public SqliteEditorLayoutStore(SqliteProjectContext context)
    {
        _context = context;
    }

    public EditorLayout LoadEditorLayout(string recordClassId)
    {
        using var connection = _context.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT layout_json FROM editor_layouts WHERE record_class_id = $recordClassId";
        command.Parameters.AddWithValue("$recordClassId", recordClassId);
        var json = command.ExecuteScalar() as string
            ?? throw new InvalidOperationException($"Missing editor layout for record class '{recordClassId}'.");
        var document = JsonPath.ParseRequiredObject(json, $"Editor layout '{recordClassId}' layout_json");
        if (document.Count != 1 || document["cards"] is not JsonArray)
        {
            throw new InvalidOperationException(
                $"Editor layout '{recordClassId}' must contain exactly one top-level cards array.");
        }

        return JsonSerializer.Deserialize<EditorLayout>(json)
            ?? throw new InvalidOperationException($"Invalid editor layout JSON for record class '{recordClassId}'.");
    }

    public void SaveEditorLayout(string recordClassId, EditorLayout layout)
    {
        using var connection = _context.OpenConnection();
        _context.Execute(
            connection,
            "UPDATE editor_layouts SET layout_json = $layoutJson WHERE record_class_id = $recordClassId",
            ("$recordClassId", recordClassId),
            ("$layoutJson", JsonSerializer.Serialize(layout, new JsonSerializerOptions { WriteIndented = true })));
    }
}
