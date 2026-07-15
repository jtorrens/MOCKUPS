using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RuntimeTestActionControl : Border
{
    private readonly Button _playButton;
    private readonly Button _restoreButton;
    private readonly Func<bool> _canRestore;
    private readonly PreviewPlaybackState _playbackState;
    private readonly IReadOnlyList<FieldOption> _targetOptions;
    private readonly string _initialTargetValue;
    private EditorInstantComboBox? _targetCombo;
    private string? _pendingTargetValue;
    private bool _wasBusy;

    public RuntimeTestActionControl(
        string label,
        Action<string?> play,
        Action restore,
        Func<bool> canRestore,
        PreviewPlaybackState playbackState,
        IReadOnlyList<FieldOption>? targetOptions = null,
        string currentTargetValue = "")
    {
        _canRestore = canRestore;
        _playbackState = playbackState;
        _targetOptions = targetOptions ?? [];
        _initialTargetValue = currentTargetValue;
        Padding = new Thickness(8, 5);
        CornerRadius = new CornerRadius(8);
        BorderThickness = new Thickness(1);
        BorderBrush = EditorSukiWindowTheme.AccentBrush(0x70);
        Background = EditorSukiWindowTheme.AccentBrush(0x12);
        HorizontalAlignment = HorizontalAlignment.Left;

        var hasTargetOptions = targetOptions is { Count: > 0 };
        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (hasTargetOptions)
        {
            var options = targetOptions!;
            _targetCombo = new EditorInstantComboBox
            {
                ItemsSource = options,
                SelectedItem = options.FirstOrDefault((option) => option.Value != currentTargetValue)
                    ?? options.First(),
                DisabledValues = string.IsNullOrWhiteSpace(currentTargetValue) ? [] : [currentTargetValue],
                MinWidth = 0,
                MaxWidth = 360,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            layout.Children.Add(_targetCombo);
        }
        else
        {
            layout.Children.Add(new TextBlock
            {
                Text = label,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        _playButton = CreateButton(EditorIcons.Play, $"Play {label}");
        _playButton.Click += (_, args) =>
        {
            args.Handled = true;
            _pendingTargetValue = _targetCombo?.SelectedItem?.Value;
            play(_pendingTargetValue);
            RefreshState();
        };
        Grid.SetColumn(_playButton, 1);
        layout.Children.Add(_playButton);

        _restoreButton = CreateButton(EditorIcons.Refresh, $"Restore {label}");
        _restoreButton.Click += (_, args) =>
        {
            args.Handled = true;
            restore();
            _pendingTargetValue = null;
            UpdateTargetCombo(_initialTargetValue);
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

    private void OnPlaybackStateChanged()
    {
        if (_wasBusy && !_playbackState.IsBusy && !string.IsNullOrWhiteSpace(_pendingTargetValue))
        {
            UpdateTargetCombo(_pendingTargetValue);
            _pendingTargetValue = null;
        }
        _wasBusy = _playbackState.IsBusy;
        RefreshState();
    }

    private void UpdateTargetCombo(string currentValue)
    {
        if (_targetCombo is null || _targetOptions.Count == 0) return;
        _targetCombo.DisabledValues = string.IsNullOrWhiteSpace(currentValue) ? [] : [currentValue];
        _targetCombo.SelectedItem = _targetOptions.FirstOrDefault((option) => option.Value != currentValue)
            ?? _targetOptions.First();
    }

    private void RefreshState()
    {
        _playButton.IsEnabled = !_playbackState.IsBusy;
        _restoreButton.IsEnabled = !_playbackState.IsBusy && _canRestore();
    }
}
