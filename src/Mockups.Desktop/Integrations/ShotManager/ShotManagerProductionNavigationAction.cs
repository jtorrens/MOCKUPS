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
        var association = string.IsNullOrWhiteSpace(projectId)
            ? null
            : _database.GetShotManagerAssociation(projectId);
        IsAssociated = association is not null;

        var foreground = IsAssociated
            ? ConnectedForeground(_isDark())
            : EditorUiVisuals.SecondaryTextBrush(_isDark());
        _button.Foreground = foreground;
        _button.Background = Brushes.Transparent;
        _button.BorderBrush = Brushes.Transparent;
        EditorIcons.ApplyBrush(_icon, foreground);
        EditorAccessibility.Describe(
            _button,
            IsAssociated
                ? $"Shot Manager connected to {association!.ProductionName} · {association.SeasonCode}. Open association"
                : "Shot Manager is not connected. Open to associate this Production");
    }

    internal static IBrush ConnectedForeground(bool isDark) =>
        new SolidColorBrush(Color.Parse(isDark ? "#39D98A" : "#137A4B"));
}
