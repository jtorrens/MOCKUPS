using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Integrations.ShotManager;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ShotManagerShotStructureCollectionEditor
{
    private readonly SpikeDatabase _database;
    private readonly Func<string, string, Task> _showInfo;

    public ShotManagerShotStructureCollectionEditor(
        SpikeDatabase database,
        Func<string, string, Task> showInfo)
    {
        _database = database;
        _showInfo = showInfo;
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
                    Text = $"{record.ShotCode} · {structure.Directories.Count} managed folders",
                    Opacity = 0.72,
                },
                new TextBlock
                {
                    Text = "The stored portable snapshot is not reinterpreted when the Production template changes.",
                    Opacity = 0.72,
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };
        var repair = new Button
        {
            Content = "Create missing folders",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        repair.Click += async (_, _) =>
        {
            repair.IsEnabled = false;
            try
            {
                var count = await new ShotManagerShotCreationService(_database)
                    .RepairAsync(shot.Id);
                await _showInfo(
                    "Shot folders checked",
                    count == 0
                        ? "All stored Shot folders already exist."
                        : $"Created {count} missing folder(s) from the stored snapshot.");
            }
            catch (Exception exception)
            {
                await _showInfo(
                    "Shot folder repair failed",
                    exception.Message);
            }
            finally
            {
                repair.IsEnabled = true;
            }
        };
        body.Children.Add(repair);

        return new InstantEditorCard(
            EditorCardHeader.Create(
                "Shot Manager folders",
                "Portable creation snapshot",
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
            SessionStateId = "integration:shot-manager-folders",
        };
    }
}
