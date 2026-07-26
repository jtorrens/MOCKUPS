using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Integrations.ShotManager;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ShotManagerAssociationSelection(
    ShotManagerProductionSnapshot Snapshot,
    string SeasonId,
    IReadOnlyList<ShotManagerEpisodeAssociationChoice> EpisodeChoices);

internal sealed class ShotManagerAssociationDialog
{
    private readonly Window _owner;
    private readonly SpikeDatabase _database;
    private readonly string _projectId;
    private readonly IShotManagerIntegrationClient _client;

    public ShotManagerAssociationDialog(
        Window owner,
        SpikeDatabase database,
        string projectId,
        IShotManagerIntegrationClient? client = null)
    {
        _owner = owner;
        _database = database;
        _projectId = projectId;
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
            Width = 760,
            Height = 680,
            MinWidth = 680,
            MinHeight = 520,
            CanResize = true,
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
            Content = "Connect",
            MinWidth = 170,
            IsEnabled = false,
        };
        var mappingHost = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
        };
        ShotManagerEpisodeMappingEditor? mappingEditor = null;
        Candidate? selectedCandidate = null;
        void RefreshConnect()
        {
            connect.IsEnabled = selectedCandidate is not null
                && mappingEditor?.IsComplete == true;
        }
        void RefreshMappings()
        {
            selectedCandidate = selector.SelectedItem is null
                ? null
                : candidates.Single((value) =>
                    value.Key.Equals(
                        selector.SelectedItem.Value,
                        StringComparison.Ordinal));
            mappingEditor = selectedCandidate is null
                ? null
                : new ShotManagerEpisodeMappingEditor(
                    _database,
                    _projectId,
                    selectedCandidate.Snapshot,
                    selectedCandidate.Season.Id);
            if (mappingEditor is not null)
            {
                mappingEditor.StateChanged += (_, _) => RefreshConnect();
            }
            mappingHost.Content = mappingEditor?.Content;
            RefreshConnect();
        }
        selector.SelectionChanged += (_, _) => RefreshMappings();
        var cancel = new Button { Content = "Cancel", MinWidth = 92 };
        cancel.Click += (_, _) => dialog.Close(null);
        connect.Click += (_, _) =>
        {
            if (selectedCandidate is null
                || mappingEditor?.IsComplete != true)
            {
                return;
            }
            dialog.Close(new ShotManagerAssociationSelection(
                selectedCandidate.Snapshot,
                selectedCandidate.Season.Id,
                mappingEditor.Choices()));
        };

        var intro = new StackPanel
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
                    Text = "Then associate every Shot Manager Episode explicitly with an existing local Episode or choose Create new. No Episode is created before confirmation.",
                    Opacity = 0.72,
                    TextWrapping = TextWrapping.Wrap,
                },
                selector,
            },
        };
        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 14,
            Children =
            {
                intro,
                new ScrollViewer
                {
                    HorizontalScrollBarVisibility =
                        Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility =
                        Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    Content = mappingHost,
                },
            },
        };
        Grid.SetRow(content.Children[1], 1);
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
        dialog.Opened += (_, _) => RefreshMappings();
        return await dialog.ShowDialog<ShotManagerAssociationSelection?>(_owner);
    }

    private sealed record Candidate(
        ShotManagerProductionSnapshot Snapshot,
        ShotManagerSeason Season)
    {
        public string Key => $"{Snapshot.Production.Id}\u001f{Season.Id}";
    }
}
