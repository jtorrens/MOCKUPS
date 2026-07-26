using Avalonia.Media;

namespace Mockups.DesktopEditorShell.Common;

internal static class EditorAnimationVisuals
{
    public static IBrush OtherKeyframeBrush { get; } = new SolidColorBrush(Color.Parse("#7B8493"));
    public static IBrush ActiveTrackBrush { get; } = new SolidColorBrush(Color.Parse("#F0B429"));
    public static IBrush CurrentKeyframeBrush { get; } = new SolidColorBrush(Color.Parse("#2F80ED"));
    public static IBrush InactiveTrackBrush { get; } = new SolidColorBrush(Color.Parse("#8F98A8"));
    public static IBrush TimelineBrush { get; } = new SolidColorBrush(Color.Parse("#4B5563"));
    public static IBrush ReferenceDurationBrush { get; } = new SolidColorBrush(Color.FromArgb(0x30, 0x73, 0xB8, 0xFF));
}
