using Avalonia;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorViewState(bool[] ExpandedCards, Vector ScrollOffset);
