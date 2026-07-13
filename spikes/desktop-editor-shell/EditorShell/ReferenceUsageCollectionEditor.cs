using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ReferenceUsageCollectionEditor
{
    private readonly SpikeDatabase _database;
    private readonly Func<string, bool> _navigateToNode;

    public ReferenceUsageCollectionEditor(SpikeDatabase database, Func<string, bool> navigateToNode)
    {
        _database = database;
        _navigateToNode = navigateToNode;
    }

    public InstantEditorCard Create(ProjectTreeNode node)
    {
        var usages = _database.GetReferenceUsageDetails(node);
        var content = new StackPanel { Spacing = 10 };
        if (usages.Count == 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = "No design or production references were found.",
                Opacity = 0.68,
            });
        }
        else
        {
            AddGroup(content, "Design usage", usages.Where((usage) => !usage.IsProduction));
            AddGroup(content, "Production usage", usages.Where((usage) => usage.IsProduction));
        }

        return new InstantEditorCard(
            EditorCardHeader.Create("Usage", usages.Count == 1 ? "1 reference" : $"{usages.Count} references", EditorIcons.CreateSemantic("Usage", EditorIcons.Structure, 18)),
            new Border { Padding = EditorUiDensity.CardThickness(10), Child = content },
            isExpanded: false)
        { HorizontalAlignment = HorizontalAlignment.Stretch };
    }

    private void AddGroup(Panel host, string label, IEnumerable<SpikeDatabase.ReferenceUsageDetail> usages)
    {
        var items = usages.ToList();
        if (items.Count == 0) return;

        host.Children.Add(CollapsibleTree.Branch(
            label,
            EditorIcons.Create(EditorIcons.Folder, 16),
            items.Select((usage, index) => CreateUsageLeaf(usage, index == items.Count - 1))));
    }

    private Control CreateUsageLeaf(SpikeDatabase.ReferenceUsageDetail usage, bool isLast)
    {
        var text = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = usage.Label, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = usage.Field, FontSize = 11, Opacity = 0.68 },
            },
        };

        var open = new Button
        {
            Width = 28,
            Height = 26,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = EditorIcons.CreateSemantic("Open referenced record", EditorIcons.Edit, 14),
        };
        ToolTip.SetTip(open, "Open referenced record");
        open.Click += (_, args) =>
        {
            args.Handled = true;
            _navigateToNode(usage.TargetNodeId);
        };

        return CollapsibleTree.Leaf(
            EditorIcons.Create(EditorIcons.ForTreeNode(usage.TargetKind), 15),
            text,
            open,
            isLast);
    }
}
