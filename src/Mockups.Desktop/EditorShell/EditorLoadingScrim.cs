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
    private readonly TextBlock _cancelText;
    private Action? _cancel;
    private long _presentationRevision;

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
        _cancelText = new TextBlock
        {
            Text = "Esc to stop",
            FontSize = 11,
            Opacity = 0.72,
            HorizontalAlignment = HorizontalAlignment.Center,
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
                    _cancelText,
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

    public void Show(string message, Action? cancel, bool takeFocus = true)
    {
        ++_presentationRevision;
        SetMessage(message);
        _cancel = cancel;
        _cancelText.IsVisible = cancel is not null;
        IsVisible = true;
        if (takeFocus) Focus();
    }

    public void SetMessage(string message)
    {
        _messageText.Text = string.IsNullOrWhiteSpace(message) ? "Loading..." : message;
    }

    public void Hide()
    {
        ++_presentationRevision;
        IsVisible = false;
        _cancel = null;
    }

    public IDisposable BeginScope(string message)
    {
        Show(message, cancel: null, takeFocus: false);
        return new PresentationScope(this, _presentationRevision);
    }

    private sealed class PresentationScope(EditorLoadingScrim owner, long revision) : IDisposable
    {
        public void Dispose()
        {
            // A completed or canceled request cannot dismiss a newer cue.
            if (owner._presentationRevision == revision) owner.Hide();
        }
    }
}
