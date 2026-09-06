using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Globalization;
using System.Threading.Tasks;

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

internal static class StartupRestoreDialogs
{
    public static Task<bool> ConfirmAsync(
        Window owner,
        PendingRestore pending)
    {
        var summary = pending.Summary;
        var createdAt = DateTimeOffset.Parse(
                summary.CreatedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind)
            .ToLocalTime()
            .ToString("g", CultureInfo.CurrentCulture);
        var dialog = Create(
            "MOCKUPS · Restore backup",
            600,
            390);
        var cancel = new Button
        {
            Content = "Cancel",
            MinWidth = 92,
        };
        var restore = new Button
        {
            Content = "Restore",
            MinWidth = 104,
        };
        cancel.Click += (_, _) => dialog.Close(false);
        restore.Click += (_, _) => dialog.Close(true);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(30),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = "Restore this MOCKUPS backup?",
                    FontSize = 22,
                    FontWeight = FontWeight.Bold,
                },
                new TextBlock
                {
                    Text =
                        $"Created: {createdAt}\nReason: {summary.Reason}\nSchema: {summary.SnapshotSchemaVersion}\nSize: {FormatBytes(summary.TotalBytes)}",
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text =
                        "MOCKUPS will first back up the current database so you can restore it again later. Only backups matching this app's current schema can be applied.",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.75,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { cancel, restore },
                },
            },
        };
        return dialog.ShowDialog<bool>(owner);
    }

    public static Task ShowNotificationAsync(
        Window owner,
        RestoreNotification notification)
    {
        var dialog = Create(
            $"MOCKUPS · {notification.Title}",
            560,
            270);
        var close = new Button
        {
            Content = "OK",
            MinWidth = 92,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(30),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text = notification.Title,
                    FontSize = 22,
                    FontWeight = FontWeight.Bold,
                    Foreground = notification.IsError
                        ? Brushes.IndianRed
                        : null,
                },
                new TextBlock
                {
                    Text = notification.Message,
                    TextWrapping = TextWrapping.Wrap,
                },
                close,
            },
        };
        return dialog.ShowDialog(owner);
    }

    private static Window Create(
        string title,
        double width,
        double height) =>
        new()
        {
            Title = title,
            Width = width,
            Height = height,
            CanResize = false,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,
        };

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }
        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024d:0.0} KB";
        }
        return $"{bytes / (1024d * 1024d):0.0} MB";
    }
}
