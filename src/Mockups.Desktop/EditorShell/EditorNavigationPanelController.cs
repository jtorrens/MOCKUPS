using Avalonia;
using Avalonia.Controls;
using Mockups.DesktopEditorShell.Common;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorNavigationPanelState(
    bool IsCollapsed,
    double ExpandedWidth);

internal sealed class EditorNavigationPanelController
{
    internal const double CollapsedRailWidth = 48;
    private const double SplitterWidth = 6;
    private readonly Grid _shellColumns;
    private readonly Control _panel;
    private readonly GridSplitter _splitter;
    private readonly Button _toggleButton;
    private readonly Func<double> _windowWidth;
    private double _expandedWidth =
        PreviewPanelLayoutPolicy.DefaultLeftColumnWidth;

    public EditorNavigationPanelController(
        Grid shellColumns,
        Control panel,
        GridSplitter splitter,
        Button toggleButton,
        Func<double> windowWidth)
    {
        _shellColumns = shellColumns;
        _panel = panel;
        _splitter = splitter;
        _toggleButton = toggleButton;
        _windowWidth = windowWidth;
        _toggleButton.Content =
            EditorIcons.Create(EditorIcons.NavigationPanel, 18);
        _toggleButton.Click += (_, _) => Toggle();
        RefreshTogglePresentation();
    }

    public bool IsCollapsed { get; private set; }

    public void Restore(
        bool isCollapsed,
        double expandedWidth)
    {
        _expandedWidth = ValidExpandedWidth(expandedWidth);
        Apply(isCollapsed, captureCurrentWidth: false);
    }

    public void EnsureVisible()
    {
        if (IsCollapsed)
        {
            Apply(isCollapsed: false, captureCurrentWidth: false);
        }
    }

    public EditorNavigationPanelState Snapshot()
    {
        if (!IsCollapsed)
        {
            CaptureExpandedWidth();
        }
        return new EditorNavigationPanelState(
            IsCollapsed,
            _expandedWidth);
    }

    private void Toggle()
    {
        Apply(!IsCollapsed, captureCurrentWidth: !IsCollapsed);
    }

    private void Apply(
        bool isCollapsed,
        bool captureCurrentWidth)
    {
        if (captureCurrentWidth)
        {
            CaptureExpandedWidth();
        }

        IsCollapsed = isCollapsed;
        if (isCollapsed)
        {
            _panel.IsVisible = false;
            _splitter.IsVisible = false;
            _shellColumns.ColumnDefinitions[0].MinWidth = 0;
            _shellColumns.ColumnDefinitions[0].Width =
                new GridLength(CollapsedRailWidth);
            _shellColumns.ColumnDefinitions[1].Width =
                new GridLength(0);
        }
        else
        {
            var editorWidth =
                _shellColumns.ColumnDefinitions[2].ActualWidth > 0
                    ? _shellColumns.ColumnDefinitions[2].ActualWidth
                    : PreviewPanelLayoutPolicy.MinimumEditorColumnWidth;
            var appliedWidth =
                PreviewPanelLayoutPolicy.ClampRestoredColumns(
                    _windowWidth(),
                    _expandedWidth,
                    editorWidth).LeftPanelWidth;
            _shellColumns.ColumnDefinitions[0].MinWidth =
                PreviewPanelLayoutPolicy.MinimumLeftColumnWidth;
            _shellColumns.ColumnDefinitions[0].Width =
                new GridLength(appliedWidth);
            _shellColumns.ColumnDefinitions[1].Width =
                new GridLength(SplitterWidth);
            _panel.IsVisible = true;
            _splitter.IsVisible = true;
        }
        RefreshTogglePresentation();
    }

    private void CaptureExpandedWidth()
    {
        var actualWidth =
            _shellColumns.ColumnDefinitions[0].ActualWidth;
        if (actualWidth > 0)
        {
            _expandedWidth = ValidExpandedWidth(actualWidth);
        }
    }

    private static double ValidExpandedWidth(double width)
    {
        return double.IsFinite(width) && width > 0
            ? System.Math.Max(
                PreviewPanelLayoutPolicy.MinimumLeftColumnWidth,
                width)
            : PreviewPanelLayoutPolicy.DefaultLeftColumnWidth;
    }

    private void RefreshTogglePresentation()
    {
        EditorAccessibility.Describe(
            _toggleButton,
            IsCollapsed
                ? "Show navigation panel"
                : "Hide navigation panel");
        _toggleButton.Margin = IsCollapsed
            ? new Thickness(0, 10, 8, 0)
            : new Thickness(0, 24, 20, 0);
    }
}
