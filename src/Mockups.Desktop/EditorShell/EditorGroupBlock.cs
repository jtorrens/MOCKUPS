using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Mockups.DesktopEditorShell.Common;
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

    public static Control CreateInlineSection(string label)
    {
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = EditorUiDensity.Card(8),
            Margin = EditorUiDensity.CardThickness(0, 6, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.66,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var separator = new Border
        {
            Height = 1,
            Background = SeparatorBrush(),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(separator, 1);
        header.Children.Add(separator);
        return header;
    }

    public static Control CreateSeparator()
    {
        return new Border
        {
            Height = 1,
            Background = SeparatorBrush(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
    }

    public static Control CreatePairColumnHeader(PairFieldLabels labels)
    {
        var pairHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = EditorUiDensity.Card(10),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        pairHeader.Children.Add(PairHeaderLabel(labels.First, 0));
        pairHeader.Children.Add(PairHeaderLabel(labels.Second, 1));

        var header = new Grid
        {
            ColumnDefinitions = DictionaryFieldLayoutRules.Columns(),
            ColumnSpacing = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        Grid.SetColumn(pairHeader, 1);
        header.Children.Add(pairHeader);
        return header;
    }

    private static TextBlock PairHeaderLabel(string label, int column)
    {
        var text = new TextBlock
        {
            Text = label,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.66,
            Margin = EditorUiDensity.CardThickness(10, 0, 10, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(text, column);
        return text;
    }

    public static Control CreateFlatCollapsible(
        string label,
        string subtitle,
        string icon,
        Control content,
        out InstantEditorCard card,
        bool isExpanded = false,
        Control? headerTrailing = null,
        double hierarchyIndent = 0)
    {
        card = new InstantEditorCard(
            EditorCardHeader.Create(
                label,
                subtitle,
                EditorIcons.CreateSemantic(label, icon, 15)),
            new Border
            {
                Padding = EditorUiDensity.CardThickness(10, 4, 10, 12),
                Child = content,
            },
            isExpanded,
            headerTrailing)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        return new Border
        {
            BorderBrush = SeparatorBrush(),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = hierarchyIndent > 0
                ? new Border
                {
                    Padding = new Thickness(hierarchyIndent, 0, 0, 0),
                    Child = card,
                }
                : card,
        };
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
            isExpanded: false)
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
        bool isExpanded = false,
        Control? headerTrailing = null)
    {
        card = new InstantEditorCard(
            header,
            new Border { Padding = EditorUiDensity.CardThickness(10), Child = content },
            isExpanded,
            headerTrailing)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        return GroupBorder(card);
    }

    public static Control CreateNestedCard(
        Control header,
        Control content,
        out InstantEditorCard card,
        bool isExpanded = false,
        Control? headerTrailing = null)
    {
        card = new InstantEditorCard(
            header,
            new Border { Padding = EditorUiDensity.CardThickness(10), Child = content },
            isExpanded,
            headerTrailing)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        return NestedCardSurface(card);
    }

    public static Control CreateInheritedCard(
        Control header,
        Control content,
        out InstantEditorCard card,
        bool isExpanded = false,
        Control? headerTrailing = null)
    {
        card = new InstantEditorCard(
            header,
            new Border { Padding = EditorUiDensity.CardThickness(10), Child = content },
            isExpanded,
            headerTrailing)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        card.SetContentSeparator(SeparatorBrush());
        return new Border
        {
            BorderBrush = SeparatorBrush(),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = card,
        };
    }

    public static Control CreateNestedCard(
        Control header,
        IReadOnlyList<EditorInternalNavigationSection> subcards,
        EditorSubcardLayout subcardLayout,
        out InstantEditorCard card,
        bool isExpanded = false,
        string? selectedSubcardId = null,
        Action<string>? subcardSelectionChanged = null,
        Control? footer = null,
        Control? headerTrailing = null)
    {
        card = new InstantEditorCard(
            header,
            subcards,
            subcardLayout,
            isExpanded,
            selectedSubcardId,
            subcardSelectionChanged,
            footer,
            headerTrailing)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        if (subcardLayout == EditorSubcardLayout.FlatStack)
        {
            card.SetContentSeparator(SeparatorBrush());
            return card;
        }
        return NestedCardSurface(card);
    }

    public static Control NestedCardSurface(InstantEditorCard card)
    {
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

    public static void ApplyContentSeparator(InstantEditorCard card)
    {
        card.SetContentSeparator(SeparatorBrush());
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
            BoxShadow = BoxShadows.Parse("0 3 8 0 #18000000"),
            Child = child,
        };
    }

    private static IBrush SeparatorBrush()
    {
        var isDark = Application.Current?.ActualThemeVariant != ThemeVariant.Light;
        return EditorUiVisuals.ScrollbarSeparatorBrush(isDark);
    }
}
