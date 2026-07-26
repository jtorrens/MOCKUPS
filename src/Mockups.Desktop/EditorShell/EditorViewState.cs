using Avalonia;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorViewState(
    IReadOnlyList<string> ExpandedCardIds,
    Vector ScrollOffset);
