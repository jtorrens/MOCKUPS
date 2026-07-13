using Avalonia.Controls;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryBehaviorTimingControl : Grid, IDictionaryValueControl
{
    private readonly DictionaryOptionTokenControl _mode;
    private readonly DictionaryIntegerControl _fixedFrames;
    private readonly DictionaryThemeTokenControl _pace;
    private readonly Control _durationRow;
    private readonly Control _paceRow;
    private readonly TextBlock _durationLabel;
    private readonly Func<FieldDefinition, string, int?>? _resolveFrames;
    private readonly FieldDefinition _definition;
    private BehaviorTimingValue _value;
    private bool _updating;

    public DictionaryBehaviorTimingControl(
        FieldDefinition definition,
        string value,
        Func<string, IReadOnlyList<FieldOption>?, Task<string?>>? showThemeTokenPicker,
        Func<FieldDefinition, string, int?>? resolveFrames)
    {
        _definition = definition;
        _resolveFrames = resolveFrames;
        _value = BehaviorTimingValue.Parse(value);
        RowDefinitions = new RowDefinitions("Auto,Auto,Auto");
        RowSpacing = 6;

        _mode = new DictionaryOptionTokenControl(
            new FieldDefinition(
                $"{definition.Id}.mode",
                "Mode",
                ValueKind.OptionToken,
                definition.IsEditable,
                Options:
                [
                    new FieldOption("fixed", "Fixed"),
                    new FieldOption("natural", "Natural"),
                ]),
            _value.Mode);
        _fixedFrames = new DictionaryIntegerControl(
            new FieldDefinition(
                $"{definition.Id}.fixedFrames",
                "Duration",
                ValueKind.Integer,
                definition.IsEditable,
                Number: new NumberDefinition(0, 100000, 1, 0)),
            _value.FixedFrames.ToString());
        _pace = new DictionaryThemeTokenControl(
            new FieldDefinition(
                $"{definition.Id}.paceToken",
                "Natural pace",
                ValueKind.ThemeToken,
                definition.IsEditable,
                Options: definition.Options),
            _value.PaceToken,
            showThemeTokenPicker);

        _durationLabel = RowLabel("Duration");
        _durationRow = Row(_durationLabel, _fixedFrames, "frames");
        _paceRow = Row("Natural pace", _pace);
        Children.Add(Row("Mode", _mode));
        Children.Add(_durationRow);
        Children.Add(_paceRow);

        _mode.ValueChanged += (_, next) => Update(_value with { Mode = next }, commit: false);
        _mode.ValueCommitted += (_, next) => Update(_value with { Mode = next }, commit: true);
        _fixedFrames.ValueChanged += (_, next) => Update(_value with { FixedFrames = ParseFrames(next) }, commit: false);
        _fixedFrames.ValueCommitted += (_, next) => Update(_value with { FixedFrames = ParseFrames(next) }, commit: true);
        _pace.ValueChanged += (_, next) => Update(_value with { PaceToken = next }, commit: false);
        _pace.ValueCommitted += (_, next) => Update(_value with { PaceToken = next }, commit: true);
        RefreshVisibility();
    }

    public event EventHandler<string>? ValueChanged;
    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        _updating = true;
        _value = BehaviorTimingValue.Parse(value);
        _mode.SetValue(_value.Mode);
        _fixedFrames.SetValue(_value.FixedFrames.ToString());
        _pace.SetValue(_value.PaceToken);
        RefreshVisibility();
        _updating = false;
    }

    private void Update(BehaviorTimingValue next, bool commit)
    {
        if (_updating) return;
        _value = next;
        RefreshVisibility();
        var json = _value.ToJson();
        ValueChanged?.Invoke(this, json);
        if (commit) ValueCommitted?.Invoke(this, json);
    }

    private void RefreshVisibility()
    {
        var natural = _value.Mode == "natural";
        _paceRow.IsVisible = natural;
        _fixedFrames.IsEnabled = !natural && _definition.IsEditable;
        _durationLabel.Text = natural ? "Calculated duration" : "Duration";
        if (natural)
        {
            var frames = _resolveFrames?.Invoke(_definition, _value.ToJson()) ?? 0;
            _fixedFrames.SetValue(Math.Max(0, frames).ToString());
            Grid.SetRow(_paceRow, 1);
            Grid.SetRow(_durationRow, 2);
        }
        else
        {
            _fixedFrames.SetValue(_value.FixedFrames.ToString());
            Grid.SetRow(_durationRow, 1);
        }
    }

    private static Control Row(string label, Control control, string unit = "")
        => Row(RowLabel(label), control, unit);

    private static TextBlock RowLabel(string label) => new()
    {
        Text = label,
        Opacity = 0.72,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static Control Row(TextBlock label, Control control, string unit = "")
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(string.IsNullOrWhiteSpace(unit) ? "110,*" : "110,*,Auto"),
            ColumnSpacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(label);
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        if (!string.IsNullOrWhiteSpace(unit))
        {
            var suffix = new TextBlock { Text = unit, Opacity = 0.62, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(suffix, 2);
            row.Children.Add(suffix);
        }
        return row;
    }

    private static int ParseFrames(string value) => int.TryParse(value, out var frames) ? Math.Max(0, frames) : 0;
}
