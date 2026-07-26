using Avalonia.Controls;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.IO;

namespace Mockups.DesktopEditorShell.Integrations.ProductionOutput;

internal sealed class ProductionOutputNavigationAction
{
    internal const string CardSessionStateId =
        "layout:production-output";

    private readonly Button _button;
    private readonly ProductionOutputRootStore _roots;
    private readonly Func<bool> _isDark;
    private readonly Control _icon;

    public ProductionOutputNavigationAction(
        Button button,
        ProductionOutputRootStore roots,
        Func<bool> isDark,
        Action open)
    {
        _button = button;
        _roots = roots;
        _isDark = isDark;
        _icon = EditorIcons.CreateSemantic(
            "Production Output",
            EditorIcons.Structure,
            17);
        _button.Content = _icon;
        _button.Click += (_, _) => open();
        Refresh(null);
    }

    internal bool HasLocalRoot { get; private set; }

    public void Refresh(string? projectId)
    {
        _button.IsEnabled = !string.IsNullOrWhiteSpace(projectId);
        var root = string.IsNullOrWhiteSpace(projectId)
            ? null
            : _roots.Get(projectId);
        HasLocalRoot = !string.IsNullOrWhiteSpace(root)
            && Directory.Exists(root);

        var foreground = HasLocalRoot
            ? ConfiguredForeground(_isDark())
            : EditorUiVisuals.SecondaryTextBrush(_isDark());
        _button.Foreground = foreground;
        _button.Background = Brushes.Transparent;
        _button.BorderBrush = Brushes.Transparent;
        EditorIcons.ApplyBrush(_icon, foreground);
        EditorAccessibility.Describe(
            _button,
            HasLocalRoot
                ? $"Production Output configured at {root}. Open settings"
                : string.IsNullOrWhiteSpace(root)
                    ? "Production Output has no local root. Open settings"
                    : $"Production Output root is unavailable at {root}. Open settings");
    }

    internal static IBrush ConfiguredForeground(bool isDark) =>
        new SolidColorBrush(Color.Parse(isDark ? "#39D98A" : "#137A4B"));
}
