using Avalonia;
using Avalonia.Controls;
using Mockups.DesktopEditorShell.Common;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorNavigationPanelState(
    bool IsCollapsed,
    double ExpandedWidth,
    double ExpandedEditorWidth,
    double ExpandedPreviewWidth);

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
    private double _expandedEditorWidth =
        PreviewPanelLayoutPolicy.ForWindow(
            PreviewPanelLayoutPolicy.DefaultWindowWidth).EditorPanelWidth;
    private double _expandedPreviewWidth =
        PreviewPanelLayoutPolicy.ForWindow(
            PreviewPanelLayoutPolicy.DefaultWindowWidth).PreviewPanelWidth;

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
        double expandedWidth,
        double expandedEditorWidth,
        double expandedPreviewWidth)
    {
        _expandedWidth = ValidExpandedWidth(expandedWidth);
        _expandedEditorWidth = ValidEditorWidth(expandedEditorWidth);
        _expandedPreviewWidth = ValidPreviewWidth(expandedPreviewWidth);
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
            CaptureExpandedGeometry();
        }
        return new EditorNavigationPanelState(
            IsCollapsed,
            _expandedWidth,
            _expandedEditorWidth,
            _expandedPreviewWidth);
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
            CaptureExpandedGeometry();
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
            _shellColumns.ColumnDefinitions[2].Width =
                new GridLength(1, GridUnitType.Star);
            _shellColumns.ColumnDefinitions[4].MinWidth = 0;
            _shellColumns.ColumnDefinitions[4].Width =
                new GridLength(_expandedPreviewWidth);
        }
        else
        {
            var appliedColumns =
                PreviewPanelLayoutPolicy.ClampRestoredColumns(
                    _windowWidth(),
                    _expandedWidth,
                    _expandedEditorWidth);
            _shellColumns.ColumnDefinitions[0].MinWidth =
                PreviewPanelLayoutPolicy.MinimumLeftColumnWidth;
            _shellColumns.ColumnDefinitions[0].Width =
                new GridLength(appliedColumns.LeftPanelWidth);
            _shellColumns.ColumnDefinitions[1].Width =
                new GridLength(SplitterWidth);
            _shellColumns.ColumnDefinitions[2].Width =
                new GridLength(appliedColumns.EditorPanelWidth);
            _shellColumns.ColumnDefinitions[4].MinWidth =
                PreviewPanelLayoutPolicy.MinimumPreviewColumnWidth;
            _shellColumns.ColumnDefinitions[4].Width =
                new GridLength(1, GridUnitType.Star);
            _panel.IsVisible = true;
            _splitter.IsVisible = true;
        }
        RefreshTogglePresentation();
    }

    private void CaptureExpandedGeometry()
    {
        var leftWidth = _shellColumns.ColumnDefinitions[0].ActualWidth;
        if (leftWidth > 0)
        {
            _expandedWidth = ValidExpandedWidth(leftWidth);
        }
        var editorWidth = _shellColumns.ColumnDefinitions[2].ActualWidth;
        if (editorWidth > 0)
        {
            _expandedEditorWidth = ValidEditorWidth(editorWidth);
        }
        var previewWidth = _shellColumns.ColumnDefinitions[4].ActualWidth;
        if (previewWidth > 0)
        {
            _expandedPreviewWidth = ValidPreviewWidth(previewWidth);
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

    private static double ValidEditorWidth(double width)
    {
        return double.IsFinite(width) && width > 0
            ? Math.Max(
                PreviewPanelLayoutPolicy.MinimumEditorColumnWidth,
                width)
            : PreviewPanelLayoutPolicy.ForWindow(
                PreviewPanelLayoutPolicy.DefaultWindowWidth).EditorPanelWidth;
    }

    private static double ValidPreviewWidth(double width)
    {
        return double.IsFinite(width) && width > 0
            ? width
            : PreviewPanelLayoutPolicy.ForWindow(
                PreviewPanelLayoutPolicy.DefaultWindowWidth).PreviewPanelWidth;
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
