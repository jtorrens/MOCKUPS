using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;

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
            ConfigureDesktopLifetime(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    protected virtual void ConfigureDesktopLifetime(
        IClassicDesktopStyleApplicationLifetime desktop) =>
        desktop.MainWindow = CreateMainWindow();

    protected virtual Window CreateMainWindow() =>
        throw new InvalidOperationException(
            "The desktop UI requires an executable composition host.");
}
