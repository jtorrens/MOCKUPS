using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal enum PreviewSetupLayoutMode
{
    FourColumns,
    TwoColumns,
    OneColumn,
}

internal sealed record PreviewShellLayout(
    double LeftPanelWidth,
    double EditorPanelWidth,
    double PreviewPanelWidth,
    double HeaderStripWidth,
    double SetupGridWidth,
    PreviewSetupLayoutMode SetupMode,
    bool IsNavigationCollapsed);

internal sealed record RestoredShellColumns(
    double LeftPanelWidth,
    double EditorPanelWidth,
    double PreviewPanelWidth);

internal static class PreviewPanelLayoutPolicy
{
    public const double SupportedMinimumWindowWidth = 1040;
    public const double DefaultWindowWidth = 1440;
    public const double MinimumPreviewColumnWidth = 520;
    public const double MinimumPreviewUtilityHeight = 320;
    public const double MinimumTimelineSliderWidth = 240;
    public const double MinimumHeaderStripWidth = 320;
    public const double MinimumEditorColumnWidth = 280;
    public const double MinimumLeftColumnWidth = 380;
    public const double DefaultLeftColumnWidth = 380;
    public const double CollapsedNavigationRailWidth = 48;
    public const double FourColumnSetupWidth = 580;
    public const double TwoColumnSetupWidth = 280;

    private const double RootHorizontalPadding = 20;
    private const double ExpandedSplitterWidth = 12;
    private const double PreviewSplitterWidth = 6;
    private const double PreviewHeaderChrome = 80;
    private const double PreviewSetupChrome = 46;

    public static PreviewSetupLayoutMode SetupMode(double availableWidth)
    {
        if (availableWidth >= FourColumnSetupWidth)
        {
            return PreviewSetupLayoutMode.FourColumns;
        }
        if (availableWidth >= TwoColumnSetupWidth)
        {
            return PreviewSetupLayoutMode.TwoColumns;
        }
        return PreviewSetupLayoutMode.OneColumn;
    }

    public static PreviewShellLayout ForWindow(double windowWidth)
    {
        var contentWidth = Math.Max(0, windowWidth - RootHorizontalPadding);
        var isNavigationCollapsed = !CanShowExpandedNavigation(windowWidth);
        double leftWidth;
        double editorWidth;
        double previewWidth;
        if (isNavigationCollapsed)
        {
            leftWidth = CollapsedNavigationRailWidth;
            previewWidth = ClampCollapsedPreviewWidth(
                windowWidth,
                MinimumPreviewColumnWidth,
                CollapsedNavigationRailWidth,
                PreviewSplitterWidth);
            editorWidth = Math.Max(
                MinimumEditorColumnWidth,
                contentWidth
                - leftWidth
                - PreviewSplitterWidth
                - previewWidth);
        }
        else
        {
            leftWidth = DefaultLeftColumnWidth;
            var weightedWidth = Math.Max(
                0,
                contentWidth - leftWidth - ExpandedSplitterWidth);
            previewWidth = Math.Max(
                MinimumPreviewColumnWidth,
                weightedWidth / 3);
            editorWidth = Math.Max(
                MinimumEditorColumnWidth,
                weightedWidth - previewWidth);
        }

        var headerWidth = Math.Max(0, previewWidth - PreviewHeaderChrome);
        var setupWidth = Math.Max(0, headerWidth - PreviewSetupChrome);
        return new PreviewShellLayout(
            leftWidth,
            editorWidth,
            previewWidth,
            headerWidth,
            setupWidth,
            SetupMode(setupWidth),
            isNavigationCollapsed);
    }

    public static bool CanShowExpandedNavigation(double windowWidth) =>
        windowWidth
        >= RootHorizontalPadding
        + ExpandedSplitterWidth
        + MinimumLeftColumnWidth
        + MinimumEditorColumnWidth
        + MinimumPreviewColumnWidth;

    public static RestoredShellColumns ClampRestoredColumns(
        double windowWidth,
        double requestedLeftWidth,
        double requestedEditorWidth,
        double requestedPreviewWidth)
    {
        var available = Math.Max(
            MinimumLeftColumnWidth
            + MinimumEditorColumnWidth
            + MinimumPreviewColumnWidth,
            windowWidth
            - RootHorizontalPadding
            - ExpandedSplitterWidth);
        var leftWidth = Math.Max(
            MinimumLeftColumnWidth,
            requestedLeftWidth);
        var editorWidth = Math.Max(
            MinimumEditorColumnWidth,
            requestedEditorWidth);
        var previewWidth = Math.Max(
            MinimumPreviewColumnWidth,
            requestedPreviewWidth);
        var overflow = leftWidth + editorWidth + previewWidth - available;
        if (overflow > 0)
        {
            var editorReduction = Math.Min(
                overflow,
                editorWidth - MinimumEditorColumnWidth);
            editorWidth -= editorReduction;
            overflow -= editorReduction;
            var previewReduction = Math.Min(
                overflow,
                previewWidth - MinimumPreviewColumnWidth);
            previewWidth -= previewReduction;
            overflow -= previewReduction;
            var leftReduction = Math.Min(
                overflow,
                leftWidth - MinimumLeftColumnWidth);
            leftWidth -= leftReduction;
        }
        else if (overflow < 0)
        {
            editorWidth -= overflow;
        }
        return new RestoredShellColumns(
            leftWidth,
            editorWidth,
            previewWidth);
    }

    public static double ClampCollapsedPreviewWidth(
        double windowWidth,
        double requestedPreviewWidth,
        double collapsedRailWidth,
        double splitterWidth)
    {
        var maximum = Math.Max(
            MinimumPreviewColumnWidth,
            windowWidth
            - RootHorizontalPadding
            - collapsedRailWidth
            - splitterWidth
            - MinimumEditorColumnWidth);
        return Math.Clamp(
            requestedPreviewWidth,
            MinimumPreviewColumnWidth,
            maximum);
    }
}
