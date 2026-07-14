using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorSessionUiState
{
    private readonly Dictionary<string, string> _selections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _expansions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pendingReveals = new(StringComparer.Ordinal);

    public string? Selection(string key) => _selections.GetValueOrDefault(key);

    public void Select(string key, string value) => _selections[key] = value;

    public bool IsExpanded(string key) => _expansions.GetValueOrDefault(key);

    public void SetExpanded(string key, bool value) => _expansions[key] = value;

    public void SetOnlyExpanded(IEnumerable<string> keys, string activeKey)
    {
        foreach (var key in keys.Distinct(StringComparer.Ordinal))
        {
            _expansions[key] = key.Equals(activeKey, StringComparison.Ordinal);
        }
        _expansions[activeKey] = true;
    }

    public void RequestReveal(string key) => _pendingReveals.Add(key);

    public bool ConsumeReveal(string key) => _pendingReveals.Remove(key);
}
