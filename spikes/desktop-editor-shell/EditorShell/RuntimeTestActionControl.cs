using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RuntimeTestActionControl : Border
{
    private readonly Button _playButton;
    private readonly Button _restoreButton;
    private readonly Func<bool> _canRestore;
    private readonly PreviewPlaybackState _playbackState;

    public RuntimeTestActionControl(
        string label,
        Action play,
        Action restore,
        Func<bool> canRestore,
        PreviewPlaybackState playbackState)
    {
        _canRestore = canRestore;
        _playbackState = playbackState;
        Padding = new Thickness(8, 5);
        CornerRadius = new CornerRadius(8);
        BorderThickness = new Thickness(1);
        BorderBrush = EditorSukiWindowTheme.AccentBrush(0x70);
        Background = EditorSukiWindowTheme.AccentBrush(0x12);
        HorizontalAlignment = HorizontalAlignment.Stretch;

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Children.Add(new TextBlock
        {
            Text = label,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });

        _playButton = CreateButton(EditorIcons.Play, $"Play {label}");
        _playButton.Click += (_, args) =>
        {
            args.Handled = true;
            play();
            RefreshState();
        };
        Grid.SetColumn(_playButton, 1);
        layout.Children.Add(_playButton);

        _restoreButton = CreateButton(EditorIcons.Refresh, $"Restore {label}");
        _restoreButton.Click += (_, args) =>
        {
            args.Handled = true;
            restore();
            RefreshState();
        };
        Grid.SetColumn(_restoreButton, 2);
        layout.Children.Add(_restoreButton);
        Child = layout;

        _playbackState.Changed += OnPlaybackStateChanged;
        DetachedFromVisualTree += (_, _) => _playbackState.Changed -= OnPlaybackStateChanged;
        RefreshState();
    }

    private static Button CreateButton(string icon, string accessibleName)
    {
        var button = EditorTimelineTransport.CreateNavigationButton(
            EditorIcons.CreateSemantic(accessibleName, icon, 13),
            accessibleName,
            30);
        button.Height = 28;
        button.BorderBrush = EditorSukiWindowTheme.AccentBrush(0x70);
        button.BorderThickness = new Thickness(1);
        button.CornerRadius = new CornerRadius(6);
        ToolTip.SetTip(button, accessibleName);
        return button;
    }

    private void OnPlaybackStateChanged() => RefreshState();

    private void RefreshState()
    {
        _playButton.IsEnabled = !_playbackState.IsBusy;
        _restoreButton.IsEnabled = !_playbackState.IsBusy && _canRestore();
    }
}
