using Avalonia.Controls;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using System;

namespace Mockups.DesktopEditorShell.Integrations.ShotManager;

internal sealed class ShotManagerProductionNavigationAction
{
    private readonly Button _button;
    private readonly SpikeDatabase _database;
    private readonly Func<bool> _isDark;
    private readonly Control _icon;

    public ShotManagerProductionNavigationAction(
        Button button,
        SpikeDatabase database,
        Func<bool> isDark,
        Action open)
    {
        _button = button;
        _database = database;
        _isDark = isDark;
        _icon = EditorIcons.CreateSemantic(
            "Shot Manager",
            EditorIcons.Structure,
            17);
        _button.Content = _icon;
        _button.Click += (_, _) => open();
        Refresh(null);
    }

    internal bool IsAssociated { get; private set; }

    public void Refresh(string? projectId)
    {
        _button.IsEnabled = !string.IsNullOrWhiteSpace(projectId);
        IsAssociated = !string.IsNullOrWhiteSpace(projectId)
            && _database.GetShotManagerAssociation(projectId) is not null;

        var foreground = IsAssociated
            ? AmberForeground(_isDark())
            : EditorUiVisuals.SecondaryTextBrush(_isDark());
        _button.Foreground = foreground;
        _button.Background = IsAssociated
            ? AmberBackground(_isDark())
            : Brushes.Transparent;
        _button.BorderBrush = Brushes.Transparent;
        EditorIcons.ApplyBrush(_icon, foreground);
        EditorAccessibility.Describe(
            _button,
            IsAssociated
                ? "Shot Manager connected. Open association"
                : "Connect this Production to Shot Manager");
    }

    internal static IBrush AmberForeground(bool isDark) =>
        new SolidColorBrush(Color.Parse(isDark ? "#F0B429" : "#A56600"));

    internal static IBrush AmberBackground(bool isDark) =>
        new SolidColorBrush(Color.Parse(isDark ? "#463711" : "#F2DEAA"));
}
