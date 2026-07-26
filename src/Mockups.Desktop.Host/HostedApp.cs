using Avalonia.Controls;

namespace Mockups.DesktopEditorShell;

internal sealed class HostedApp : App
{
    protected override Window CreateMainWindow()
    {
        var databasePath = DesktopEditorLaunchOptions.DatabasePath
            ?? DesktopHost.DefaultDatabasePath();
        return DesktopHost.CreateWindow(databasePath);
    }
}
