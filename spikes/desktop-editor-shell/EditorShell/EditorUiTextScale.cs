using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorUiTextScale
{
    private static readonly ConditionalWeakTable<AvaloniaObject, BaseFontSize> BaseFontSizes = new();

    public static void Apply(Visual root, double scale, params Visual[] excludedRoots)
    {
        var normalizedScale = Math.Clamp(scale, 0.75, 1.15);
        if (root is TemplatedControl templatedRoot)
        {
            ApplyTemplatedControl(templatedRoot, normalizedScale);
        }

        foreach (var child in root.GetVisualChildren())
        {
            ApplyRecursive(child, normalizedScale, excludedRoots);
        }
    }

    private static void ApplyRecursive(Visual visual, double scale, Visual[] excludedRoots)
    {
        if (excludedRoots.Any((excluded) => ReferenceEquals(excluded, visual)))
        {
            return;
        }

        switch (visual)
        {
            case TextBlock textBlock when textBlock.IsSet(TextBlock.FontSizeProperty):
                ApplyTextBlock(textBlock, scale);
                break;
        }

        foreach (var child in visual.GetVisualChildren())
        {
            ApplyRecursive(child, scale, excludedRoots);
        }
    }

    private static void ApplyTextBlock(TextBlock textBlock, double scale)
    {
        var baseValue = BaseFontSizes.GetValue(textBlock, static (control) => new BaseFontSize(((TextBlock)control).FontSize));
        textBlock.FontSize = Math.Round(baseValue.Value * scale, 2);
    }

    private static void ApplyTemplatedControl(TemplatedControl control, double scale)
    {
        var baseValue = BaseFontSizes.GetValue(control, static (control) => new BaseFontSize(((TemplatedControl)control).FontSize));
        control.FontSize = Math.Round(baseValue.Value * scale, 2);
    }

    private sealed class BaseFontSize
    {
        public BaseFontSize(double value)
        {
            Value = value;
        }

        public double Value { get; }
    }
}
