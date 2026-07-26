using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record PreviewSessionState(
    EditorWorkspace Workspace,
    string? SelectedNodeId,
    long Revision);

public sealed record EditorSessionState
{
    public static EditorSessionState Empty { get; } = new(
        [],
        EditorWorkspace.Design,
        "",
        null,
        null,
        null,
        EditorVariantSelectionState.Empty,
        new PreviewSessionState(EditorWorkspace.Design, null, 0),
        0);

    public EditorSessionState(
        IReadOnlyList<ProjectTreeNode> treeRoots,
        EditorWorkspace workspace,
        string? productionId,
        ProjectTreeNode? selectedNode,
        EditorEmbeddedContext? embeddedEditor,
        IReadOnlyDictionary<EditorWorkspace, string>? workspaceSelections,
        EditorVariantSelectionState variantSelections,
        PreviewSessionState preview,
        long revision)
    {
        TreeRoots = Array.AsReadOnly(treeRoots.ToArray());
        Workspace = workspace;
        ProductionId = productionId ?? "";
        SelectedNode = selectedNode;
        EmbeddedEditor = embeddedEditor;
        WorkspaceSelections = new ReadOnlyDictionary<EditorWorkspace, string>(
            workspaceSelections is null
                ? new Dictionary<EditorWorkspace, string>()
                : new Dictionary<EditorWorkspace, string>(
                    workspaceSelections));
        VariantSelections = variantSelections;
        Preview = preview;
        Revision = revision;
    }

    public IReadOnlyList<ProjectTreeNode> TreeRoots { get; }
    public EditorWorkspace Workspace { get; }
    public string ProductionId { get; }
    public ProjectTreeNode? SelectedNode { get; }
    public EditorEmbeddedContext? EmbeddedEditor { get; }
    public IReadOnlyDictionary<EditorWorkspace, string> WorkspaceSelections { get; }
    public EditorVariantSelectionState VariantSelections { get; }
    public PreviewSessionState Preview { get; }
    public long Revision { get; }
}

public sealed record EditorSessionRestoreState(
    EditorWorkspace Workspace,
    string ProductionId,
    IReadOnlyDictionary<string, string>? ComponentVariantSelections = null);

[Flags]
public enum EditorSessionEffects
{
    None = 0,
    Workspace = 1 << 0,
    Production = 1 << 1,
    Navigation = 1 << 2,
    Editor = 1 << 3,
    PreviewSelection = 1 << 4,
    PreviewOptions = 1 << 5,
}

public sealed record EditorSessionTransition(
    string Source,
    EditorSessionState Previous,
    EditorSessionState Current,
    EditorSessionEffects Effects);
