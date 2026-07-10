using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorGroupBlock
{
    public static Control CreatePlain(EditorLayoutGroup group, Control content)
    {
        if (string.IsNullOrWhiteSpace(group.Label) ||
            string.Equals(group.Label, "General", System.StringComparison.OrdinalIgnoreCase))
        {
            return content;
        }

        var panel = new StackPanel
        {
            Spacing = EditorUiDensity.Card(10),
        };
        panel.Children.Add(new TextBlock
        {
            Text = group.Label,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.82,
        });
        panel.Children.Add(content);
        return panel;
    }

    public static Control Create(EditorLayoutGroup group, Control content)
    {
        return CreatePanel(group.Label, content);
    }

    public static Control CreatePanel(string label, Control content)
    {
        return GroupBorder(CreateHeaderedPanel(label, content));
    }

    public static Control CreateCollapsible(EditorLayoutGroup group, Control content, out InstantEditorCard card)
    {
        card = new InstantEditorCard(
            new TextBlock
            {
                Text = group.Label,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Opacity = 0.82,
            },
            content,
            group.DefaultOpen)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        return GroupBorder(card);
    }

    public static Control CreateCollapsible(Control header, Control content, bool isExpanded = false)
    {
        return CreateCollapsible(header, content, out _, isExpanded);
    }

    public static Control CreateCollapsible(
        Control header,
        Control content,
        out InstantEditorCard card,
        bool isExpanded = false)
    {
        card = new InstantEditorCard(
            header,
            new Border { Padding = EditorUiDensity.CardThickness(10), Child = content },
            isExpanded)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        return GroupBorder(card);
    }

    public static void WireExclusiveCards(IReadOnlyList<InstantEditorCard> cards)
    {
        foreach (var card in cards)
        {
            card.Expanded += (_, _) =>
            {
                foreach (var other in cards)
                {
                    if (!ReferenceEquals(other, card))
                    {
                        other.IsExpanded = false;
                    }
                }
            };
        }

        foreach (var extraOpenCard in cards.Where((card) => card.IsExpanded).Skip(1))
        {
            extraOpenCard.IsExpanded = false;
        }
    }

    private static Control CreateHeaderedPanel(EditorLayoutGroup group, Control content)
    {
        return CreateHeaderedPanel(group.Label, content);
    }

    private static Control CreateHeaderedPanel(string label, Control content)
    {
        var panel = new StackPanel
        {
            Spacing = EditorUiDensity.Card(10),
        };

        if (!string.IsNullOrWhiteSpace(label))
        {
            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Opacity = 0.82,
            });
        }

        panel.Children.Add(content);

        return panel;
    }

    private static Control GroupBorder(Control child)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(14),
            Padding = EditorUiDensity.CardThickness(12),
            Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Child = child,
        };
    }
}
