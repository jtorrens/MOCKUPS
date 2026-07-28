using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using System;
using System.Runtime.CompilerServices;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorSliderBehavior
{
    private static readonly ConditionalWeakTable<Slider, PenDragState> States = new();

    public static Slider Configure(Slider slider)
    {
        States.GetValue(slider, static configuredSlider =>
        {
            var state = new PenDragState(configuredSlider);
            state.Attach();
            return state;
        });
        return slider;
    }

    internal static bool IsConfigured(Slider slider) =>
        States.TryGetValue(slider, out _);

    internal static double ValueFromPosition(
        double minimum,
        double maximum,
        double position,
        double extent,
        bool vertical,
        bool reversed)
    {
        if (extent <= 0 || maximum <= minimum)
        {
            return minimum;
        }

        var ratio = Math.Clamp(position / extent, 0, 1);
        if (vertical)
        {
            ratio = 1 - ratio;
        }
        if (reversed)
        {
            ratio = 1 - ratio;
        }
        return minimum + ((maximum - minimum) * ratio);
    }

    private sealed class PenDragState(Slider slider)
    {
        private readonly Slider _slider = slider;
        private IPointer? _penPointer;

        public void Attach()
        {
            _slider.AddHandler(
                InputElement.PointerPressedEvent,
                (_, args) => OnPointerPressed(args),
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            _slider.AddHandler(
                InputElement.PointerMovedEvent,
                (_, args) => OnPointerMoved(args),
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            _slider.AddHandler(
                InputElement.PointerReleasedEvent,
                (_, args) => OnPointerReleased(args),
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            _slider.AddHandler(
                InputElement.PointerCaptureLostEvent,
                (_, _) => _penPointer = null,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
        }

        private void OnPointerPressed(PointerPressedEventArgs args)
        {
            if (!_slider.IsEnabled
                || !IsPrimaryPen(args))
            {
                return;
            }

            _penPointer = args.Pointer;
            UpdateValue(args);
            args.Pointer.Capture(_slider);
            args.PreventGestureRecognition();
            args.Handled = true;
        }

        private void OnPointerMoved(PointerEventArgs args)
        {
            if (!ReferenceEquals(args.Pointer, _penPointer))
            {
                return;
            }

            UpdateValue(args);
            args.PreventGestureRecognition();
            args.Handled = true;
        }

        private void OnPointerReleased(PointerReleasedEventArgs args)
        {
            if (!ReferenceEquals(args.Pointer, _penPointer))
            {
                return;
            }

            UpdateValue(args);
            _penPointer = null;
            args.Pointer.Capture(null);
            args.Handled = true;
        }

        private bool IsPrimaryPen(PointerPressedEventArgs args)
        {
            if (args.Pointer.Type != PointerType.Pen)
            {
                return false;
            }

            var properties = args.GetCurrentPoint(_slider).Properties;
            return !properties.IsRightButtonPressed
                && !properties.IsMiddleButtonPressed
                && !properties.IsBarrelButtonPressed
                && !properties.IsEraser;
        }

        private void UpdateValue(PointerEventArgs args)
        {
            var vertical = _slider.Orientation == Orientation.Vertical;
            var position = args.GetPosition(_slider);
            var rawValue = ValueFromPosition(
                _slider.Minimum,
                _slider.Maximum,
                vertical ? position.Y : position.X,
                vertical ? _slider.Bounds.Height : _slider.Bounds.Width,
                vertical,
                _slider.IsDirectionReversed);
            _slider.Value = Snap(rawValue);
        }

        private double Snap(double value)
        {
            if (!_slider.IsSnapToTickEnabled
                || _slider.TickFrequency <= 0)
            {
                return value;
            }

            var steps = Math.Round(
                (value - _slider.Minimum) / _slider.TickFrequency,
                MidpointRounding.AwayFromZero);
            return Math.Clamp(
                _slider.Minimum + (steps * _slider.TickFrequency),
                _slider.Minimum,
                _slider.Maximum);
        }
    }
}
