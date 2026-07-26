using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Mockups.DesktopEditorShell;

internal sealed class StartupStatusWindow : Window
{
    public StartupStatusWindow()
    {
        Title = "MOCKUPS";
        Width = 520;
        Height = 220;
        CanResize = false;
        WindowStartupLocation =
            WindowStartupLocation.CenterScreen;
        Content = new StackPanel
        {
            Margin = new Thickness(32),
            Spacing = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "Opening MOCKUPS",
                    FontSize = 22,
                    FontWeight = FontWeight.Bold,
                },
                new ProgressBar
                {
                    IsIndeterminate = true,
                    Height = 5,
                },
                new TextBlock
                {
                    Text =
                        "Validating the Preview bundle and current project database…",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.72,
                },
            },
        };
    }
}

internal sealed class StartupRecoveryWindow : Window
{
    public StartupRecoveryWindow(StartupResult result)
    {
        Title = "MOCKUPS · Startup recovery";
        Width = 620;
        Height = 320;
        MinWidth = 520;
        MinHeight = 260;
        WindowStartupLocation =
            WindowStartupLocation.CenterScreen;
        var close = new Button
        {
            Content = "Close",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 88,
        };
        close.Click += (_, _) => Close();
        Content = new StackPanel
        {
            Margin = new Thickness(32),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = "MOCKUPS could not open",
                    FontSize = 22,
                    FontWeight = FontWeight.Bold,
                },
                new TextBlock
                {
                    Text = StartupResultMessage.For(result),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.IndianRed,
                },
                new TextBlock
                {
                    Text =
                        "The project was not opened and no recovery or migration was attempted.",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.72,
                },
                close,
            },
        };
    }
}
