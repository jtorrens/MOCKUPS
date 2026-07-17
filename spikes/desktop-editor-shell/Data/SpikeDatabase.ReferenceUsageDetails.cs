using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public sealed record ReferenceUsageDetail(
        string TargetNodeId,
        ProjectTreeNodeKind TargetKind,
        string Label,
        string Field,
        bool IsProduction);

    public IReadOnlyList<ReferenceUsageDetail> GetReferenceUsageDetails(ProjectTreeNode node)
    {
        return _referenceUsageService.GetUsages(node.Kind, node.Id)
            .Select((usage) => new ReferenceUsageDetail(
                usage.SourceNodeId,
                usage.SourceKind,
                $"{usage.SourceTypeLabel}: {usage.SourceName}",
                usage.FieldLabel,
                usage.Scope == ReferenceUsageScope.Production))
            .OrderBy((usage) => usage.IsProduction)
            .ThenBy((usage) => usage.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy((usage) => usage.Field, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
