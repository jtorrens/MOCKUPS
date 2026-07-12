using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorSessionHistoryState
{
    public EditorVariantHistoryStore? VariantHistory { get; set; }
    public List<EditorDesignPreviewHistoryEntryState> DesignPreviewHistory { get; set; } = [];
    public List<EditorDesignPreviewHistoryEntryState> ProductionPreviewHistory { get; set; } = [];
    public Dictionary<string, string> LastComponentVariantSelections { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class EditorVariantHistoryStore
{
    public int Sequence { get; set; }
    public Dictionary<string, List<EditorVariantHistorySnapshotState>> SnapshotsByVariant { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class EditorVariantHistorySnapshotState
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public EditorViewStateSnapshot? ViewState { get; set; }
}

internal sealed class EditorDesignPreviewHistoryEntryState
{
    public ProjectTreeNodeKind Kind { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public EditorViewStateSnapshot? ViewState { get; set; }
}

internal sealed class EditorViewStateSnapshot
{
    public bool[] ExpandedCards { get; set; } = [];
    public double ScrollX { get; set; }
    public double ScrollY { get; set; }

    public static EditorViewStateSnapshot From(EditorViewState state)
    {
        return new EditorViewStateSnapshot
        {
            ExpandedCards = state.ExpandedCards.ToArray(),
            ScrollX = state.ScrollOffset.X,
            ScrollY = state.ScrollOffset.Y,
        };
    }

    public EditorViewState ToViewState()
    {
        return new EditorViewState(
            ExpandedCards.ToArray(),
            new Avalonia.Vector(ScrollX, ScrollY));
    }
}
