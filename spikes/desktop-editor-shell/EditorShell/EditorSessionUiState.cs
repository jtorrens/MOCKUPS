using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorSessionUiState
{
    private readonly Dictionary<string, string> _selections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _expansions = new(StringComparer.Ordinal);

    public string? Selection(string key) => _selections.GetValueOrDefault(key);

    public void Select(string key, string value) => _selections[key] = value;

    public bool IsExpanded(string key) => _expansions.GetValueOrDefault(key);

    public void SetExpanded(string key, bool value) => _expansions[key] = value;
}
