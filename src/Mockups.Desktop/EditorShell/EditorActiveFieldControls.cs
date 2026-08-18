using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorActiveFieldControls
{
    private readonly Dictionary<string, DictionaryFieldControl> _controlsByFieldId = [];

    public IReadOnlyDictionary<string, DictionaryFieldControl> ControlsByFieldId => _controlsByFieldId;

    public void Clear()
    {
        _controlsByFieldId.Clear();
    }

    public void Register(DictionaryFieldControl control)
    {
        _controlsByFieldId[control.FieldId] = control;
    }

    public string ValueOrStored(string fieldId, Func<string, string> storedValue)
    {
        return _controlsByFieldId.TryGetValue(fieldId, out var control)
            ? control.Value
            : storedValue(fieldId);
    }

    public void RefreshPreviews()
    {
        foreach (var control in _controlsByFieldId.Values)
        {
            control.RefreshPreview();
        }
    }

    public void RefreshReadOnlyValues(Func<string, FieldValue> read)
    {
        foreach (var control in _controlsByFieldId.Values)
        {
            if (control.IsEditable) continue;
            var current = read(control.FieldId);
            control.SetValue(
                current.IsInherited
                    ? current.Definition.InheritedValue
                    : current.Value);
            control.MarkCurrentValueCommitted();
        }
    }
}
