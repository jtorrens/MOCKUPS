using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ReferenceUsageCollectionEditor
{
    private readonly SpikeDatabase _database;
    private readonly bool _isDark;
    private readonly Func<SpikeDatabase.ReferenceUsageDetail, Task> _navigateToUsage;

    public ReferenceUsageCollectionEditor(
        SpikeDatabase database,
        bool isDark,
        Func<SpikeDatabase.ReferenceUsageDetail, Task> navigateToUsage)
    {
        _database = database;
        _isDark = isDark;
        _navigateToUsage = navigateToUsage;
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
        return CollapsibleTree.Leaf(
            EditorIcons.Create(EditorIcons.ForTreeNode(usage.SourceKind), 15),
            EditorReferenceUsageLink.Create(usage, _isDark, () => _navigateToUsage(usage)),
            new Border { Width = 0 },
            isLast);
    }
}
