using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Mockups.DesktopEditorShell.Data;
using SukiUI;
using SukiUI.Enums;
using System;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorThemeController
{
    private readonly Window _window;
    private readonly Border _rootShell;
    private readonly ToggleSwitch _modeSwitch;
    private readonly EditorInstantComboBox _colorCombo;
    private readonly Action _onChanged;
    private bool _isUpdating;

    public EditorThemeController(
        Window window,
        Border rootShell,
        ToggleSwitch modeSwitch,
        EditorInstantComboBox colorCombo,
        Action onChanged)
    {
        _window = window;
        _rootShell = rootShell;
        _modeSwitch = modeSwitch;
        _colorCombo = colorCombo;
        _onChanged = onChanged;

        _modeSwitch.PropertyChanged += (_, change) =>
        {
            if (_isUpdating || change.Property != ToggleSwitch.IsCheckedProperty) return;

            IsDark = _modeSwitch.IsChecked == true;
            Apply();
        };
        _colorCombo.ItemsSource = Enum.GetNames<SukiColor>()
            .Select((name) => new FieldOption(name, name));
        _colorCombo.SelectionChanged += (_, _) =>
        {
            if (_isUpdating || _colorCombo.SelectedItem is not FieldOption option) return;

            if (Enum.TryParse<SukiColor>(option.Value, out var color))
            {
                SelectedColor = color;
                Apply();
            }
        };
    }

    public bool IsDark { get; private set; } = true;

    private SukiColor SelectedColor { get; set; } = SukiColor.Blue;

    public void Apply()
    {
        var themeVariant = IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        _window.RequestedThemeVariant = themeVariant;
        Application.Current!.RequestedThemeVariant = themeVariant;
        SukiTheme.GetInstance().ChangeBaseTheme(themeVariant);
        SukiTheme.GetInstance().ChangeColorTheme(SelectedColor);
        var appBackground = new SolidColorBrush(IsDark ? Color.Parse("#181A1F") : Color.Parse("#ECEDEF"));
        _window.Background = appBackground;
        _rootShell.Background = appBackground;

        _isUpdating = true;
        try
        {
            _modeSwitch.IsChecked = IsDark;
            _colorCombo.SelectedItem = _colorCombo.ItemsSource?
                .OfType<FieldOption>()
                .FirstOrDefault((option) => option.Value == SelectedColor.ToString());
        }
        finally
        {
            _isUpdating = false;
        }
        _onChanged();
    }
}
