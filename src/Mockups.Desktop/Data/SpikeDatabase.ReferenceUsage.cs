using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static bool IsUsed(
        IReadOnlyDictionary<ReferenceTarget, IReadOnlyList<ReferenceUsageRecord>> index,
        ProjectTreeNodeKind kind,
        string id)
    {
        return index.ContainsKey(new ReferenceTarget(kind, id));
    }

    private IReadOnlyList<string> GetReferenceUsages(
        SqliteConnection connection,
        ProjectTreeNodeKind kind,
        string nodeId)
    {
        return _referenceUsageService.GetUsages(connection, kind, nodeId)
            .Select(UsageSummary)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy((usage) => usage, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string UsageSummary(ReferenceUsageRecord usage)
    {
        return $"{usage.SourceTypeLabel}: {usage.SourceName}{(string.IsNullOrWhiteSpace(usage.FieldLabel) ? "" : $" · {usage.FieldLabel}")}";
    }
}
