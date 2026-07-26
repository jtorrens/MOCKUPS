using Avalonia.Controls;
using SukiUI.Controls;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorBusyOverlay
{
    public static BusyArea Create(Control content)
    {
        return new BusyArea
        {
            Content = content,
            BusyText = "",
            IsBusy = false,
        };
    }

    public static void SetBusy(BusyArea busyArea, bool isBusy, string message = "")
    {
        busyArea.BusyText = message;
        busyArea.IsBusy = isBusy;
    }
}
