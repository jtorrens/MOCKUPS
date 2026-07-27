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
    private readonly IReferenceUsageQuery _database;
    private readonly EditorOperationCoordinator _operations;
    private readonly bool _isDark;
    private readonly Func<ReferenceUsageDetail, Task> _navigateToUsage;

    public ReferenceUsageCollectionEditor(
        IReferenceUsageQuery database,
        EditorOperationCoordinator operations,
        bool isDark,
        Func<ReferenceUsageDetail, Task> navigateToUsage)
    {
        _database = database;
        _operations = operations;
        _isDark = isDark;
        _navigateToUsage = navigateToUsage;
    }

    public InstantEditorCard Create(ProjectTreeNode node)
    {
        return DeferredEditorCard.Create(
            "Usage",
            "Load on expand",
            () => EditorIcons.CreateSemantic(
                    "Usage",
                    EditorIcons.Structure,
                    18),
            "collection:usage",
            (cancellationToken) => _operations.ExecuteAsync(
                () => _database.GetReferenceUsageDetails(node),
                cancellationToken),
            Present);
    }

    private DeferredEditorCardContent Present(
        IReadOnlyList<ReferenceUsageDetail> usages)
    {
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

        return new DeferredEditorCardContent(
            usages.Count == 1
                ? "1 reference"
                : $"{usages.Count} references",
            new Border
            {
                Padding = EditorUiDensity.CardThickness(10),
                Child = content,
            });
    }

    private void AddGroup(Panel host, string label, IEnumerable<ReferenceUsageDetail> usages)
    {
        var items = usages.ToList();
        if (items.Count == 0) return;

        host.Children.Add(CollapsibleTree.Branch(
            label,
            EditorIcons.Create(EditorIcons.Folder, 16),
            items.Select((usage, index) => CreateUsageLeaf(usage, index == items.Count - 1))));
    }

    private Control CreateUsageLeaf(ReferenceUsageDetail usage, bool isLast)
    {
        return CollapsibleTree.Leaf(
            EditorIcons.Create(EditorIcons.ForTreeNode(usage.SourceKind), 15),
            EditorReferenceUsageLink.Create(usage, _isDark, () => _navigateToUsage(usage)),
            new Border { Width = 0 },
            isLast);
    }
}
