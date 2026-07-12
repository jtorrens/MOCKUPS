using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal enum EditorContextSaveState
{
    Saved,
    Dirty,
}

internal sealed record EditorContextIdentity(string Type, string Name);

internal sealed record EditorContextStripMetadata(
    IReadOnlyList<EditorContextIdentity> Identities,
    int OverrideCount,
    EditorContextSaveState SaveState,
    bool IsInherited = false)
{
    public string AccessibleText => string.Join(" · ", Identities.Select((identity) => $"{identity.Type}: {identity.Name}"))
        + $" · {OverrideCount} {(OverrideCount == 1 ? "override" : "overrides")}"
        + (IsInherited ? " · Inherited" : "")
        + $" · {SaveState}";
}

internal static class EditorContextStrip
{
    public static Control Create(EditorContextStripMetadata metadata)
    {
        var content = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        foreach (var identity in metadata.Identities)
        {
            content.Children.Add(Item($"{identity.Type}: ", identity.Name));
        }

        if (metadata.OverrideCount > 0)
        {
            content.Children.Add(Badge($"{metadata.OverrideCount} {(metadata.OverrideCount == 1 ? "override" : "overrides")}"));
        }
        if (metadata.IsInherited)
        {
            content.Children.Add(Status("Inherited", "#9CA3AF"));
        }
        content.Children.Add(Status(metadata.SaveState == EditorContextSaveState.Dirty ? "Unsaved changes" : "Saved",
            metadata.SaveState == EditorContextSaveState.Dirty ? "#D6A638" : "#8FA99A"));

        var border = new Border
        {
            Padding = new Thickness(10, 6),
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#554B525D")),
            Background = new SolidColorBrush(Color.Parse("#18252A31")),
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ToolTip.SetTip(border, metadata.AccessibleText);
        return border;
    }

    private static Control Item(string prefix, string value)
    {
        return new TextBlock
        {
            Text = prefix + value,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 1, 12, 1),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 240,
        };
    }

    private static Control Badge(string text)
    {
        return new Border
        {
            Padding = new Thickness(7, 2),
            Margin = new Thickness(0, 0, 10, 0),
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(Color.Parse("#263A3324")),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#D6A638")),
            },
        };
    }

    private static Control Status(string text, string color)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse(color)),
            Margin = new Thickness(0, 2, 10, 1),
        };
    }
}
