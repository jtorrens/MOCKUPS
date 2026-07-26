using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal interface IDictionaryValueControl
{
    event EventHandler<string>? ValueChanged;

    event EventHandler<string>? ValueCommitted;

    // Programmatic display updates must not emit change or commit events.
    void SetValue(string value);
}

internal interface IDictionaryPreviewValueControl
{
    void RefreshPreview();
}

// Composite dictionary values can change the runtime contract exposed by their
// owning record. The shell listens only to this generic structural signal and
// rebuilds the current editor surface from persisted metadata.
internal interface IDictionaryRuntimeContractValueControl
{
    event EventHandler? RuntimeContractChanged;
}

// Composite controls can opt into a local horizontal viewport without leaking
// their minimum width into the editor's main scrolling surface.
internal interface IDictionaryLocalHorizontalScrollControl
{
}
