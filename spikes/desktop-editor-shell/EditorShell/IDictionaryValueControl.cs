using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal interface IDictionaryValueControl
{
    event EventHandler<string>? ValueChanged;

    event EventHandler<string>? ValueCommitted;

    // Programmatic display updates must not emit change or commit events.
    void SetValue(string value);
}
