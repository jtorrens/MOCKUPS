using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class TimelineSliderMagnet
{
    private const double CapturePixels = 4;
    private const double EscapePixels = 10;
    private readonly Slider _slider;
    private readonly Func<IReadOnlyList<int>> _keyframes;
    private bool _dragging;
    private double? _capturedFrame;
    private double? _lastRawValue;

    public TimelineSliderMagnet(Slider slider, Func<IReadOnlyList<int>> keyframes)
    {
        _slider = slider;
        _keyframes = keyframes;
        _slider.PointerPressed += (_, _) =>
        {
            _dragging = true;
            _capturedFrame = null;
            _lastRawValue = _slider.Value;
        };
        _slider.PointerReleased += (_, _) => Reset();
        _slider.PointerCaptureLost += (_, _) => Reset();
    }

    public double Resolve(double rawValue)
    {
        if (!_dragging || _slider.Maximum <= _slider.Minimum)
        {
            _lastRawValue = rawValue;
            return rawValue;
        }

        var width = Math.Max(1, _slider.Bounds.Width);
        var unitsPerPixel = (_slider.Maximum - _slider.Minimum) / width;
        var captureDistance = Math.Max(0.5, CapturePixels * unitsPerPixel);
        var escapeDistance = Math.Max(1, EscapePixels * unitsPerPixel);
        if (_capturedFrame is double captured)
        {
            _lastRawValue = rawValue;
            if (Math.Abs(rawValue - captured) <= escapeDistance) return captured;
            _capturedFrame = null;
            return rawValue;
        }

        var keyframes = _keyframes()
            .Where((frame) => frame >= _slider.Minimum && frame <= _slider.Maximum)
            .Distinct()
            .OrderBy((frame) => frame)
            .ToList();
        var nearest = keyframes
            .OrderBy((frame) => Math.Abs(frame - rawValue))
            .FirstOrDefault();
        var hasNearest = keyframes.Count > 0 && Math.Abs(nearest - rawValue) <= captureDistance;
        if (!hasNearest && _lastRawValue is double previous)
        {
            var crossed = keyframes.Cast<int?>().FirstOrDefault((frame) =>
                frame is not null
                && frame.Value >= Math.Min(previous, rawValue)
                && frame.Value <= Math.Max(previous, rawValue));
            if (crossed is not null)
            {
                nearest = crossed.Value;
                hasNearest = true;
            }
        }
        _lastRawValue = rawValue;
        if (!hasNearest) return rawValue;
        _capturedFrame = nearest;
        return nearest;
    }

    private void Reset()
    {
        _dragging = false;
        _capturedFrame = null;
        _lastRawValue = null;
    }
}
