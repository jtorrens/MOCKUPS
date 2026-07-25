using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Integrations.ShotManager;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ShotManagerAssociationSelection(
    ShotManagerProductionSnapshot Snapshot,
    string SeasonId);

internal sealed class ShotManagerAssociationDialog
{
    private readonly Window _owner;
    private readonly IShotManagerIntegrationClient _client;

    public ShotManagerAssociationDialog(
        Window owner,
        IShotManagerIntegrationClient? client = null)
    {
        _owner = owner;
        _client = client ?? new ShotManagerIntegrationClient();
    }

    public async Task<ShotManagerAssociationSelection?> Show()
    {
        var catalog = await _client.GetCatalogAsync();
        var snapshots = new List<ShotManagerProductionSnapshot>();
        foreach (var production in catalog.Where((candidate) =>
            candidate.ProductionType == "SERIES"
            && candidate.SeriesShotStructure == "EPISODE_SHOT"))
        {
            snapshots.Add(await _client.GetSnapshotAsync(production.Id));
        }
        var candidates = snapshots
            .SelectMany((snapshot) => snapshot.Seasons.Select((season) =>
                new Candidate(snapshot, season)))
            .OrderBy((candidate) => candidate.Snapshot.Production.Name)
            .ThenBy((candidate) => candidate.Season.Number)
            .ToList();
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                "Shot Manager has no available direct-Episode series Seasons.");
        }

        var dialog = new SukiWindow
        {
            Title = "Connect Shot Manager",
            Width = 520,
            Height = 290,
            MinWidth = 480,
            MinHeight = 270,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);
        var options = candidates.Select((candidate) => new FieldOption(
            candidate.Key,
            $"{candidate.Snapshot.Production.Name} · {candidate.Season.Code}"
            + (string.IsNullOrWhiteSpace(candidate.Season.Name)
                ? ""
                : $" · {candidate.Season.Name}"))).ToList();
        var selector = new EditorInstantComboBox
        {
            ItemsSource = options,
            SelectedItem = null,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var connect = new Button
        {
            Content = "Connect and synchronize",
            MinWidth = 170,
            IsEnabled = false,
        };
        selector.SelectionChanged += (_, _) =>
            connect.IsEnabled = selector.SelectedItem is not null;
        var cancel = new Button { Content = "Cancel", MinWidth = 92 };
        cancel.Click += (_, _) => dialog.Close(null);
        connect.Click += (_, _) =>
        {
            var selected = selector.SelectedItem;
            if (selected is null) return;
            var candidate = candidates.Single((value) =>
                value.Key.Equals(selected.Value, StringComparison.Ordinal));
            dialog.Close(new ShotManagerAssociationSelection(
                candidate.Snapshot,
                candidate.Season.Id));
        };

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = "Choose the exact Production Season that owns this MOCKUPS Project.",
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text = "Shot Manager will govern Episode identities, technical Shot names and folder layouts. Shots remain local to MOCKUPS.",
                    Opacity = 0.72,
                    TextWrapping = TextWrapping.Wrap,
                },
                selector,
            },
        };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancel, connect },
        };
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 18,
            Children = { content, actions },
        };
        Grid.SetRow(actions, 1);
        dialog.Content = new Border
        {
            Padding = EditorUiDensity.CardThickness(18),
            Child = root,
        };
        return await dialog.ShowDialog<ShotManagerAssociationSelection?>(_owner);
    }

    private sealed record Candidate(
        ShotManagerProductionSnapshot Snapshot,
        ShotManagerSeason Season)
    {
        public string Key => $"{Snapshot.Production.Id}\u001f{Season.Id}";
    }
}
