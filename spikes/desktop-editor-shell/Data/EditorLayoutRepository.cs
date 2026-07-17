using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Text.Json;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class EditorLayoutRepository : IEditorLayoutRepository
{
    private readonly SqliteProjectContext _context;

    public EditorLayoutRepository(SqliteProjectContext context)
    {
        _context = context;
    }

    public EditorLayout Load(string recordClassId)
    {
        using var connection = _context.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT layout_json FROM editor_layouts WHERE record_class_id = $recordClassId";
        command.Parameters.AddWithValue("$recordClassId", recordClassId);
        var json = command.ExecuteScalar() as string
            ?? throw new InvalidOperationException($"Missing editor layout for record class '{recordClassId}'.");

        return JsonSerializer.Deserialize<EditorLayout>(json)
            ?? throw new InvalidOperationException($"Invalid editor layout JSON for record class '{recordClassId}'.");
    }

    public void Save(string recordClassId, EditorLayout layout)
    {
        using var connection = _context.OpenConnection();
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE editor_layouts SET layout_json = $layoutJson WHERE record_class_id = $recordClassId",
            ("$recordClassId", recordClassId),
            ("$layoutJson", JsonSerializer.Serialize(layout, new JsonSerializerOptions { WriteIndented = true })));
    }
}
