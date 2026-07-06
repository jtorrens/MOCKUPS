using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using SukiUI;
using SukiUI.Enums;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorThemeController
{
    private readonly Window _window;
    private readonly TextBlock _label;
    private readonly ContentControl _toggleButton;
    private readonly Action _onChanged;

    public EditorThemeController(
        Window window,
        TextBlock label,
        ContentControl toggleButton,
        Action onChanged)
    {
        _window = window;
        _label = label;
        _toggleButton = toggleButton;
        _onChanged = onChanged;
    }

    public bool IsDark { get; private set; } = true;

    public void Toggle()
    {
        IsDark = !IsDark;
        Apply();
    }

    public void Apply()
    {
        var themeVariant = IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        _window.RequestedThemeVariant = themeVariant;
        Application.Current!.RequestedThemeVariant = themeVariant;
        SukiTheme.GetInstance().ChangeBaseTheme(themeVariant);
        SukiTheme.GetInstance().ChangeColorTheme(SukiColor.Blue);

        if (IsDark)
        {
            _label.Text = "Dark mode";
            _toggleButton.Content = "Switch to light";
            _onChanged();
            return;
        }

        _label.Text = "Light mode";
        _toggleButton.Content = "Switch to dark";
        _onChanged();
    }
}
