using System;

namespace Mockups.DesktopEditorShell.Common;

internal sealed class PreviewPlaybackState
{
    public event Action? Changed;

    public bool IsBusy { get; private set; }
    public bool IsPlaying { get; private set; }

    public void SetBusy(bool isBusy)
    {
        if (IsBusy == isBusy) return;
        IsBusy = isBusy;
        Changed?.Invoke();
    }

    public void SetPlaying(bool isPlaying)
    {
        if (IsPlaying == isPlaying) return;
        IsPlaying = isPlaying;
        Changed?.Invoke();
    }

    public void NotifyFrameChanged() => Changed?.Invoke();

}
