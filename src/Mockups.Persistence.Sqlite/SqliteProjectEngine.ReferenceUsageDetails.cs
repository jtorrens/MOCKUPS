using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    public IReadOnlyList<ReferenceUsageDetail> GetReferenceUsageDetails(ProjectTreeNode node)
    {
        return _referenceUsageService.GetUsages(node.Kind, node.Id)
            .Select((usage) => new ReferenceUsageDetail(
                usage.SourceNodeId,
                usage.SourceKind,
                usage.SourceTypeLabel,
                usage.SourceName,
                usage.FieldLabel,
                usage.Scope,
                usage.EmbeddedContext is null ? null : ToEmbeddedComponentUsage(usage.EmbeddedContext)))
            .OrderBy((usage) => usage.IsProduction)
            .ThenBy((usage) => usage.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy((usage) => usage.Field, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
