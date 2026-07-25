using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Integrations.ShotManager;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ShotManagerShotStructureCollectionEditor
{
    private readonly SpikeDatabase _database;

    public ShotManagerShotStructureCollectionEditor(
        SpikeDatabase database)
    {
        _database = database;
    }

    public InstantEditorCard? Create(ProjectTreeNode shot)
    {
        var record = _database.GetShotManagerShotStructure(shot.Id);
        if (record is null) return null;
        var structure = ShotManagerPortableStructure.Parse(
            record.StructureJson,
            $"Shot Manager Shot '{shot.Id}' structure");
        var body = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = record.FullName,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                },
                new TextBlock
                {
                    Text = $"{record.ShotCode} · {structure.OutputContracts.Count} render destinations",
                    Opacity = 0.72,
                },
                new TextBlock
                {
                    Text = "This is the last route contract received from Shot Manager. Missing destination folders are created when a render starts.",
                    Opacity = 0.72,
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

        return new InstantEditorCard(
            EditorCardHeader.Create(
                "Shot Manager render routes",
                "Last synchronized contract",
                EditorIcons.CreateSemantic(
                    "Shot Manager folders",
                    EditorIcons.Folder,
                    18)),
            new Border
            {
                Padding = EditorUiDensity.CardThickness(12),
                Child = body,
            },
            isExpanded: false)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SessionStateId = "integration:shot-manager-render-routes",
        };
    }
}
