using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorLoadingScrim : Border
{
    private readonly TextBlock _messageText;
    private Action? _cancel;

    public EditorLoadingScrim()
    {
        IsVisible = false;
        IsHitTestVisible = true;
        Focusable = true;
        Background = new SolidColorBrush(Color.FromArgb(178, 18, 20, 24));
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        _messageText = new TextBlock
        {
            Text = "Loading...",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

        Child = new Border
        {
            Width = 280,
            MaxWidth = 320,
            Padding = new Avalonia.Thickness(18),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(232, 33, 37, 43)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(62, 255, 255, 255)),
            BorderThickness = new Avalonia.Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel
            {
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Children =
                {
                    new ProgressBar
                    {
                        IsIndeterminate = true,
                        Height = 4,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                    },
                    _messageText,
                    new TextBlock
                    {
                        Text = "Esc to stop",
                        FontSize = 11,
                        Opacity = 0.72,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    },
                },
            },
        };

        KeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape)
            {
                return;
            }

            _cancel?.Invoke();
            args.Handled = true;
        };
    }

    public void Show(string message, Action? cancel)
    {
        SetMessage(message);
        _cancel = cancel;
        IsVisible = true;
        Focus();
    }

    public void SetMessage(string message)
    {
        _messageText.Text = string.IsNullOrWhiteSpace(message) ? "Loading..." : message;
    }

    public void Hide()
    {
        IsVisible = false;
        _cancel = null;
    }
}
