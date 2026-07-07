using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SukiUI.Controls;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorCardHostController
{
    private readonly Panel _host;
    private readonly List<InstantEditorCard> _cards = [];

    public EditorCardHostController(Panel host)
    {
        _host = host;
    }

    public IReadOnlyList<InstantEditorCard> Cards => _cards;

    public void Clear()
    {
        _cards.Clear();
        _host.Children.Clear();
    }

    public void Add(InstantEditorCard card)
    {
        card.Expanded += (_, _) =>
        {
            foreach (var other in _cards.Where((item) => item != card))
            {
                other.IsExpanded = false;
            }
        };

        _cards.Add(card);
        _host.Children.Add(new Border
        {
            Margin = new Thickness(0, 0, 0, 12),
            CornerRadius = new CornerRadius(14),
            BoxShadow = BoxShadows.Parse("0 6 14 0 #22000000"),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Child = new GlassCard
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                Content = card,
            },
        });
    }
}
