using Avalonia.Controls;
using Avalonia.Layout;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryPathBrowseButton : Button
{
    private readonly ValueKind _valueKind;
    private readonly Func<string, ValueKind, Task<string?>>? _browsePath;
    private string _value;

    public DictionaryPathBrowseButton(
        ValueKind valueKind,
        string value,
        bool isEditable,
        Func<string, ValueKind, Task<string?>>? browsePath)
    {
        _valueKind = valueKind;
        _value = value;
        _browsePath = browsePath;

        Content = "Browse";
        MinWidth = 86;
        MinHeight = 36;
        VerticalAlignment = VerticalAlignment.Center;
        IsEnabled = isEditable && browsePath is not null;
        Click += async (_, _) => await Browse();
    }

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        _value = value;
    }

    private async Task Browse()
    {
        if (_browsePath is null) return;

        var selectedPath = await _browsePath(_value, _valueKind);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            _value = selectedPath;
            ValueCommitted?.Invoke(this, _value);
        }
    }
}
