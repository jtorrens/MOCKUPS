using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using SukiUI.Controls;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class HexColorPickerDialog
{
    public static async Task<string?> Show(Window owner, string label, string currentValue)
    {
        var themeVariant = owner.ActualThemeVariant;
        var isLight = themeVariant == ThemeVariant.Light;
        var foreground = new SolidColorBrush(Color.Parse(isLight ? "#1F2937" : "#F1F5F9"));
        var mutedForeground = new SolidColorBrush(Color.Parse(isLight ? "#667085" : "#B8C0CE"));
        var panelBackground = new SolidColorBrush(Color.Parse(isLight ? "#FFFFFF" : "#172033"));
        var tabBackground = new SolidColorBrush(Color.Parse(isLight ? "#F3F6FA" : "#101827"));
        var selectedTabBackground = new SolidColorBrush(Color.Parse(isLight ? "#E7F1FF" : "#20314D"));
        var pointerBackground = new SolidColorBrush(Color.Parse(isLight ? "#EDF4FF" : "#263B5C"));
        var borderBrush = new SolidColorBrush(Color.Parse(isLight ? "#D0D7E2" : "#34445A"));
        var accentBrush = new SolidColorBrush(Color.Parse(isLight ? "#1368CE" : "#7DB7FF"));

        var colorView = new ColorView
        {
            Color = ParseColor(currentValue),
            IsAlphaEnabled = false,
            IsAlphaVisible = false,
            IsColorModelVisible = false,
            IsAccentColorsVisible = true,
            IsColorPaletteVisible = true,
            IsColorPreviewVisible = true,
            MinWidth = 420,
            MinHeight = 360,
        };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var panel = new DockPanel
        {
            Margin = new Thickness(16),
            LastChildFill = true,
        };
        panel.Children.Add(actions);
        panel.Children.Add(colorView);
        DockPanel.SetDock(actions, Dock.Bottom);

        var window = new SukiWindow
        {
            Title = $"Pick {label}",
            Width = 520,
            Height = 560,
            MinWidth = 520,
            MinHeight = 560,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            RequestedThemeVariant = themeVariant,
            Content = panel,
        };
        ApplyColorPickerThemeResources(
            window,
            foreground,
            mutedForeground,
            panelBackground,
            tabBackground,
            selectedTabBackground,
            pointerBackground,
            borderBrush,
            accentBrush);
        actions.Children.Add(DialogButton("Cancel", () => null));
        actions.Children.Add(DialogButton("OK", () => ColorToHex(colorView.Color)));
        return await window.ShowDialog<string?>(owner);

        Button DialogButton(string buttonLabel, Func<string?> result)
        {
            var button = new Button
            {
                Content = buttonLabel,
                MinWidth = 88,
                MinHeight = 34,
            };
            button.Click += (_, _) => window.Close(result());
            return button;
        }
    }

    private static void ApplyColorPickerThemeResources(
        Window window,
        IBrush foreground,
        IBrush mutedForeground,
        IBrush panelBackground,
        IBrush tabBackground,
        IBrush selectedTabBackground,
        IBrush pointerBackground,
        IBrush borderBrush,
        IBrush accentBrush)
    {
        window.Resources["SystemControlForegroundBaseHighBrush"] = foreground;
        window.Resources["SystemControlForegroundListLowBrush"] = mutedForeground;
        window.Resources["SystemControlBackgroundBaseLowBrush"] = tabBackground;
        window.Resources["SystemControlHighlightListAccentLowBrush"] = accentBrush;
        window.Resources["ColorViewContentBackgroundBrush"] = panelBackground;
        window.Resources["ColorViewContentBorderBrush"] = borderBrush;
        window.Resources["ColorViewTabBorderBrush"] = borderBrush;
        window.Resources["TabItemHeaderForegroundUnselected"] = mutedForeground;
        window.Resources["TabItemHeaderForegroundSelected"] = foreground;
        window.Resources["TabItemHeaderForegroundUnselectedPointerOver"] = foreground;
        window.Resources["TabItemHeaderForegroundSelectedPointerOver"] = foreground;
        window.Resources["TabItemHeaderForegroundUnselectedPressed"] = foreground;
        window.Resources["TabItemHeaderForegroundSelectedPressed"] = foreground;
        window.Resources["TabItemHeaderBackgroundUnselected"] = tabBackground;
        window.Resources["TabItemHeaderBackgroundSelected"] = selectedTabBackground;
        window.Resources["TabItemHeaderBackgroundUnselectedPointerOver"] = pointerBackground;
        window.Resources["TabItemHeaderBackgroundSelectedPointerOver"] = pointerBackground;
        window.Resources["TabItemHeaderBackgroundUnselectedPressed"] = selectedTabBackground;
        window.Resources["TabItemHeaderBackgroundSelectedPressed"] = selectedTabBackground;
        window.Resources["TabItemHeaderSelectedPipeFill"] = accentBrush;
        window.Resources["ToggleButtonForeground"] = foreground;
        window.Resources["ToggleButtonForegroundPointerOver"] = foreground;
        window.Resources["ToggleButtonForegroundPressed"] = foreground;
        window.Resources["ToggleButtonForegroundChecked"] = foreground;
        window.Resources["ToggleButtonForegroundCheckedPointerOver"] = foreground;
        window.Resources["ToggleButtonForegroundCheckedPressed"] = foreground;
        window.Resources["ToggleButtonBackground"] = tabBackground;
        window.Resources["ToggleButtonBackgroundPointerOver"] = pointerBackground;
        window.Resources["ToggleButtonBackgroundPressed"] = selectedTabBackground;
        window.Resources["ToggleButtonBackgroundChecked"] = selectedTabBackground;
        window.Resources["ToggleButtonBackgroundCheckedPointerOver"] = pointerBackground;
        window.Resources["ToggleButtonBackgroundCheckedPressed"] = selectedTabBackground;
        window.Resources["ToggleButtonBorderBrush"] = borderBrush;
        window.Resources["ToggleButtonBorderBrushPointerOver"] = accentBrush;
        window.Resources["ToggleButtonBorderBrushPressed"] = accentBrush;
        window.Resources["ToggleButtonBorderBrushChecked"] = accentBrush;
        window.Resources["ToggleButtonBorderBrushCheckedPointerOver"] = accentBrush;
        window.Resources["ToggleButtonBorderBrushCheckedPressed"] = accentBrush;
        window.Resources["TextControlForegroundDisabled"] = foreground;
        window.Resources["TextControlBackgroundDisabled"] = tabBackground;
        window.Resources["TextControlBorderBrush"] = borderBrush;
    }

    private static string ColorToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static Color ParseColor(string value)
    {
        try
        {
            return Color.Parse(string.IsNullOrWhiteSpace(value) ? "#808080" : NormalizeHex(value));
        }
        catch (FormatException)
        {
            return Color.Parse("#808080");
        }
    }

    private static string NormalizeHex(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 6 && !trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            trimmed = $"#{trimmed}";
        }

        return trimmed;
    }
}
