using Mockups.DesktopEditorShell.EditorShell;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Data;

public enum ExternalMediaAuthoringSurface
{
    Editor,
    PreviewAuthoring,
}

public sealed record ExternalMediaUsageDetail(
    string ProjectId,
    string SourceNodeId,
    ProjectTreeNodeKind SourceKind,
    string SourceRecordClassId,
    string SourceTypeLabel,
    string SourceName,
    ReferenceUsageScope Scope,
    ExternalMediaAuthoringSurface AuthoringSurface,
    IReadOnlyList<string> SlotFieldIds,
    string FieldId,
    string FieldLabel,
    string ItemId,
    string AuthoredPath,
    string AbsoluteTargetPath,
    string AbsoluteDirectoryPath,
    string FileName,
    bool IsDirectory,
    bool Exists)
{
    public string SystemItem =>
        $"{SourceTypeLabel} · {SourceName} › {FieldLabel}";

    public bool IsProduction => Scope == ReferenceUsageScope.Production;
}
