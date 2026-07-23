using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorContextIdentity(string Type, string Name);

internal sealed record EditorContextVariantSelector(
    IReadOnlyList<FieldOption> Options,
    string SelectedId,
    Action<string> Select);

internal sealed record EditorContextStripMetadata(
    IReadOnlyList<EditorContextIdentity> Identities,
    EditorContextVariantSelector? VariantSelector,
    int OverrideCount,
    bool IsUsed = false,
    bool IsProtected = false,
    bool IsLocked = false,
    bool IsInherited = false)
{
    public string AccessibleText
    {
        get
        {
            var parts = Identities
                .Select((identity) => $"{identity.Type}: {identity.Name}")
                .ToList();
            if (VariantSelector is not null)
            {
                parts.Add(
                    $"Variant: {VariantSelector.Options.FirstOrDefault((option) => option.Value == VariantSelector.SelectedId)?.Label ?? VariantSelector.SelectedId}");
            }
            parts.Add($"{OverrideCount} {(OverrideCount == 1 ? "override" : "overrides")}");
            if (IsUsed) parts.Add("Used");
            if (IsProtected) parts.Add("Protected");
            if (IsLocked) parts.Add("Locked");
            if (IsInherited) parts.Add("Inherited");
            return string.Join(" · ", parts);
        }
    }
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

        if (metadata.VariantSelector is not null)
        {
            content.Children.Add(VariantSelector(metadata.VariantSelector));
        }
        if (metadata.OverrideCount > 0)
        {
            content.Children.Add(Badge($"{metadata.OverrideCount} {(metadata.OverrideCount == 1 ? "override" : "overrides")}"));
        }
        if (metadata.IsUsed)
        {
            content.Children.Add(Status("Used", "#8FA99A"));
        }
        if (metadata.IsProtected)
        {
            content.Children.Add(Status("Protected", "#9CA3AF"));
        }
        if (metadata.IsLocked)
        {
            content.Children.Add(Status("Locked", "#D6A638"));
        }
        if (metadata.IsInherited)
        {
            content.Children.Add(Status("Inherited", "#9CA3AF"));
        }

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

    private static Control VariantSelector(EditorContextVariantSelector metadata)
    {
        var selectedOption = metadata.Options.FirstOrDefault((option) => option.Value == metadata.SelectedId);
        var combo = new EditorInstantComboBox
        {
            ItemsSource = metadata.Options,
            SelectedItem = selectedOption,
            Width = 210,
            Height = 30,
            VerticalAlignment = VerticalAlignment.Center,
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is not { } selected
                || selected.Value == metadata.SelectedId)
            {
                return;
            }
            metadata.Select(selected.Value);
        };
        EditorAccessibility.Describe(combo, "Select active Variant");

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "Variant:",
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                combo,
            },
        };
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
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#D6A638")),
                VerticalAlignment = VerticalAlignment.Center,
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
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
    }
}
