using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal enum EditorSubcardLayout
{
    Stacked,
    FlatStack,
    VerticalCards,
    SeparatedSections,
}

internal sealed record EditorInternalNavigationSection(
    string Id,
    string Label,
    string Subtitle,
    string Icon,
    Control Content,
    Control? Trailing = null,
    IReadOnlyList<EditorInternalNavigationSection>? Subcards = null,
    EditorSubcardLayout SubcardLayout = EditorSubcardLayout.Stacked,
    bool IsExpanded = false,
    Action<bool>? ExpansionChanged = null,
    string? SelectedSubcardId = null,
    Action<string>? SubcardSelectionChanged = null,
    double? SubcardNavigationWidth = null,
    Action<double>? SubcardNavigationWidthChanged = null,
    bool ShowLabel = true,
    bool Reveal = false);

internal sealed class EditorSubcardLayoutHost : ContentControl
{
    public EditorSubcardLayoutHost(
        IReadOnlyList<EditorInternalNavigationSection> subcards,
        EditorSubcardLayout layout,
        string? selectedId = null,
        Action<string>? selectionChanged = null,
        double? navigationWidth = null,
        Action<double>? navigationWidthChanged = null)
    {
        Layout = layout;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Top;
        Content = Compose(subcards, layout, selectedId, selectionChanged, navigationWidth, navigationWidthChanged);
    }

    public EditorSubcardLayout Layout { get; }

    internal static Control ComposeSectionContent(EditorInternalNavigationSection section)
    {
        if (section.Subcards is not { Count: > 0 }) return section.Content;
        return new StackPanel
        {
            Spacing = EditorUiDensity.Card(8),
            Children =
            {
                section.Content,
                new Border
                {
                    Height = 1,
                    Background = EditorUiVisuals.ScrollbarSeparatorBrush(
                        Application.Current?.ActualThemeVariant != Avalonia.Styling.ThemeVariant.Light),
                },
                new EditorSubcardLayoutHost(
                    section.Subcards,
                    section.SubcardLayout,
                    section.SelectedSubcardId,
                    section.SubcardSelectionChanged,
                    section.SubcardNavigationWidth,
                    section.SubcardNavigationWidthChanged),
            },
        };
    }

    private static Control Compose(
        IReadOnlyList<EditorInternalNavigationSection> subcards,
        EditorSubcardLayout layout,
        string? selectedId,
        Action<string>? selectionChanged,
        double? navigationWidth = null,
        Action<double>? navigationWidthChanged = null)
    {
        if (layout == EditorSubcardLayout.VerticalCards)
        {
            return new EditorInternalNavigation(
                subcards,
                selectedId,
                selectionChanged,
                navigationWidth,
                navigationWidthChanged);
        }

        if (layout == EditorSubcardLayout.SeparatedSections)
        {
            var sections = new StackPanel { Spacing = EditorUiDensity.Card(8) };
            foreach (var subcard in subcards)
            {
                if (subcard.ShowLabel)
                {
                    sections.Children.Add(EditorGroupBlock.CreateInlineSection(subcard.Label));
                }
                sections.Children.Add(ComposeSectionContent(subcard));
            }
            return sections;
        }

        var stack = new StackPanel { Spacing = layout == EditorSubcardLayout.FlatStack ? 0 : EditorUiDensity.Card(8) };
        if (layout == EditorSubcardLayout.FlatStack)
        {
            var cards = new List<InstantEditorCard>();
            foreach (var subcard in subcards)
            {
                stack.Children.Add(EditorGroupBlock.CreateFlatCollapsible(
                    subcard.Label,
                    subcard.Subtitle,
                    subcard.Icon,
                    ComposeSectionContent(subcard),
                    out var card,
                    isExpanded: subcard.IsExpanded,
                    headerTrailing: subcard.Trailing,
                    hierarchyIndent: EditorUiDensity.Card(12)));
                if (subcard.ExpansionChanged is not null)
                {
                    card.ExpansionChanged += subcard.ExpansionChanged;
                }
                if (subcard.Reveal)
                {
                    Dispatcher.UIThread.Post(card.BringIntoView, DispatcherPriority.Loaded);
                }
                cards.Add(card);
            }
            EditorGroupBlock.WireExclusiveCards(cards);
            return stack;
        }

        foreach (var subcard in subcards) stack.Children.Add(ComposeSectionContent(subcard));
        return stack;
    }
}

