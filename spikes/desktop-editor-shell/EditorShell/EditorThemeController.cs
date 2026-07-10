using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using SukiUI;
using SukiUI.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorThemeController
{
    private static readonly SukiColor[] AllowedAccentColors =
    [
        SukiColor.Blue,
        SukiColor.Green,
        SukiColor.Red,
    ];

    private readonly Window _window;
    private readonly Border _rootShell;
    private readonly Action _onChanged;

    public EditorThemeController(
        Window window,
        Border rootShell,
        Action onChanged)
    {
        _window = window;
        _rootShell = rootShell;
        _onChanged = onChanged;
    }

    public bool IsDark { get; private set; } = true;

    public SukiColor SelectedColor { get; private set; } = SukiColor.Blue;

    public static IReadOnlyList<FieldOption> AccentColorOptions()
    {
        return AllowedAccentColors
            .Select((color) => new FieldOption(color.ToString(), color.ToString()))
            .ToList();
    }

    public void SetState(bool isDark, string? colorName)
    {
        IsDark = isDark;
        if (!string.IsNullOrWhiteSpace(colorName) && Enum.TryParse<SukiColor>(colorName, out var color)
            && AllowedAccentColors.Contains(color))
        {
            SelectedColor = color;
        }
    }

    public void SetDark(bool isDark)
    {
        if (IsDark == isDark) return;

        IsDark = isDark;
        Apply();
    }

    public void SetColor(string value)
    {
        if (!Enum.TryParse<SukiColor>(value, out var color) || !AllowedAccentColors.Contains(color))
        {
            return;
        }

        if (SelectedColor == color) return;

        SelectedColor = color;
        Apply();
    }

    public void Apply()
    {
        var themeVariant = IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        _window.RequestedThemeVariant = themeVariant;
        Application.Current!.RequestedThemeVariant = themeVariant;
        SukiTheme.GetInstance().ChangeBaseTheme(themeVariant);
        SukiTheme.GetInstance().ChangeColorTheme(SelectedColor);
        EditorSukiWindowTheme.SetAccentColor(SelectedColor);
        EditorSukiWindowTheme.ApplyNeutralBackground(_window, _rootShell, IsDark);
        _onChanged();
    }
}
