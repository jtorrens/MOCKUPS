using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using SukiUI.Controls;
using SukiUI.Enums;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorShellSettingsDialog
{
    private readonly Window _owner;
    private readonly EditorThemeController _themeController;
    private readonly EditorShellStateService _shellState;
    private readonly Action<bool> _applyUiDensity;
    private bool _isUpdating;

    public EditorShellSettingsDialog(
        Window owner,
        EditorThemeController themeController,
        EditorShellStateService shellState,
        Action<bool> applyUiDensity)
    {
        _owner = owner;
        _themeController = themeController;
        _shellState = shellState;
        _applyUiDensity = applyUiDensity;
    }

    public Task Show()
    {
        var dialog = new SukiWindow
        {
            Title = "Editor settings",
            Width = 520,
            Height = 430,
            MinWidth = 480,
            MinHeight = 380,
            CanResize = false,
            IsMenuVisible = false,
            BackgroundStyle = SukiBackgroundStyle.Flat,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);

        var modeSwitch = new ToggleSwitch
        {
            IsChecked = _themeController.IsDark,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var colorCombo = new EditorInstantComboBox
        {
            MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = EditorThemeController.AccentColorOptions(),
            SelectedItem = EditorThemeController.AccentColorOptions()
                .FirstOrDefault((option) => option.Value == _themeController.SelectedColor.ToString()),
        };

        modeSwitch.PropertyChanged += (_, change) =>
        {
            if (_isUpdating || change.Property != ToggleSwitch.IsCheckedProperty) return;

            _themeController.SetDark(modeSwitch.IsChecked == true);
            _shellState.SetTheme(_themeController.IsDark, _themeController.SelectedColor.ToString());
        };
        colorCombo.SelectionChanged += (_, _) =>
        {
            if (_isUpdating || colorCombo.SelectedItem is not FieldOption option) return;

            _themeController.SetColor(option.Value);
            _shellState.SetTheme(_themeController.IsDark, _themeController.SelectedColor.ToString());
        };

        var textScaleRow = CreateScaleRow(
            "UI text scale",
            _shellState.UiTextScale,
            0.5,
            1.75,
            (value) =>
            {
                _shellState.SetUiTextScale(value);
                _applyUiDensity(false);
            });
        var cardPaddingRow = CreateScaleRow(
            "Card padding",
            _shellState.UiCardPaddingScale,
            0.1,
            1.5,
            (value) =>
            {
                _shellState.SetUiCardPaddingScale(value);
                _applyUiDensity(true);
            });

        var closeButton = new Button
        {
            Content = "Close",
            MinWidth = 92,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        closeButton.Click += (_, _) => dialog.Close();

        dialog.Content = new Border
        {
            Padding = new Thickness(22),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                RowSpacing = 18,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 16,
                        Children =
                        {
                            Section(
                                "Appearance",
                                new Grid
                                {
                                    ColumnDefinitions = new ColumnDefinitions("*,160"),
                                    ColumnSpacing = 14,
                                    Children =
                                    {
                                        SettingLine("Dark mode", modeSwitch),
                                        colorCombo,
                                    },
                                }),
                            Section(
                                "UI density",
                                new StackPanel
                                {
                                    Spacing = 12,
                                    Children =
                                    {
                                        textScaleRow,
                                        cardPaddingRow,
                                    },
                                }),
                        },
                    },
                    closeButton,
                },
            },
        };
        Grid.SetColumn(colorCombo, 1);
        Grid.SetRow(closeButton, 1);

        return dialog.ShowDialog(_owner);
    }

    private Control CreateScaleRow(
        string label,
        double value,
        double minimum,
        double maximum,
        Action<double> onChanged)
    {
        var slider = new Slider
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            TickFrequency = 0.05,
            SmallChange = 0.05,
            LargeChange = 0.1,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var box = EditorNumericTextStyle.Apply(new TextBox
        {
            Text = Format(value),
            Width = 68,
            VerticalContentAlignment = VerticalAlignment.Center,
        });

        void SetValue(double nextValue, bool updateBox)
        {
            var normalized = Math.Round(Math.Clamp(nextValue, minimum, maximum) / 0.05) * 0.05;
            _isUpdating = true;
            try
            {
                slider.Value = normalized;
                if (updateBox)
                {
                    box.Text = Format(normalized);
                }
            }
            finally
            {
                _isUpdating = false;
            }

            onChanged(normalized);
        }

        slider.PropertyChanged += (_, change) =>
        {
            if (_isUpdating || change.Property != RangeBase.ValueProperty) return;

            SetValue(slider.Value, updateBox: true);
        };
        box.LostFocus += (_, _) => SetValue(NumericText.Double(box.Text ?? "", value), updateBox: true);
        box.KeyDown += (_, args) =>
        {
            if (args.Key != Avalonia.Input.Key.Enter) return;

            SetValue(NumericText.Double(box.Text ?? "", value), updateBox: true);
            args.Handled = true;
        };

        Grid.SetColumn(slider, 1);
        Grid.SetColumn(box, 2);

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("130,*,76"),
            ColumnSpacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    FontWeight = FontWeight.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                slider,
                box,
            },
        };
    }

    private static Control SettingLine(string label, Control control)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        grid.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return grid;
    }

    private static Control Section(string title, Control content)
    {
        return new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontWeight = FontWeight.Bold,
                    },
                    content,
                },
            },
        };
    }

    private static string Format(double value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
