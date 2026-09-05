using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ShotDuplicationDialog
{
    private readonly Window _owner;
    private readonly IEditorChildStore _database;
    private readonly EditorOperationCoordinator _operations;

    public ShotDuplicationDialog(
        Window owner,
        IEditorChildStore database,
        EditorOperationCoordinator operations)
    {
        _owner = owner;
        _database = database;
        _operations = operations;
    }

    public async Task<int?> Show(ProjectTreeNode episode)
    {
        if (episode.Kind != ProjectTreeNodeKind.Episode)
        {
            throw new InvalidOperationException("Shot duplication requires an Episode.");
        }
        var suggested = await _operations.ExecuteAsync(
            () => _database.SuggestShotNumber(episode.Id));
        var definition = new RecordCreationDefinition(
            "shot.duplicate",
            "shot",
            "Duplicate Shot",
            "The duplicate keeps its original Actor. Choose its new Shot number.",
            "Duplicate",
            [
                new FieldValue(
                    new FieldDefinition(
                        "shot.creation.shotNumber",
                        "Shot number",
                        ValueKind.Integer,
                        DefaultValue: suggested.ToString(),
                        Number: new NumberDefinition(1, 99_999_999, 1, 0)),
                    suggested.ToString()),
            ]);
        var draft = await new RecordCreationDialog(_owner).Show(definition);
        return draft is null
            ? null
            : int.Parse(draft.Values["shot.creation.shotNumber"]);
    }
}
