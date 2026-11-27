using Avalonia.Controls;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Globalization;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EventBurstFields : StackPanel
{
    private readonly DictionaryFieldControl _count;
    private readonly DictionaryFieldControl _interval;
    public event Action? Committed;

    public EventBurstFields(EventBurst burst)
    {
        Spacing = 4;
        _count = Integer("count", "Count", burst.Count, 1, EventBurst.MaximumCount);
        _interval = Integer("intervalFrames", "Interval (frames)", burst.IntervalFrames, 0, EventBurst.MaximumInterval);
        Children.Add(_count);
        Children.Add(_interval);
    }

    private DictionaryFieldControl Integer(string id, string label, int value, int minimum, int maximum)
    {
        var definition = new FieldDefinition($"animation.burst.{id}", label, ValueKind.Integer,
            DefaultValue: minimum.ToString(CultureInfo.InvariantCulture), Number: new NumberDefinition(minimum, maximum));
        var control = new DictionaryFieldControl(new FieldValue(definition, value.ToString(CultureInfo.InvariantCulture)));
        control.ValueCommitted += (_, _) => Committed?.Invoke();
        return control;
    }

    public EventBurst Value => new(int.Parse(_count.Value, CultureInfo.InvariantCulture),
        int.Parse(_interval.Value, CultureInfo.InvariantCulture));

    public void Present(EventBurst burst)
    {
        _count.SetValue(burst.Count.ToString(CultureInfo.InvariantCulture));
        _interval.SetValue(burst.IntervalFrames.ToString(CultureInfo.InvariantCulture));
    }
}
