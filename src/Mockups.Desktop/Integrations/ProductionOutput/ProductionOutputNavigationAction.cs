using Avalonia.Controls;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
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
    private readonly ShotManagerDocumentStore _shotManagerDocuments;
    private readonly IProductionRecordFieldStore _production;
    private readonly Func<bool> _isDark;
    private readonly Control _icon;

    public ProductionOutputNavigationAction(
        Button button,
        ProductionOutputRootStore roots,
        ShotManagerDocumentStore shotManagerDocuments,
        IProductionRecordFieldStore production,
        Func<bool> isDark,
        Action open)
    {
        _button = button;
        _roots = roots;
        _shotManagerDocuments = shotManagerDocuments;
        _production = production;
        _isDark = isDark;
        _icon = EditorIcons.CreateSemantic(
            "Production Output",
            EditorIcons.Structure,
            17);
        _button.Content = _icon;
        _button.Click += (_, _) => open();
        Refresh(null);
    }

    internal bool HasLocalOutput { get; private set; }

    public void Refresh(string? projectId)
    {
        _button.IsEnabled = !string.IsNullOrWhiteSpace(projectId);
        var shotManaged = !string.IsNullOrWhiteSpace(projectId)
            && _production.GetProjectSettings(projectId)
                .ShotManagerOutput.Enabled;
        var location = string.IsNullOrWhiteSpace(projectId)
            ? null
            : shotManaged
                ? _shotManagerDocuments.GetRoot(projectId)
                : _roots.Get(projectId);
        HasLocalOutput = !string.IsNullOrWhiteSpace(location)
            && Directory.Exists(location);
        var liveDocument = string.IsNullOrWhiteSpace(projectId)
            ? null
            : _shotManagerDocuments.Get(projectId);
        var connected = !string.IsNullOrWhiteSpace(liveDocument)
            && File.Exists(liveDocument);

        var foreground = HasLocalOutput
            ? ConfiguredForeground(_isDark())
            : EditorUiVisuals.SecondaryTextBrush(_isDark());
        _button.Foreground = foreground;
        _button.Background = Brushes.Transparent;
        _button.BorderBrush = Brushes.Transparent;
        EditorIcons.ApplyBrush(_icon, foreground);
        EditorAccessibility.Describe(
            _button,
            HasLocalOutput
                ? shotManaged && !connected
                    ? $"Production Output is associated offline at {location}. Open settings"
                    : $"Production Output configured at {location}. Open settings"
                : string.IsNullOrWhiteSpace(location)
                    ? shotManaged
                        ? "Production Output has no local Shot Manager root. Open settings"
                        : "Production Output has no local root. Open settings"
                    : $"Production Output location is unavailable at {location}. Open settings");
    }

    internal static IBrush ConfiguredForeground(bool isDark) =>
        new SolidColorBrush(Color.Parse(isDark ? "#39D98A" : "#137A4B"));
}
