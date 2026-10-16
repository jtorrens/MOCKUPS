using Avalonia;
using System;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorTimelineTransport
{
    // One pending presentation for track activation and keyframe actions.
    // This acknowledges input immediately without publishing unconfirmed data.
    public static void BindAnimationAction(
        Button button, Func<Task> action, Action<Exception> onError)
    {
        var pending = false;
        button.Click += async (_, args) =>
        {
            args.Handled = true;
            if (pending) return;
            pending = true;
            var content = button.Content;
            var name = AutomationProperties.GetName(button);
            var help = AutomationProperties.GetHelpText(button);
            var tip = ToolTip.GetTip(button);
            var enabled = button.IsEnabled;
            var indicator = new ProgressBar
            {
                IsIndeterminate = true, Width = 16, Height = 4,
                VerticalAlignment = VerticalAlignment.Center,
            };
            button.IsEnabled = false;
            button.Content = indicator;
            EditorAccessibility.Describe(button, "Saving animation…");
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                await action();
            }
            catch (Exception exception)
            {
                onError(exception);
            }
            finally
            {
                pending = false;
                if (ReferenceEquals(button.Content, indicator)) button.Content = content;
                if (AutomationProperties.GetName(button) == "Saving animation…")
                {
                    AutomationProperties.SetName(button, name);
                    AutomationProperties.SetHelpText(button, help);
                    ToolTip.SetTip(button, tip);
                }
                button.IsEnabled = enabled;
            }
        };
    }

    public static ToggleButton CreateLoopButton(PreviewPlaybackState state)
    {
        var button = new ToggleButton
        {
            Content = EditorIcons.Create(EditorIcons.Loop, 16),
            Width = 34,
            Height = 30,
            Padding = new Thickness(0),
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        EditorAccessibility.Describe(button, "Loop Preview", "Repeat the current Preview scope; session only");
        button.Click += (_, _) => state.SetLooping(button.IsChecked == true);
        PreviewPlaybackStateBinding.Attach(button, state, () =>
        {
            button.IsChecked = state.IsLooping;
            button.Background = state.IsLooping ? EditorSukiWindowTheme.AccentBrush() : Brushes.Transparent;
            ToolTip.SetTip(button, state.IsLooping ? "Loop Preview: on" : "Loop Preview: off");
        });
        return button;
    }

    public static Button CreateNavigationButton(Control icon, string accessibleName, double width = 34)
    {
        var button = new Button
        {
            Content = icon,
            Width = width,
            Height = 30,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        EditorAccessibility.Describe(button, accessibleName);
        return button;
    }

    public static void ApplyPrimaryStyle(Button button)
    {
        button.Background = EditorSukiWindowTheme.AccentBrush();
        button.Foreground = new SolidColorBrush(Color.Parse("#E6E6E6"));
        button.BorderBrush = Brushes.Transparent;
        button.BorderThickness = new Thickness(0);
    }

    public static Control CreateKeyframeStepIcon(bool next)
    {
        var chevron = EditorIcons.Create(
            next ? EditorIcons.TimelineNextFrame : EditorIcons.TimelinePreviousFrame,
            12);
        var diamond = CreateKeyframeGlyph(
            filled: true,
            size: 14,
            brush: EditorAnimationVisuals.OtherKeyframeBrush);
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (next)
        {
            content.Children.Add(chevron);
            content.Children.Add(diamond);
        }
        else
        {
            content.Children.Add(diamond);
            content.Children.Add(chevron);
        }
        return content;
    }

    public static Control CreateKeyframeGlyph(
        bool filled,
        double size = 16,
        IBrush? brush = null)
    {
        var icon = EditorIcons.Create(
            filled ? EditorIcons.TimelineKeyframe : EditorIcons.TimelineKeyframeEmpty,
            size);
        EditorIcons.ApplyBrush(icon, brush);
        return icon;
    }

    public static Control CreateAnimationActivationGlyph(
        bool filled,
        bool extendsOwnerDuration,
        double size = 16,
        IBrush? brush = null)
    {
        if (extendsOwnerDuration) return CreateKeyframeGlyph(filled, size, brush);
        var color = brush ?? EditorAnimationVisuals.InactiveTrackBrush;
        return new Ellipse
        {
            Width = size * 0.68,
            Height = size * 0.68,
            Fill = filled ? color : Brushes.Transparent,
            Stroke = color,
            StrokeThickness = 1.4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    public static Control CreateSeparator(bool isDark, double height = 22) => new Border
    {
        Width = 1,
        Height = height,
        Margin = new Thickness(2, 0),
        Background = EditorUiVisuals.ScrollbarSeparatorBrush(isDark),
        VerticalAlignment = VerticalAlignment.Center,
    };
}
