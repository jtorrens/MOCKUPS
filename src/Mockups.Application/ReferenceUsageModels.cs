using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

public sealed record ReferenceUsageDetail(
    string SourceNodeId,
    ProjectTreeNodeKind SourceKind,
    string SourceTypeLabel,
    string SourceName,
    string Field,
    ReferenceUsageScope Scope,
    EmbeddedComponentUsage? EmbeddedUsage)
{
    public string Label => $"{SourceTypeLabel}: {SourceName}";

    public bool IsProduction => Scope == ReferenceUsageScope.Production;
}
