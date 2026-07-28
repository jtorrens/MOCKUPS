using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorSliderBehavior
{
    private static readonly ConditionalWeakTable<Slider, PenDragState> States = new();
    internal static readonly DispatcherPriority PenUpdatePriority = DispatcherPriority.Render;

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
        private double? _pendingValue;
        private bool _updateQueued;
        private int _updateGeneration;

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
                (_, _) => OnPointerCaptureLost(),
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            _slider.DetachedFromVisualTree += (_, _) => Reset();
        }

        private void OnPointerPressed(PointerPressedEventArgs args)
        {
            if (!_slider.IsEnabled
                || !IsPrimaryPen(args))
            {
                return;
            }

            _penPointer = args.Pointer;
            args.Pointer.Capture(_slider);
            _slider.Focus();
            CommitValue(ValueAt(args));
            args.PreventGestureRecognition();
            args.Handled = true;
        }

        private void OnPointerMoved(PointerEventArgs args)
        {
            if (!ReferenceEquals(args.Pointer, _penPointer))
            {
                return;
            }

            QueueValue(args);
            args.PreventGestureRecognition();
            args.Handled = true;
        }

        private void OnPointerReleased(PointerReleasedEventArgs args)
        {
            if (!ReferenceEquals(args.Pointer, _penPointer))
            {
                return;
            }

            CommitValue(ValueAt(args));
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

        private void QueueValue(PointerEventArgs args)
        {
            var value = Snap(ValueAt(args));
            _pendingValue = value;
            if (_updateQueued)
            {
                return;
            }

            _updateQueued = true;
            var generation = _updateGeneration;
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (generation != _updateGeneration)
                    {
                        return;
                    }

                    _updateQueued = false;
                    CommitPendingValue();
                },
                PenUpdatePriority);
        }

        private double ValueAt(PointerEventArgs args)
        {
            var track = _slider
                .GetVisualDescendants()
                .OfType<Track>()
                .FirstOrDefault();
            if (track is not null
                && track.Bounds.Width > 0
                && track.Bounds.Height > 0)
            {
                return track.ValueFromPoint(
                    args.GetPosition(track));
            }

            var vertical = _slider.Orientation == Orientation.Vertical;
            var position = args.GetPosition(_slider);
            return ValueFromPosition(
                _slider.Minimum,
                _slider.Maximum,
                vertical ? position.Y : position.X,
                vertical ? _slider.Bounds.Height : _slider.Bounds.Width,
                vertical,
                _slider.IsDirectionReversed);
        }

        private void CommitPendingValue()
        {
            if (_pendingValue is not { } value)
            {
                return;
            }

            _pendingValue = null;
            _slider.SetCurrentValue(
                RangeBase.ValueProperty,
                value);
        }

        private void CommitValue(double value)
        {
            CancelQueuedUpdate();
            _slider.SetCurrentValue(
                RangeBase.ValueProperty,
                Snap(value));
        }

        private void OnPointerCaptureLost()
        {
            if (_penPointer is null)
            {
                return;
            }

            Reset();
        }

        private void Reset()
        {
            _penPointer = null;
            CancelQueuedUpdate();
        }

        private void CancelQueuedUpdate()
        {
            _updateGeneration++;
            _updateQueued = false;
            _pendingValue = null;
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
