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
    internal static readonly TimeSpan PenUpdateInterval = TimeSpan.FromMilliseconds(16);

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
        private readonly DispatcherTimer _updateTimer = new()
        {
            Interval = PenUpdateInterval,
        };
        private IPointer? _penPointer;
        private double? _pendingValue;

        public void Attach()
        {
            _updateTimer.Tick += (_, _) => CommitPendingValue();
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
            QueueValue(args, commitImmediately: true);
            args.PreventGestureRecognition();
            args.Handled = true;
            Dispatcher.UIThread.Post(
                EnsureCapture,
                DispatcherPriority.Input);
        }

        private void OnPointerMoved(PointerEventArgs args)
        {
            if (!ReferenceEquals(args.Pointer, _penPointer))
            {
                return;
            }

            EnsureCapture();
            QueueValue(args, commitImmediately: false);
            args.PreventGestureRecognition();
            args.Handled = true;
        }

        private void OnPointerReleased(PointerReleasedEventArgs args)
        {
            if (!ReferenceEquals(args.Pointer, _penPointer))
            {
                return;
            }

            QueueValue(args, commitImmediately: true);
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

        private void QueueValue(
            PointerEventArgs args,
            bool commitImmediately)
        {
            _pendingValue = Snap(ValueAt(args));
            if (commitImmediately)
            {
                CommitPendingValue();
                return;
            }

            if (!_updateTimer.IsEnabled)
            {
                _updateTimer.Start();
            }
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
                _updateTimer.Stop();
                return;
            }

            _pendingValue = null;
            _slider.Value = value;
        }

        private void EnsureCapture()
        {
            if (_penPointer is not { } pointer
                || ReferenceEquals(pointer.Captured, _slider))
            {
                return;
            }

            pointer.Capture(_slider);
        }

        private void OnPointerCaptureLost()
        {
            if (_penPointer is null)
            {
                return;
            }

            Dispatcher.UIThread.Post(
                EnsureCapture,
                DispatcherPriority.Input);
        }

        private void Reset()
        {
            _penPointer = null;
            _pendingValue = null;
            _updateTimer.Stop();
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
