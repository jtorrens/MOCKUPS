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
    PreviewSetupLayoutMode SetupMode);

internal sealed record RestoredShellColumns(double LeftPanelWidth, double EditorPanelWidth);

internal static class PreviewPanelLayoutPolicy
{
    public const double SupportedMinimumWindowWidth = 1040;
    public const double DefaultWindowWidth = 1440;
    public const double MinimumPreviewColumnWidth = 420;
    public const double MinimumHeaderStripWidth = 320;
    public const double MinimumEditorColumnWidth = 280;
    public const double MinimumLeftColumnWidth = 240;
    public const double DefaultLeftColumnWidth = 300;
    public const double FourColumnSetupWidth = 580;
    public const double TwoColumnSetupWidth = 280;

    private const double RootHorizontalPadding = 20;
    private const double SplitterWidth = 12;
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
        var leftWidth = DefaultLeftColumnWidth;
        var weightedWidth = Math.Max(0, contentWidth - leftWidth - SplitterWidth);
        var previewWidth = Math.Max(MinimumPreviewColumnWidth, weightedWidth / 3);
        var editorWidth = weightedWidth - previewWidth;
        if (editorWidth < MinimumEditorColumnWidth)
        {
            var deficit = MinimumEditorColumnWidth - editorWidth;
            leftWidth = Math.Max(MinimumLeftColumnWidth, leftWidth - deficit);
            weightedWidth = Math.Max(0, contentWidth - leftWidth - SplitterWidth);
            editorWidth = Math.Max(MinimumEditorColumnWidth, weightedWidth - previewWidth);
        }

        var headerWidth = Math.Max(0, previewWidth - PreviewHeaderChrome);
        var setupWidth = Math.Max(0, headerWidth - PreviewSetupChrome);
        return new PreviewShellLayout(
            leftWidth,
            editorWidth,
            previewWidth,
            headerWidth,
            setupWidth,
            SetupMode(setupWidth));
    }

    public static RestoredShellColumns ClampRestoredColumns(
        double windowWidth,
        double requestedLeftWidth,
        double requestedEditorWidth)
    {
        var available = Math.Max(
            MinimumLeftColumnWidth + MinimumEditorColumnWidth,
            windowWidth - RootHorizontalPadding - SplitterWidth - MinimumPreviewColumnWidth);
        var leftWidth = Math.Clamp(
            requestedLeftWidth,
            MinimumLeftColumnWidth,
            Math.Max(MinimumLeftColumnWidth, available - MinimumEditorColumnWidth));
        var editorWidth = Math.Clamp(
            requestedEditorWidth,
            MinimumEditorColumnWidth,
            Math.Max(MinimumEditorColumnWidth, available - leftWidth));
        if (leftWidth + editorWidth > available)
        {
            editorWidth = Math.Max(MinimumEditorColumnWidth, available - leftWidth);
        }
        return new RestoredShellColumns(leftWidth, editorWidth);
    }
}
