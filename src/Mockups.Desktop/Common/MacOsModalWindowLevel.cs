using Avalonia.Controls;
using Avalonia.Platform;
using System;
using System.Runtime.InteropServices;

namespace Mockups.DesktopEditorShell.Common;

internal static class MacOsModalWindowLevel
{
    private const nint ModalPanelWindowLevel = 8;

    public static void Raise(Window dialog)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        if (dialog.TryGetPlatformHandle()
                is not IMacOSTopLevelPlatformHandle handle
            || handle.NSWindow == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "The native macOS modal window is unavailable after Opened.");
        }

        var setLevel = SelRegisterName("setLevel:");
        SendInteger(handle.NSWindow, setLevel, ModalPanelWindowLevel);
        var orderFrontRegardless = SelRegisterName("orderFrontRegardless");
        Send(handle.NSWindow, orderFrontRegardless);
    }

    [DllImport(
        "/usr/lib/libobjc.A.dylib",
        EntryPoint = "sel_registerName",
        CharSet = CharSet.Ansi)]
    private static extern IntPtr SelRegisterName(string name);

    [DllImport(
        "/usr/lib/libobjc.A.dylib",
        EntryPoint = "objc_msgSend")]
    private static extern void Send(
        IntPtr receiver,
        IntPtr selector);

    [DllImport(
        "/usr/lib/libobjc.A.dylib",
        EntryPoint = "objc_msgSend")]
    private static extern void SendInteger(
        IntPtr receiver,
        IntPtr selector,
        nint value);
}