internal sealed class EditorInternalNavigation : Grid
{
    public const double DefaultNavigationWidth = 190;
    private const double MinimumNavigationWidth = 120;
    private const double MinimumContentWidth = 260;
    private const double SplitterWidth = 6;
    private const double ResponsiveHysteresis = 16;
    private readonly IReadOnlyList<EditorInternalNavigationSection> _sections;
    private readonly Action<string>? _selectionChanged;
    private readonly StackPanel _navigation = new() { Spacing = 0 };
    private readonly ScrollViewer _navigationScroller;
    private readonly Border _navigationHost;
    private readonly Border _navigationDivider;
    private readonly GridSplitter _navigationSplitter;
    private readonly Action<double>? _navigationWidthChanged;
    private readonly ContentControl _content = new();
    private readonly Border _contentHost;
    private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Border> _entries = new(StringComparer.Ordinal);
    private string _selectedId;
    private bool _isCompact;
    private double _navigationWidth;

    public EditorInternalNavigation(
        IReadOnlyList<EditorInternalNavigationSection> sections,
        string? selectedId = null,
        Action<string>? selectionChanged = null,
        double? navigationWidth = null,
        Action<double>? navigationWidthChanged = null)
    {
        _sections = sections;
        _selectionChanged = selectionChanged;
        _navigationWidthChanged = navigationWidthChanged;
        _navigationWidth = Math.Max(MinimumNavigationWidth, navigationWidth ?? DefaultNavigationWidth);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Top;
        _selectedId = sections.Any((section) => section.Id == selectedId)
            ? selectedId!
            : sections.FirstOrDefault()?.Id ?? "";

        ColumnDefinitions = new ColumnDefinitions($"{_navigationWidth},{SplitterWidth},*");
        RowDefinitions = new RowDefinitions("Auto");
        ClipToBounds = true;

        _navigationScroller = new ScrollViewer
        {
            Content = _navigation,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        _navigationHost = new Border
        {
            Padding = new Thickness(0),
            Child = _navigationScroller,
        };
        Children.Add(_navigationHost);

        _navigationDivider = new Border
        {
            Width = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Grid.SetColumn(_navigationDivider, 1);
        Children.Add(_navigationDivider);

        _navigationSplitter = new GridSplitter
        {
            Width = SplitterWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Background = Brushes.Transparent,
        };
        Grid.SetColumn(_navigationSplitter, 1);
        _navigationSplitter.PointerEntered += (_, _) =>
            _navigationDivider.Background = EditorSukiWindowTheme.AccentBrush();
        _navigationSplitter.PointerExited += (_, _) => RefreshVisuals();
        _navigationSplitter.DragCompleted += (_, _) =>
        {
            if (_isCompact) return;
            _navigationWidth = Math.Max(MinimumNavigationWidth, ColumnDefinitions[0].ActualWidth);
            _navigationWidthChanged?.Invoke(_navigationWidth);
        };
        Children.Add(_navigationSplitter);

        _contentHost = new Border
        {
            Padding = new Thickness(18, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Child = _content,
        };
        Grid.SetColumn(_contentHost, 2);
        Children.Add(_contentHost);

        foreach (var section in sections)
        {
            var button = CreateNavigationButton(section);
            _buttons[section.Id] = button;
            var entry = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = button,
            };
            _entries[section.Id] = entry;
            _navigation.Children.Add(entry);
        }

        SizeChanged += (_, args) => ApplyResponsiveLayout(args.NewSize.Width, _contentHost);
        ActualThemeVariantChanged += (_, _) => RefreshVisuals();
        Select(_selectedId, notify: false);
        RefreshVisuals();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var measured = base.MeasureOverride(new Size(availableSize.Width, double.PositiveInfinity));
        var naturalHeight = _isCompact
            ? _navigationHost.DesiredSize.Height + 1 + _contentHost.DesiredSize.Height
            : Math.Max(_navigationHost.DesiredSize.Height, _contentHost.DesiredSize.Height);
        return new Size(measured.Width, naturalHeight);
    }

    private Button CreateNavigationButton(EditorInternalNavigationSection section)
    {
        var text = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = section.Label,
                    FontWeight = FontWeight.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                new TextBlock
                {
                    Text = section.Subtitle,
                    FontSize = 11,
                    Opacity = 0.68,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            },
        };
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 10,
            Children =
            {
                EditorIcons.CreateSemantic(section.Label, section.Icon, 16),
            },
        };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);

