using System;

namespace Mockups.DesktopEditorShell.Common;

internal sealed class PreviewPlaybackState
{
    public event Action? Changed;

    public bool IsBusy { get; private set; }

    public void SetBusy(bool isBusy)
    {
        if (IsBusy == isBusy) return;
        IsBusy = isBusy;
        Changed?.Invoke();
    }

}
