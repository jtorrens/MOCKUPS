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
    private readonly Button _previousFrameButton;
    private readonly Button _nextFrameButton;
    private readonly NumericUpDown _frameInput;
    private readonly Slider _frameSlider;
    private readonly TextBlock _maximumFrameText;
    private readonly Func<bool> _canRestore;
    private readonly Func<int, bool> _canStep;
    private readonly Action<string?, int> _setFrame;
    private readonly Func<int> _currentFrame;
    private readonly Func<int> _maximumFrame;
    private readonly PreviewPlaybackState _playbackState;
    private readonly IReadOnlyList<FieldOption> _targetOptions;
    private readonly string _initialTargetValue;
    private EditorInstantComboBox? _targetCombo;
    private string? _pendingTargetValue;
    private bool _wasBusy;
    private bool _isUpdatingFrame;

    public RuntimeTestActionControl(
        string label,
        Action<string?> play,
        Action restore,
        Func<bool> canRestore,
        Action<string?, int> step,
        Func<int, bool> canStep,
        Action<string?, int> setFrame,
        Func<int> currentFrame,
        Func<int> maximumFrame,
        PreviewPlaybackState playbackState,
        IReadOnlyList<FieldOption>? targetOptions = null,
        string currentTargetValue = "")
    {
        _canRestore = canRestore;
        _canStep = canStep;
        _setFrame = setFrame;
        _currentFrame = currentFrame;
        _maximumFrame = maximumFrame;
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
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto"),
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

        _previousFrameButton = CreateButton(
            EditorIcons.TimelinePreviousFrame,
            $"Previous frame · {label}");
        _previousFrameButton.Click += (_, args) =>
        {
            args.Handled = true;
            step(_targetCombo?.SelectedItem?.Value, -1);
            RefreshState();
        };
        Grid.SetColumn(_previousFrameButton, 3);
        layout.Children.Add(_previousFrameButton);

        _frameInput = EditorNumericUpDownBehavior.ConfigureCompact(new NumericUpDown
        {
            Width = EditorUiDensity.TextAwareWidth(48),
            Height = 28,
            Minimum = 0,
            Maximum = 0,
            Increment = 1,
            FormatString = "0",
            ShowButtonSpinner = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        });
        ToolTip.SetTip(_frameInput, $"Current frame · {label}");
        _frameInput.PropertyChanged += (_, change) =>
        {
            if (change.Property != NumericUpDown.ValueProperty
                || _isUpdatingFrame
                || _frameInput.Value is not { } value)
            {
                return;
            }

            _setFrame(
                _targetCombo?.SelectedItem?.Value,
                decimal.ToInt32(decimal.Truncate(value)));
            RefreshState();
        };
        Grid.SetColumn(_frameInput, 4);
        layout.Children.Add(_frameInput);

        _maximumFrameText = new TextBlock
        {
            Text = "/ 0",
            FontFamily = new FontFamily("SF Mono, Menlo, Consolas, monospace"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(_maximumFrameText, 5);
        layout.Children.Add(_maximumFrameText);

        _nextFrameButton = CreateButton(
            EditorIcons.TimelineNextFrame,
            $"Next frame · {label}");
        _nextFrameButton.Click += (_, args) =>
        {
            args.Handled = true;
            step(_targetCombo?.SelectedItem?.Value, 1);
            RefreshState();
        };
        Grid.SetColumn(_nextFrameButton, 6);
        layout.Children.Add(_nextFrameButton);

        _frameSlider = new Slider
        {
            Height = 14,
            Minimum = 0,
            Maximum = 0,
            Value = 0,
            TickFrequency = 1,
            SmallChange = 1,
            LargeChange = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(_frameSlider, $"Navigate frames · {label}");
        EditorAccessibility.Describe(
            _frameSlider,
            $"Navigate frames · {label}",
            "Move to an exact frame in the Preview action",
            showToolTip: false);
        _frameSlider.PropertyChanged += (_, change) =>
        {
            if (change.Property != Slider.ValueProperty
                || _isUpdatingFrame)
            {
                return;
            }

            _setFrame(
                _targetCombo?.SelectedItem?.Value,
                Convert.ToInt32(
                    Math.Round(
                        _frameSlider.Value,
                        MidpointRounding.AwayFromZero)));
            RefreshState();
        };
        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            RowSpacing = 4,
            Children =
            {
                layout,
                _frameSlider,
            },
        };
        Grid.SetRow(_frameSlider, 1);
        Child = content;

        PreviewPlaybackStateBinding.Attach(this, _playbackState, OnPlaybackStateChanged);
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
        _playButton.IsEnabled = true;
        _restoreButton.IsEnabled = _canRestore();
        _previousFrameButton.IsEnabled = _canStep(-1);
        _nextFrameButton.IsEnabled = _canStep(1);
        var maximumFrame = Math.Max(0, _maximumFrame());
        var currentFrame = Math.Clamp(_currentFrame(), 0, maximumFrame);
        _isUpdatingFrame = true;
        _frameInput.Maximum = maximumFrame;
        _frameInput.Value = currentFrame;
        _frameSlider.Maximum = maximumFrame;
        _frameSlider.Value = currentFrame;
        _maximumFrameText.Text = $"/ {maximumFrame}";
        _isUpdatingFrame = false;
    }
}