        var button = new Button
        {
            Content = row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = 54,
            Padding = new Thickness(10, 7),
            BorderThickness = new Thickness(0, 0, 0, 0),
            Background = Brushes.Transparent,
        };
        button.Click += (_, args) =>
        {
            args.Handled = true;
            Select(section.Id, notify: true);
        };
        button.KeyDown += (_, args) => NavigateByKeyboard(section.Id, args);
        EditorAccessibility.Describe(button, $"Open {section.Label}", section.Subtitle, showToolTip: false);
        return button;
    }

    private void NavigateByKeyboard(string currentId, KeyEventArgs args)
    {
        var current = _sections.ToList().FindIndex((section) => section.Id == currentId);
        var next = args.Key switch
        {
            Key.Home => 0,
            Key.End => _sections.Count - 1,
            Key.Up when !_isCompact => current - 1,
            Key.Down when !_isCompact => current + 1,
            Key.Left when _isCompact => current - 1,
            Key.Right when _isCompact => current + 1,
            _ => current,
        };
        next = Math.Clamp(next, 0, _sections.Count - 1);
        if (next == current) return;
        args.Handled = true;
        var section = _sections[next];
        Select(section.Id, notify: true);
        _buttons[section.Id].Focus();
    }

    private void Select(string sectionId, bool notify)
    {
        var section = _sections.FirstOrDefault((candidate) => candidate.Id == sectionId);
        if (section is null) return;
        _selectedId = section.Id;
        _content.Content = null;
        var sectionContent = EditorSubcardLayoutHost.ComposeSectionContent(section);
        if (section.Trailing is null)
        {
            _content.Content = sectionContent;
        }
        else
        {
            var content = new StackPanel { Spacing = EditorUiDensity.Card(8) };
            var actions = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = section.Trailing,
            };
            content.Children.Add(actions);
            content.Children.Add(sectionContent);
            _content.Content = content;
        }
        RefreshVisuals();
        if (notify) _selectionChanged?.Invoke(section.Id);
    }

    private void ApplyResponsiveLayout(double width, Border contentHost)
    {
        var verticalMinimum = _navigationWidth + SplitterWidth + MinimumContentWidth;
        var compact = width > 0 && width < verticalMinimum + (_isCompact ? ResponsiveHysteresis : 0);
        if (_isCompact == compact) return;
        _isCompact = compact;
        if (compact)
        {
            ColumnDefinitions = new ColumnDefinitions("*");
            RowDefinitions = new RowDefinitions("Auto,1,Auto");
            _navigation.Orientation = Orientation.Horizontal;
            _navigationScroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            _navigationHost.Padding = new Thickness(0, 0, 0, 10);
            Grid.SetColumn(_navigationDivider, 0);
            Grid.SetRow(_navigationDivider, 1);
            _navigationDivider.Width = double.NaN;
            _navigationDivider.Height = 1;
            _navigationSplitter.IsVisible = false;
            Grid.SetColumn(contentHost, 0);
            Grid.SetRow(contentHost, 2);
            contentHost.Padding = new Thickness(0, 14, 0, 0);
            foreach (var button in _buttons.Values)
            {
                button.MinWidth = 142;
                button.MinHeight = 64;
                button.Padding = new Thickness(10, 9);
            }
        }
        else
        {
            ColumnDefinitions = new ColumnDefinitions($"{_navigationWidth},{SplitterWidth},*");
            RowDefinitions = new RowDefinitions("Auto");
            _navigation.Orientation = Orientation.Vertical;
            _navigationScroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _navigationHost.Padding = new Thickness(0);
            Grid.SetColumn(_navigationDivider, 1);
            Grid.SetRow(_navigationDivider, 0);
            _navigationDivider.Width = 2;
            _navigationDivider.Height = double.NaN;
            _navigationSplitter.IsVisible = true;
            Grid.SetColumn(_navigationSplitter, 1);
            Grid.SetRow(_navigationSplitter, 0);
            Grid.SetColumn(contentHost, 2);
            Grid.SetRow(contentHost, 0);
            contentHost.Padding = new Thickness(18, 0, 0, 0);
            foreach (var button in _buttons.Values)
            {
                button.ClearValue(MinWidthProperty);
                button.MinHeight = 54;
                button.Padding = new Thickness(10, 7);
            }
        }
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        var isDark = ActualThemeVariant != Avalonia.Styling.ThemeVariant.Light;
        var separator = EditorUiVisuals.ScrollbarSeparatorBrush(isDark);
        _navigationDivider.Background = separator;
        foreach (var entry in _entries.Values) entry.BorderBrush = separator;
        foreach (var section in _sections)
        {
            if (!_buttons.TryGetValue(section.Id, out var button)) continue;
            var selected = section.Id == _selectedId;
            button.Background = selected
                ? EditorSukiWindowTheme.SelectionBackgroundBrush(isDark)
                : Brushes.Transparent;
            button.BorderBrush = selected ? EditorSukiWindowTheme.AccentBrush() : Brushes.Transparent;
            button.BorderThickness = selected
                ? _isCompact ? new Thickness(0, 0, 0, 2) : new Thickness(2, 0, 0, 0)
                : new Thickness(0);
        }
    }

}
