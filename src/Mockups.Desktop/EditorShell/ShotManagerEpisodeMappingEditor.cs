using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Integrations.ShotManager;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ShotManagerEpisodeMappingEditor
{
    private const string CreateNewValue = "\u001ecreate-new";
    private readonly IReadOnlyList<ShotManagerEpisode> _remoteEpisodes;
    private readonly IReadOnlyList<ShotManagerLocalEpisodeRecord> _localEpisodes;
    private readonly Dictionary<string, EditorInstantComboBox> _selectors =
        new(StringComparer.Ordinal);

    public ShotManagerEpisodeMappingEditor(
        IShotManagerProjectStore database,
        string projectId,
        ShotManagerProductionSnapshot snapshot,
        string seasonId)
    {
        var existingExternalIds = database
            .LoadShotManagerLocalEpisodes(projectId)
            .Where((local) => local.Binding is not null)
            .Select((local) => local.Binding!.ExternalEpisodeId)
            .ToHashSet(StringComparer.Ordinal);
        _remoteEpisodes = snapshot.Episodes
            .Where((episode) =>
                episode.SeasonId.Equals(seasonId, StringComparison.Ordinal)
                && !existingExternalIds.Contains(episode.Id))
            .OrderBy((episode) => episode.Number)
            .ThenBy((episode) => episode.Id, StringComparer.Ordinal)
            .ToList();
        _localEpisodes = database.LoadShotManagerLocalEpisodes(projectId)
            .Where((local) => local.Binding is null)
            .OrderBy((local) => local.Episode.SortOrder)
            .ThenBy((local) => local.Episode.Name, StringComparer.Ordinal)
            .ThenBy((local) => local.Episode.Id, StringComparer.Ordinal)
            .ToList();
        Content = BuildContent();
    }

    public event EventHandler? StateChanged;

    public Control Content { get; }

    public bool IsComplete => _selectors.Values.All((selector) =>
        selector.SelectedItem is not null);

    public IReadOnlyList<ShotManagerEpisodeAssociationChoice> Choices()
    {
        if (!IsComplete)
        {
            throw new InvalidOperationException(
                "Every Shot Manager Episode requires an explicit association choice.");
        }

        return _remoteEpisodes.Select((remote) =>
        {
            var selected = _selectors[remote.Id].SelectedItem!;
            return new ShotManagerEpisodeAssociationChoice(
                remote.Id,
                selected.Value.Equals(
                    CreateNewValue,
                    StringComparison.Ordinal)
                    ? null
                    : selected.Value);
        }).ToList();
    }

    private Control BuildContent()
    {
        var root = new StackPanel
        {
            Spacing = 8,
        };
        if (_remoteEpisodes.Count == 0)
        {
            root.Children.Add(new TextBlock
            {
                Text = "Every Shot Manager Episode is already associated.",
                Opacity = 0.72,
                TextWrapping = TextWrapping.Wrap,
            });
            return root;
        }

        root.Children.Add(new TextBlock
        {
            Text = "Episode associations",
            FontWeight = FontWeight.SemiBold,
        });
        foreach (var remote in _remoteEpisodes)
        {
            var options = new List<FieldOption>
            {
                new(CreateNewValue, "Create new Episode"),
            };
            options.AddRange(_localEpisodes.Select((local) =>
                new FieldOption(
                    local.Episode.Id,
                    LocalEpisodeLabel(local))));
            var selector = new EditorInstantComboBox
            {
                ItemsSource = options,
                SelectedItem = null,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 260,
            };
            _selectors[remote.Id] = selector;
            selector.SelectionChanged += (_, _) =>
            {
                RefreshDisabledOptions();
                StateChanged?.Invoke(this, EventArgs.Empty);
            };

            var remoteIdentity = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"{remote.Code} · Episode {remote.Number}",
                        FontWeight = FontWeight.SemiBold,
                    },
                    new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(remote.Title)
                            ? "No Shot Manager title"
                            : remote.Title.Trim(),
                        Opacity = 0.72,
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            };
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("1.05*,1.4*"),
                ColumnSpacing = 14,
                Children = { remoteIdentity, selector },
            };
            Grid.SetColumn(selector, 1);
            root.Children.Add(new Border
            {
                Padding = new Thickness(10, 9),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.Parse("#40515A68")),
                Child = row,
            });
        }
        return root;
    }

    private void RefreshDisabledOptions()
    {
        var selectedLocalIds = _selectors.Values
            .Select((selector) => selector.SelectedItem?.Value)
            .Where((value) =>
                value is not null
                && !value.Equals(CreateNewValue, StringComparison.Ordinal))
            .Select((value) => value!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var selector in _selectors.Values)
        {
            var current = selector.SelectedItem?.Value;
            selector.DisabledValues = selectedLocalIds.Where((localId) =>
                !localId.Equals(current, StringComparison.Ordinal));
        }
    }

    private static string LocalEpisodeLabel(
        ShotManagerLocalEpisodeRecord local)
    {
        var slug = string.IsNullOrWhiteSpace(local.Episode.Slug)
            ? ""
            : $" · {local.Episode.Slug}";
        var shots = local.HasShots ? " · contains Shots" : "";
        return $"{local.Episode.Name}{slug}{shots}";
    }
}
