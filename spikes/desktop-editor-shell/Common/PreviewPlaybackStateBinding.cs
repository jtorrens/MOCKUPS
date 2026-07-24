using Avalonia.Controls;
using System;

namespace Mockups.DesktopEditorShell.Common;

internal static class PreviewPlaybackStateBinding
{
    public static void Attach(
        Control control,
        PreviewPlaybackState playbackState,
        Action refresh)
    {
        var subscribed = false;

        void Subscribe()
        {
            if (subscribed) return;
            playbackState.Changed += refresh;
            subscribed = true;
            refresh();
        }

        void Unsubscribe()
        {
            if (!subscribed) return;
            playbackState.Changed -= refresh;
            subscribed = false;
        }

        control.AttachedToVisualTree += (_, _) => Subscribe();
        control.DetachedFromVisualTree += (_, _) => Unsubscribe();
        Subscribe();
    }
}
