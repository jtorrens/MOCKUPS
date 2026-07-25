using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Integrations.ShotManager;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ShotManagerIntegrationCollectionEditor
{
    private readonly SpikeDatabase _database;
    private readonly EditorDomainDialogService _dialogs;
    private readonly Func<string, string, Task> _showInfo;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;
    private readonly IShotManagerIntegrationClient _client;

    public ShotManagerIntegrationCollectionEditor(
        SpikeDatabase database,
        EditorDomainDialogService dialogs,
        Func<string, string, Task> showInfo,
        Action<ProjectTreeNode> reloadAndSelect,
        IShotManagerIntegrationClient? client = null)
    {
        _database = database;
        _dialogs = dialogs;
        _showInfo = showInfo;
        _reloadAndSelect = reloadAndSelect;
        _client = client ?? new ShotManagerIntegrationClient();
    }

    public InstantEditorCard Create(
        ProjectTreeNode project,
        ProjectTreeNode selectionNode)
    {
        var association = _database.GetShotManagerAssociation(project.Id);
        var status = new TextBlock
        {
            Text = "Checking local Shot Manager connection…",
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
        };
        var body = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = association is null
                        ? "Independent Project"
                        : $"{association.ProductionName} · {association.SeasonCode}"
                            + (string.IsNullOrWhiteSpace(association.SeasonName)
                                ? ""
                                : $" · {association.SeasonName}"),
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text = association is null
                        ? "Connect optionally to synchronize Episodes and create official Shot Manager folder layouts."
                        : "Episodes use stable Shot Manager identities. Shots and their creative content remain owned by MOCKUPS.",
                    Opacity = 0.72,
                    TextWrapping = TextWrapping.Wrap,
                },
                status,
            },
        };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            Spacing = 8,
        };
        var primary = new Button
        {
            Content = association is null ? "Connect" : "Synchronize Episodes",
        };
        primary.Click += async (_, _) =>
        {
            primary.IsEnabled = false;
            try
            {
                var service = new ShotManagerAssociationService(_database);
                if (association is null)
                {
                    var selection =
                        await _dialogs.ShowShotManagerAssociation();
                    if (selection is null) return;
                    service.Synchronize(
                        project.Id,
                        selection.Snapshot,
                        selection.SeasonId);
                }
                else
                {
                    var snapshot = await _client.GetSnapshotAsync(
                        association.ProductionId);
                    service.Synchronize(
                        project.Id,
                        snapshot,
                        association.SeasonId);
                }
                _reloadAndSelect(selectionNode);
            }
            catch (Exception exception)
            {
                await _showInfo(
                    association is null
                        ? "Shot Manager connection failed"
                        : "Shot Manager synchronization failed",
                    exception.Message);
            }
            finally
            {
                primary.IsEnabled = true;
            }
        };
        actions.Children.Add(primary);
        if (association is not null)
        {
            var disconnect = new Button { Content = "Disconnect" };
            disconnect.Click += async (_, _) =>
            {
                if (!await _dialogs.ConfirmShotManagerDisconnect(
                    association.ProductionName,
                    association.SeasonCode))
                {
                    return;
                }
                new ShotManagerAssociationService(_database)
                    .Disconnect(project.Id);
                _reloadAndSelect(selectionNode);
            };
            actions.Children.Add(disconnect);
        }
        body.Children.Add(actions);
        _ = RefreshStatus(status);

        return new InstantEditorCard(
            EditorCardHeader.Create(
                "Shot Manager",
                association is null ? "Optional integration" : "Connected",
                EditorIcons.CreateSemantic(
                    "Shot Manager",
                    EditorIcons.Structure,
                    18)),
            new Border
            {
                Padding = EditorUiDensity.CardThickness(12),
                Child = body,
            },
            isExpanded: false)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SessionStateId = "integration:shot-manager",
        };
    }

    private async Task RefreshStatus(TextBlock status)
    {
        try
        {
            var connection = await _client.GetStatusAsync();
            status.Text = connection.Message;
            status.Opacity = connection.Connected ? 0.78 : 0.62;
        }
        catch (Exception exception)
        {
            status.Text = $"Connection status unavailable: {exception.Message}";
            status.Opacity = 0.62;
        }
    }
}
