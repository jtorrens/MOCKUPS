using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Mockups.DesktopEditorShell;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var databasePath = DesktopEditorLaunchOptions.DatabasePath
                ?? Data.SpikeDatabase.DefaultDatabasePath();
            desktop.MainWindow = new MainWindow(databasePath);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
